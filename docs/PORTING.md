# Supporting another game build

New profiles are reviewed binary patches, not compatibility aliases. Never accept a new full-file hash solely because the file version or nearby bytes appear similar.

1. Record the exact original SHA-256, length, architecture, renderer, and font environment. Obtain the file locally; do not upload or commit it.
2. Reproduce the rendering fault. Separate unsupported font glyphs, text encoding issues, layout errors, cache eviction, and display resets.
3. Locate the allocation, layout, invalidation, geometry, and upload paths in that binary. Confirm calling conventions, structures, termination conditions, and instruction boundaries independently.
4. Design the smallest validated changes for that build. Treat file offsets and RVAs separately; verify executable mapped pages, padding ownership, relative branch destinations, and relocation behavior.
5. Add a new profile in `patches/`. Use a distinct ID and backup name. Record each supported revision's complete output hash and explicit list of patch IDs.
6. Run the isolated native tests for every embedded build. The tests derive call destinations from each stub and cover both reviewed compiler families. A different ABI requires a different test model, not just another fingerprint.
7. Run synthetic tests, local full-file install/upgrade/restore tests, and actual cold-start game validation under documented resolutions and representative workloads.
8. Update the compatibility table and validation record. Keep unsupported configurations explicitly unverified.

Schema version 1 stores a list of equal-length, non-overlapping edits and known revisions. `fileOffset` selects bytes on disk; `rva` supports instruction/PE analysis. Schema version 2 additionally permits a revision-specific `fileLength` for appended code. Missing/zero revision length means the original length. Appended original bytes must be zero, edits must fit their selected revision, and growth is bounded to 1 MiB. Every supported revision still requires an exact full-file fingerprint. JSON is compiled into the installer; arbitrary external profiles are not accepted.

## Shared generator

`tools/engine-layouts.json` records reviewed RVAs and normalized instruction-anchor hashes. `tools/generate_profiles.py` generates all eight profiles using a common allocation/UTF-8/batch recipe, with an atlas-load/clear variation for the older compiler. The normalized anchors retain structure offsets and registers, masking only relative branch operands and absolute image addresses. The installer does not perform this scanning or accept unknown hashes.

Keep your own originals outside the checkout as `<originals>/<four-part-version>/Game.dll`, then run:

```powershell
python -m pip install -r tools/requirements.txt
python tools/generate_profiles.py "C:\Private\War3Originals" --check
```

Omitting `--check` rewrites the public JSON profiles, never the input DLLs. Generation requires the exact original fingerprint and verified anchors. Adding an unreviewed layout or changing its accepted hash is not a port.

1.21 and 1.24e lack sufficient `.text` tail space. Their recipe adds a read/execute `.cjk` section after all original file bytes, including overlays, with a checked unused section-header slot, aligned raw data and RVA, and updated section count, code size and image size. Only fully fingerprinted patched files can be truncated back to their original length during restoration. Other builds retain verified code-tail placement. The 1.25b v2 output hash is asserted explicitly to prevent accidental changes to its released payload.

A new native stub needs evidence for its register and stack behavior, engine callback assumptions, iteration/eviction termination, memory-write bounds, and behavior when reconstruction triggers another invalidation. Successful installation is not proof of correct rendering.
