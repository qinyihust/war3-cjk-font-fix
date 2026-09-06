# Changelog

## 2.0.0

First repository-oriented public release, using patch-data revision 2.

- Separate patch definitions, byte transformations, file operations, and UI.
- Embed validated version profiles; retain exact original/revision-1/revision-2 fingerprints.
- Replace a machine-specific default directory with folder selection and optional detection beside the tool.
- Provide Chinese/English UI labels and a dedicated console executable.
- Keep the original backup after restoration; support restoration from recognized patch data when the backup is missing.
- Serialize concurrent installer operations for the same normalized directory within a Windows session.
- Add synthetic file tests, isolated native x86 tests, optional local full-file tests, build/package scripts, and GitHub CI.
- Publish installation, compatibility, design, validation, contribution, and porting documentation.

The patched game DLL is byte-identical to the revision-2 DLL used in the recorded long-session acceptance test.

## Patch-data revision 2 — local validation

- Retain revision 1's allocation retry and UTF-8 cursor fixes.
- Rebuild invalidated batch strings before rendering.
- Clear dirty glyph atlases and repopulate every live glyph, including already-cached CPU bitmaps.
- Pass the recorded 45-minute 48-second session for the documented environment.

## Patch-data revision 1 — superseded

- Correct insufficient-space retry after glyph eviction.
- Advance the UTF-8 line cursor only after accepting a character.
- Short tests improved tooltips; a later session exposed residual cached-glyph substitution. Revision 1 is supported as an upgrade/restore input and is not the release target.
