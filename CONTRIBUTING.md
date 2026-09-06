# Contributing

Changes should keep the supported game-file fingerprints explicit. Do not weaken identification to a version string or partial byte signature.

For an issue, provide the installer version, `--check` output, game version, resolution, renderer, replacement-font status, and a short reproduction sequence. A screenshot of the affected tooltip can help; remove unrelated personal content. Do not attach game binaries, maps, full memory dumps, or credentials.

For a pull request:

1. Explain the visible problem and resulting behavior.
2. Build and run the default tests. Add a test for a changed binary transformation or meaningful file-operation failure path.
3. For patch-data changes, provide full-file hashes and independent instruction/runtime evidence. See [porting](docs/PORTING.md).
4. Update compatibility, validation, and changelog entries without generalizing beyond the tested environment.

Keep generated executables and test fixtures under `artifacts/` or `local-evidence/`. CI must run without copyrighted game files. Do not upload a DLL to a workflow artifact or repository, even if it is ignored locally.

The project uses four-space C# indentation and explicit error paths. Patch IDs and schema fields remain stable so published revisions can be upgraded or restored. Installer versions are tracked in `VERSION`; game-patch revisions are tracked separately in the embedded profiles.
