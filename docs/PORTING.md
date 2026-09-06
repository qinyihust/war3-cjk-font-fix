# Supporting another game build

New profiles are reviewed binary patches, not compatibility aliases. Never accept a new full-file hash solely because the file version or nearby bytes appear similar.

1. Record the exact original SHA-256, length, architecture, renderer, and font environment. Obtain the file locally; do not upload or commit it.
2. Reproduce the rendering fault. Separate unsupported font glyphs, text encoding issues, layout errors, cache eviction, and display resets.
3. Locate the allocation, layout, invalidation, geometry, and upload paths in that binary. Confirm calling conventions, structures, termination conditions, and instruction boundaries independently.
4. Design the smallest validated changes for that build. Treat file offsets and RVAs separately; verify executable mapped pages, padding ownership, relative branch destinations, and relocation behavior.
5. Add a new profile in `patches/`. Use a distinct ID and backup name. Record each supported revision's complete output hash and explicit list of patch IDs.
6. Add isolated native tests for that build's code. The existing native tests target 1.25.1.6397 and cannot certify different addresses or structures.
7. Run synthetic tests, local full-file install/upgrade/restore tests, and actual cold-start game validation under documented resolutions and representative workloads.
8. Update the compatibility table and validation record. Keep unsupported configurations explicitly unverified.

Schema version 1 stores a list of non-overlapping edits and known revisions. `fileOffset` selects bytes on disk; `rva` supports instruction/PE analysis. The engine only identifies builds by exact length plus SHA-256. JSON is compiled into the installer; arbitrary external profiles are not accepted.

A new native stub needs evidence for its register and stack behavior, engine callback assumptions, iteration/eviction termination, memory-write bounds, and behavior when reconstruction triggers another invalidation. Successful installation is not proof of correct rendering.
