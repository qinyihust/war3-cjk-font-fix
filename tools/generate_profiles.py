"""Generate deterministic profiles from privately supplied, fingerprinted original DLLs.

This is a maintainer tool, not a signature-scanning installer. No game is modified.
Run: python tools/generate_profiles.py <directory-of-version-subdirectories> --check
Dependencies: tools/requirements.txt. See docs/PORTING.md.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

import capstone
import pefile

ROOT = Path(__file__).resolve().parents[1]
UTF8_IDS = ["defer-advance", "control-sequence", "accepted-character", "newline-advance", "narrow-line-progress"]


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def align(value, boundary):
    return (value + boundary - 1) // boundary * boundary


def branch(opcode, source, target):
    return opcode + struct.pack("<i", target - source - len(opcode) - 4)


def normalized(data, start, size):
    """Only mask branch displacements and absolute image addresses; retain ABI operands."""
    cs = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
    cs.detail = True
    code = bytearray(data[start:start + size])
    instructions = list(cs.disasm(code, start))
    require(sum(i.size for i in instructions) == size, "Incomplete anchor instruction")
    for ins in instructions:
        for operand in ins.operands:
            offset = count = 0
            if operand.type == capstone.x86.X86_OP_IMM and (ins.group(capstone.CS_GRP_CALL) or ins.group(capstone.CS_GRP_JUMP) or 0x6F000000 <= operand.imm < 0x70000000):
                offset, count = ins.imm_offset, ins.imm_size
            elif operand.type == capstone.x86.X86_OP_MEM and operand.mem.base == 0 and operand.mem.index == 0 and 0x6F000000 <= operand.mem.disp < 0x70000000:
                offset, count = ins.disp_offset, ins.disp_size
            for k in range(offset, offset + count):
                code[ins.address - start + k] = 0
    return sha(code)


def generate(data, layout):
    require(len(data) == layout["fileLength"] and sha(data) == layout["originalSha256"], "Original DLL fingerprint mismatch")
    pe = pefile.PE(data=data, fast_load=True)
    require(pe.FILE_HEADER.Machine == 0x14C and pe.OPTIONAL_HEADER.ImageBase == 0x6F000000, "Expected x86 Warcraft III image")
    for anchor in layout["anchors"]:
        require(normalized(data, pe.get_offset_from_rva(anchor["rva"]), anchor["size"]) == anchor["normalizedSha256"], "Engine anchor mismatch")
    sites = []
    source = bytearray(data)
    batch, atlas = layout["batchStub"], layout["atlasStub"]

    def add(name, rva, replacement, expected=None, offset=None):
        if offset is None:
            offset = pe.get_offset_from_rva(rva)
        original = bytes(source[offset:offset + len(replacement)])
        require(len(original) == len(replacement), "Patch outside backing file")
        if expected is not None:
            require(original == expected, "Unexpected instruction: " + name)
        sites.append(dict(id=name, fileOffset=offset, rva=rva, original=original.hex().upper(), replacement=replacement.hex().upper()))

    stub_offset = None
    if layout["placement"] == "new-section":
        # Preserve every original byte, including the overlay. Add a distinct RX section.
        file_align, section_align = pe.OPTIONAL_HEADER.FileAlignment, pe.OPTIONAL_HEADER.SectionAlignment
        require(file_align == 0x1000 and section_align == 0x1000, "Unexpected PE alignment")
        slot = pe.sections[-1].get_file_offset() + 40
        require(slot + 40 <= pe.OPTIONAL_HEADER.SizeOfHeaders and not any(data[slot:slot + 40]), "No vacant section header")
        batch = align(max(s.VirtualAddress + max(s.Misc_VirtualSize, s.SizeOfRawData) for s in pe.sections), section_align)
        atlas = batch + 64
        require(batch == layout["batchStub"] and atlas == layout["atlasStub"], "Reviewed section layout differs from PE placement")
        stub_offset = align(len(data), file_align)
        source.extend(bytes(stub_offset + file_align - len(source)))
        header = struct.pack("<8sIIIIIIHHI", b".cjk\0\0\0\0", 128, batch, file_align, stub_offset, 0, 0, 0, 0, 0x60000020)
        add("pe-cjk-section", slot, header, bytes(40), slot)
        off = pe.FILE_HEADER.get_field_absolute_offset("NumberOfSections")
        add("pe-section-count", off, struct.pack("<H", pe.FILE_HEADER.NumberOfSections + 1), offset=off)
        off = pe.OPTIONAL_HEADER.get_field_absolute_offset("SizeOfImage")
        add("pe-image-size", off, struct.pack("<I", batch + section_align), offset=off)
        off = pe.OPTIONAL_HEADER.get_field_absolute_offset("SizeOfCode")
        add("pe-code-size", off, struct.pack("<I", pe.OPTIONAL_HEADER.SizeOfCode + file_align), offset=off)
    else:
        section = pe.get_section_by_rva(batch)
        require(section is not None and section.Characteristics & 0x20000000, "Code tail is not executable")
        require(batch >= section.VirtualAddress + section.Misc_VirtualSize, "Code tail overlaps declared content")
        require(atlas + 33 <= section.VirtualAddress + section.SizeOfRawData, "Insufficient code-tail backing")
        require((atlas + 32) // 4096 == (section.VirtualAddress + section.Misc_VirtualSize - 1) // 4096, "Code tail exceeds final mapped page")

    old = layout["family"] == "frame-pointer"
    retry = layout["retry"]
    add("glyph-allocation-retry", retry, branch(b"\x0f\x84", retry, layout["retryTarget"]), bytes.fromhex("0F84B5000000" if old else "0F84ED000000"))
    utf8_before = ["7502", "0F8482000000", "7403", "7411", "7403"] if old else ["7502", "0F84B5000000", "7404", "7416", "7404"]
    for i, (rva, expected) in enumerate(zip(layout["utf8"], utf8_before)):
        add("utf8-" + UTF8_IDS[i], rva, bytes.fromhex("EB02") if i == 0 else b"\x90" * (len(expected) // 2), bytes.fromhex(expected))
    # Same intrusive-list ABI and dirty flag in both compiler families.
    rebuild = bytes.fromhex("9C608BF98B772485F67E2483BE80000000007411C78680000000000000008BCE")
    rebuild += branch(b"\xe8", batch + len(rebuild), layout["rebuild"])
    rebuild += bytes.fromhex("8B471C03C68B7004EBD8619D")
    rebuild += branch(b"\xe9", batch + len(rebuild), layout["render"])
    require(len(rebuild) == 54, "Batch recipe length changed")
    hook = layout["atlasHook"]
    load = bytes.fromhex("8BB0B0010000" if old else "8B88B0010000")
    if old:
        # The older compiler inlines memset; explicitly clear DF, then restore all flags.
        clear = bytes.fromhex("9C60FC33C0B9000001008B7B04F3AB619D") + load
    else:
        clear = bytes.fromhex("9C6068000004006A00FF7304")
        clear += branch(b"\xe8", atlas + len(clear), layout["memset"])
        clear += bytes.fromhex("83C40C619D") + load
    clear += branch(b"\xe9", atlas + len(clear), hook + len(load))
    add("batch-rebuild-stub", batch, rebuild, bytes(len(rebuild)), stub_offset)
    add("atlas-clear-stub", atlas, clear, bytes(len(clear)), None if stub_offset is None else stub_offset + 64)
    add("batch-rebuild-hook", layout["batchHook"], branch(b"\xe8", layout["batchHook"], batch), branch(b"\xe8", layout["batchHook"], layout["render"]))
    add("atlas-clear-hook", hook, branch(b"\xe9", hook, atlas) + b"\x90", load)
    add("atlas-copy-live-glyphs", layout["atlasCopy"], b"\x90" * 6, bytes.fromhex("0F840C010000" if old else "0F84E0000000"))
    result = bytearray(source)
    for site in sites:
        off = site["fileOffset"]
        result[off:off + len(site["replacement"]) // 2] = bytes.fromhex(site["replacement"])
    version = layout["version"]
    revision = dict(revision=2, sha256=sha(result), patchIds=[s["id"] for s in sites])
    profile = dict(schemaVersion=2 if stub_offset is not None else 1, id="war3-" + version, gameVersion=version, currentRevision=2,
                   fileLength=len(data), originalSha256=sha(data), backupFileName="Game.dll.before-fontfix-" + version.replace(".", "") + ".bak", patches=sites, revisions=[revision])
    if stub_offset is not None:
        revision["fileLength"] = len(result)
    if version == "1.25.1.6397":
        # Keep the released v1/v2 identities and migration paths byte-for-byte.
        profile["revisions"].insert(0, dict(revision=1, sha256="7435968F413C72E78ADD12CB4F6B6CD1A050D47C142A44AEB47BF023BC237B3E", patchIds=[s["id"] for s in sites[:6]]))
        require(sha(result) == "77BB9CBA5EA090775D02995003BADE493707E4D987B08DF2AE93B8B0B95CEB98", "Released v2 payload changed")
    return profile, bytes(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("originals", type=Path)
    parser.add_argument("--check", action="store_true", help="compare profiles without writing")
    args = parser.parse_args()
    for layout in json.loads((ROOT / "tools/engine-layouts.json").read_text(encoding="utf-8")):
        data = (args.originals / layout["version"] / "Game.dll").read_bytes()
        profile, _ = generate(data, layout)
        target = ROOT / "patches" / (profile["id"] + ".json")
        if args.check:
            require(profile == json.loads(target.read_text(encoding="utf-8")), "Generated profile differs: " + target.name)
        else:
            target.write_text(json.dumps(profile, indent=2) + "\n", encoding="utf-8")
        print("PASS", layout["version"], profile["revisions"][-1]["sha256"])


if __name__ == "__main__":
    main()
