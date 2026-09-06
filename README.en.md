# War3 CJK Font Fix

[简体中文](README.md)

A fingerprint-checked patch for overlapping, overflowing, or substituted Chinese glyphs in high-resolution classic Warcraft III. It fixes font-cache and line-layout behavior in `Game.dll`. No resident injector or window-mode toggle is needed after installation.

**Only one verified Warcraft III 1.25.1.6397 binary is currently supported.** The version number alone is insufficient. Other builds, modified DLLs, later game versions, and Reforged are not implicitly supported.

## Install

Download the Windows ZIP from [Releases](https://github.com/qinyihust/war3-cjk-font-fix/releases), extract it, and exit all Warcraft III processes. Open `War3FontFix.exe`, browse to the directory containing `Game.dll`, check the file, and select **Install / upgrade**. Start the game normally after verification succeeds.

The tool can run from any directory. It selects its own directory only when a `Game.dll` is present there. The UI follows the system language (Chinese or English); CLI diagnostics are in English. Windows and .NET Framework 4.x are required. See [build requirements](docs/BUILDING.md).

The patch changes only the recognized `Game.dll`; maps, fonts, saves, registry settings, and display modes are not edited. Unknown files are rejected. Repeating installation is a no-op when the current patch is already installed.

## Restore

Exit the game and select **Restore original**. The installer verifies the exact original and retains `Game.dll.before-fontfix-12516397.bak`. An existing backup with a different fingerprint is never overwritten. If the backup is missing, a recognized patched file can reconstruct the exact original. This does not recover unknown or damaged files.

## CLI

Use the console executable for standard output, waiting, and exit codes:

```powershell
.\War3FontFix.Cli.exe --check "C:\Games\Warcraft III"
.\War3FontFix.Cli.exe --apply "C:\Games\Warcraft III"
.\War3FontFix.Cli.exe --restore "C:\Games\Warcraft III"
.\War3FontFix.Cli.exe --self-test "C:\Games\Warcraft III\Game.dll"
.\War3FontFix.Cli.exe --list
```

`--check` and `--self-test` do not modify the game. Exit codes: `0` success, `1` operation failure, `2` invalid arguments or unsupported file. The GUI executable retains these arguments for compatibility with callers that redirect its output.

## Scope and evidence

Revision 2 changes 102 bytes at 11 locations in a 12,042,240-byte DLL. Both the original and revision 1 can be upgraded. See [exact fingerprints](docs/COMPATIBILITY.md).

A 45-minute 48-second session used Direct3D 8, 3120 × 2080, IMBA 3.86b AI, and nine AI players. Periodic checks of skill/item tooltips and persistent labels found no recurrence, and the game generated no new crash reports. The engine patch does not depend on the map, but other maps, renderers, replacement fonts, multiplayer platforms, and anti-cheat environments have not been validated.

The public installer was refactored while preserving the exact patched DLL hash. Its tests are separate from the earlier game session; the long session was not rerun for this packaging refactor. Private memory increased during that session, and no frame-rate or leak benchmark was performed. See [validation and limitations](docs/VALIDATION.md).

## Development

Run these commands from a source checkout. The Windows release archive does not include development scripts.

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\package.ps1
```

Default tests use synthetic files and embedded x86 stubs; no game files are required. Optional full-file tests read your local DLL and modify temporary copies only.

Further documentation: [building/testing](docs/BUILDING.md), [root-cause analysis](docs/ARCHITECTURE.md), [porting](docs/PORTING.md), [contributing](CONTRIBUTING.md), and [changelog](CHANGELOG.md).

## License

Project code and documentation are licensed under [MIT](LICENSE). Warcraft III and related trademarks belong to their respective owners; this project is not affiliated with Blizzard. Complete game binaries are not distributed. See [NOTICE](NOTICE.md).
