# Building and testing

## Requirements

- Windows with the .NET Framework 4.x C# compiler and Windows Forms assemblies. The scripts locate the system `v4.0.30319\csc.exe`; a modern .NET SDK is not a replacement for this build path.
- Windows PowerShell 5.1 or PowerShell 7.
- A checkout in a directory where Git and the compiler can write. If Windows protects a Documents directory, use another writable development directory instead of changing protection settings.

The installer is built as AnyCPU; the native stub tests are explicitly x86. No NuGet packages, Python, Frida, or game files are required for the standard build and tests. The optional disassembly tool needs Python, `capstone`, and `pefile`.

## Build

```powershell
.\scripts\build.ps1
```

Outputs are under `artifacts/build/`:

- `War3FontFix.exe`: GUI executable, with compatibility support for redirected CLI calls.
- `War3FontFix.Cli.exe`: console executable for terminal and automation use.
- `CoreTests.exe`: synthetic-file unit and integration checks.
- `NativePatchTests.exe`: isolated execution of embedded x86 stubs, with engine dependencies replaced by test stand-ins.
- `LocalGameTests.exe`: opt-in full-size tests using a supplied local DLL and temporary copies.

`VERSION` controls installer assembly versions. `patches/*.json` is embedded into each relevant executable; profiles are not loaded from disk at runtime. Adding a profile requires rebuilding.

## Tests without game files

```powershell
.\scripts\test.ps1
```

This covers original/current round trips, legacy upgrade, unknown-file rejection, patch-site/final-hash verification, backup preservation, corrupt backups, missing backups, locked files, concurrent installers, external file changes, profile validation, read-only operations, and native-stub state preservation.

These checks do not simulate the whole game renderer. Native tests mock the engine's rebuild/render/memset functions and prove the tested stubs' contracts, not arbitrary in-game behavior.

## Optional local DLL tests

Exit Warcraft III, then use your own supported original or patched DLL:

```powershell
.\scripts\test.ps1 -GameDll "C:\Games\Warcraft III\Game.dll"
```

The input is read-only. The optional tests derive the exact original/current/legacy states and perform installation and restoration only in a freshly created temporary directory. Tests verify the input remains unchanged. Never add that DLL or generated fixtures to Git.

For read-only instruction and PE-layout checks of an already patched revision-2 DLL:

```powershell
python -m pip install -r tools/requirements.txt
python tools/verify_patch.py "C:\Games\Warcraft III\Game.dll"
```

## Package

```powershell
.\scripts\package.ps1
```

This creates `artifacts/release/war3-cjk-font-fix-v2.1.0-windows.zip` and a `.sha256` sidecar. The archive contains both user executables, documentation, license, profile definitions, and internal checksums. Test executables, complete game files, private evidence, and debug tools are excluded.

Each script accepts an output/build directory override; use `Get-Help .\scripts\build.ps1` and the script's parameter block for names. Build and test steps can also be run in GitHub Actions. CI artifacts are build outputs, not automatically published Releases.

The source is rebuildable, but the older system compiler can emit differing PE timestamps and metadata across builds. Do not assume two installer EXEs must have identical hashes. The patched **game DLL** must always match the profile's exact expected SHA-256.

Reference: [Microsoft C# compiler documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/) and [GitHub build/test workflows](https://docs.github.com/en/actions/tutorials/build-and-test-code/net).
