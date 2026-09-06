"""Read-only instruction and PE-layout checks for any current-revision DLL."""
import argparse
import hashlib
import json
from pathlib import Path

import capstone
import pefile
from generate_profiles import ROOT, generate, sha


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game_dll", type=Path)
    args = parser.parse_args()
    game = args.game_dll.read_bytes()
    profiles = [json.loads(p.read_text(encoding="utf-8")) for p in (ROOT / "patches").glob("*.json")]
    matches = [(p, r) for p in profiles for r in p["revisions"] if r["revision"] == p["currentRevision"] and r["sha256"] == sha(game)]
    require(len(matches) == 1, "Exact supported current-revision DLL required")
    profile, current = matches[0]
    require(len(game) == current.get("fileLength", profile["fileLength"]), "Incorrect revision length")
    layout = next(e for e in json.loads((ROOT / "tools/engine-layouts.json").read_text()) if e["version"] == profile["gameVersion"])
    original = bytearray(game)
    pe = pefile.PE(data=game)
    require(pe.FILE_HEADER.Machine == 0x14C, "Expected x86 PE")
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
    decoder.detail = True
    for patch in profile["patches"]:
        code = bytes.fromhex(patch["replacement"])
        offset, rva = patch["fileOffset"], patch["rva"]
        require(game[offset:offset + len(code)] == code, "Patch bytes differ: " + patch["id"])
        original[offset:offset + len(code)] = bytes.fromhex(patch["original"])
        require(pe.get_offset_from_rva(rva) == offset, "RVA/file-offset mismatch")
        if patch["id"].startswith("pe-"):
            continue
        instructions = list(decoder.disasm(code, rva))
        require(sum(i.size for i in instructions) == len(code), "Incomplete instruction region")
        if patch["id"] not in {"batch-rebuild-stub", "atlas-clear-stub"}:
            continue
        section = next(s for s in pe.sections if s.VirtualAddress <= rva < s.VirtualAddress + s.SizeOfRawData)
        require(section.Characteristics & 0x20000000 and not section.Characteristics & 0x80000000, "Stub must be executable and non-writable")
        require((section.VirtualAddress + section.Misc_VirtualSize - 1) // 4096 == (rva + len(code) - 1) // 4096,
                "Stub does not fit the existing final mapped page")
        calls = [i.operands[0].imm for i in instructions if i.mnemonic == "call"]
        if patch["id"] == "batch-rebuild-stub":
            require(calls == [layout["rebuild"]], "Unexpected rebuild target")
            require(instructions[-1].operands[0].imm == layout["render"], "Unexpected render continuation")
            boundaries = {i.address for i in instructions}
            for instruction in instructions[:-1]:
                if instruction.mnemonic.startswith("j"):
                    require(instruction.operands[0].imm in boundaries, "Internal branch misses instruction boundary")
        else:
            require(calls == ([layout["memset"]] if layout["memset"] else []), "Unexpected memset target")
            require(instructions[-1].operands[0].imm == layout["atlasHook"] + 6, "Unexpected atlas continuation")
            if layout["memset"]:
                require(instructions[2].operands[0].imm == 262144, "Unexpected clear size")
            else:
                require(any(i.mnemonic == "mov" and i.op_str == "ecx, 0x10000" for i in instructions), "Unexpected inline clear size")
    original = bytes(original[:profile["fileLength"]])
    require(sha(original) == profile["originalSha256"], "Restoration/overlay hash")
    regenerated, result = generate(original, layout)
    require(regenerated == profile and result == game, "Deterministic recipe differs")
    if layout["placement"] == "new-section":
        section = pe.sections[-1]
        require(section.Name == b".cjk\0\0\0\0" and section.Misc_VirtualSize == 128, "Unexpected new section")
        require(pe.OPTIONAL_HEADER.SizeOfImage == section.VirtualAddress + 4096, "Image size")
        require(section.PointerToRawData >= profile["fileLength"], "Original overlay was overwritten")
    print("PASS", profile["gameVersion"], "fingerprint, PE mapping, instructions, branches, ABI anchors, deterministic generation, exact restoration")


if __name__ == "__main__":
    main()
