# War3 CJK Font Fix

[简体中文](README.md)

A fingerprint-checked patch for overlapping, overflowing, or substituted Chinese glyphs in high-resolution classic Warcraft III. It fixes font-cache and line-layout behavior in `Game.dll`. No resident injector or window-mode toggle is needed after installation.

**Eight exact builds are adapted: 1.20e, 1.21, 1.22, 1.23, 1.24b, 1.24e, 1.25b, and 1.26.** See the [compatibility table](docs/COMPATIBILITY.md) for fingerprints and runtime validation. The version label alone is insufficient. Unknown/modified DLLs, 1.27 and later, and Reforged are rejected.

## Install

Download the Windows ZIP from [Releases](https://github.com/qinyihust/war3-cjk-font-fix/releases), extract it, and exit all Warcraft III processes. Open `War3FontFix.exe`, browse to the directory containing `Game.dll`, check the file, and select **Install / upgrade**. Start the game normally after verification succeeds.

The tool can run from any directory. It selects its own directory only when a `Game.dll` is present there. The UI follows the system language (Chinese or English); CLI diagnostics are in English. Windows and .NET Framework 4.x are required. See [build requirements](docs/BUILDING.md).

The patch changes only the recognized `Game.dll`; maps, fonts, saves, registry settings, and display modes are not edited. Unknown files are rejected. Repeating installation is a no-op when the current patch is already installed.

## Restore

Exit the game and select **Restore original**. The installer verifies the exact original and retains a separate backup for each build, such as `Game.dll.before-fontfix-12516397.bak` for 1.25b. An existing backup with a different fingerprint is never overwritten. A recognized patched file can reconstruct its original even without the backup. Unknown or damaged files cannot be recovered this way.

After using a game version converter, run this tool again: replacing the game DLL also replaces the font patch. This tool patches the currently installed build; it does not switch game versions.

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

One shared generator produces eight fingerprinted profiles. Builds 1.21 and 1.24e gain a separate read/execute `.cjk` section; the others use verified code-section padding. Restoration preserves the exact original file, including its overlay. The released 1.25b payload remains byte-for-byte identical, with its revision-1 upgrade path preserved. See [exact fingerprints](docs/COMPATIBILITY.md).

A 45-minute 48-second session on 1.25b used Direct3D 8, 3120 × 2080, IMBA 3.86b AI, and nine AI players. Periodic checks of skill/item tooltips and persistent labels found no recurrence, and the game generated no new crash reports. The engine patch does not depend on the map. Renderer, font, multiplayer-platform and anti-cheat compatibility require separate validation.

The 1.25b payload keeps its released hash. New builds receive separate runtime and pressure checks; one build's long session does not certify all builds for long games. Private memory increased during the earlier session, and no frame-rate or leak benchmark was performed. See [validation and limitations](docs/VALIDATION.md).

## Development

Run these commands from a source checkout. The Windows release archive does not include development scripts.

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\package.ps1
```

Default tests use synthetic files and embedded x86 stubs; no game files are required. Optional full-file tests read your local DLL and modify temporary copies only.

Further documentation: [technical report (Chinese)](docs/TECHNICAL_REPORT.md), [building/testing](docs/BUILDING.md), [root-cause analysis](docs/ARCHITECTURE.md), [porting](docs/PORTING.md), [contributing](CONTRIBUTING.md), and [changelog](CHANGELOG.md).

## License

Project code and documentation are licensed under [MIT](LICENSE). Warcraft III and related trademarks belong to their respective owners; this project is not affiliated with Blizzard. Complete game binaries are not distributed. See [NOTICE](NOTICE.md).
