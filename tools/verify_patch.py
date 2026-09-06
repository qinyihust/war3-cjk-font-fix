"""Read-only instruction and PE-layout checks for the supported revision-2 DLL."""
import argparse
import hashlib
import json
from pathlib import Path

import capstone
import pefile


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("game_dll", type=Path)
    args = parser.parse_args()
    profile_path = Path(__file__).resolve().parents[1] / "patches" / "war3-1.25.1.6397.json"
    profile = json.loads(profile_path.read_text(encoding="utf-8"))
    current = next(r for r in profile["revisions"] if r["revision"] == profile["currentRevision"])
    game = args.game_dll.read_bytes()
    require(len(game) == profile["fileLength"], "Unsupported file length")
    require(hashlib.sha256(game).hexdigest().upper() == current["sha256"], "Exact patched revision-2 DLL required")
    pe = pefile.PE(data=game)
    require(pe.FILE_HEADER.Machine == 0x14C, "Expected x86 PE")
    decoder = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
    decoder.detail = True
    for patch in profile["patches"]:
        code = bytes.fromhex(patch["replacement"])
        offset, rva = patch["fileOffset"], patch["rva"]
        require(game[offset:offset + len(code)] == code, "Patch bytes differ: " + patch["id"])
        require(pe.get_offset_from_rva(rva) == offset, "RVA/file-offset mismatch")
        instructions = list(decoder.disasm(code, rva))
        require(sum(i.size for i in instructions) == len(code), "Incomplete instruction region")
        if patch["id"] not in {"batch-rebuild-stub", "atlas-clear-stub"}:
            continue
        section = next(s for s in pe.sections if s.VirtualAddress <= rva < s.VirtualAddress + s.SizeOfRawData)
        require(section.Characteristics & 0x20000000, "Stub is not in an executable section")
        require((section.VirtualAddress + section.Misc_VirtualSize - 1) // 4096 == (rva + len(code) - 1) // 4096,
                "Stub does not fit the existing final mapped page")
        print("\n".join(f"{i.address:08x} {i.mnemonic} {i.op_str}" for i in instructions))
        calls = [i.operands[0].imm for i in instructions if i.mnemonic == "call"]
        if patch["id"] == "batch-rebuild-stub":
            require(calls == [0x7C7110], "Unexpected rebuild target")
            require(instructions[-1].operands[0].imm == 0x7C5EB0, "Unexpected render continuation")
            boundaries = {i.address for i in instructions}
            for instruction in instructions[:-1]:
                if instruction.mnemonic.startswith("j"):
                    require(instruction.operands[0].imm in boundaries, "Internal branch misses instruction boundary")
        else:
            require(calls == [0x7E0F92], "Unexpected memset target")
            require(instructions[-1].operands[0].imm == 0x7C1AD7, "Unexpected atlas continuation")
            require(instructions[2].operands[0].imm == 262144, "Unexpected clear size")
    print("PASS: exact fingerprint and patch bytes, instruction boundaries, call/branch targets, clear size, and existing executable mapped page")


if __name__ == "__main__":
    main()
