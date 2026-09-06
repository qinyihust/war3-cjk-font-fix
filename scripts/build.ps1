[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts\build' }
$buildRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
$compiler = Join-Path ([System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Install .NET Framework 4.x or supply a Windows environment with csc.exe.' }
$version = ([System.IO.File]::ReadAllText((Join-Path $repoRoot 'VERSION'))).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain a three-part numeric version.' }
$assemblyInfo = Join-Path $buildRoot 'AssemblyInfo.cs'
[System.IO.File]::WriteAllText($assemblyInfo, "using System.Reflection;`n[assembly: AssemblyTitle(`"War3 CJK Font Fix`")]`n[assembly: AssemblyVersion(`"$version.0`")]`n[assembly: AssemblyFileVersion(`"$version.0`")]`n", [System.Text.UTF8Encoding]::new($false))
$sourceRoot = Join-Path $repoRoot 'src\War3FontFix'
$core = @('PatchProfile.cs','PatchEngine.cs','PatchService.cs') | ForEach-Object { Join-Path $sourceRoot $_ }
$resources = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'patches') -Filter '*.json' -File | Sort-Object Name | ForEach-Object {
    '/resource:' + $_.FullName + ',War3FontFix.Profiles.' + $_.Name
})
if ($resources.Count -eq 0) { throw 'No patch profiles were found.' }
$common = @('/nologo','/optimize+','/warn:4','/r:System.Core.dll','/r:System.Web.Extensions.dll')
function Invoke-Compiler([string[]]$CompilerArguments) {
    & $compiler @CompilerArguments
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }
}
Invoke-Compiler ($common + @('/target:winexe','/platform:anycpu','/r:System.Windows.Forms.dll','/r:System.Drawing.dll',('/out:' + (Join-Path $buildRoot 'War3FontFix.exe')), $assemblyInfo) + $resources + $core + @((Join-Path $sourceRoot 'Program.cs'),(Join-Path $sourceRoot 'MainForm.cs')))
Invoke-Compiler ($common + @('/target:exe','/define:CONSOLE_APP','/platform:anycpu','/r:System.Windows.Forms.dll','/r:System.Drawing.dll',('/out:' + (Join-Path $buildRoot 'War3FontFix.Cli.exe')), $assemblyInfo) + $resources + $core + @((Join-Path $sourceRoot 'Program.cs'),(Join-Path $sourceRoot 'MainForm.cs')))
Invoke-Compiler ($common + @('/target:exe','/platform:anycpu',('/out:' + (Join-Path $buildRoot 'CoreTests.exe')), $assemblyInfo, (Join-Path $repoRoot 'tests\CoreTests.cs')) + $resources + $core)
Invoke-Compiler ($common + @('/target:exe','/platform:anycpu',('/out:' + (Join-Path $buildRoot 'LocalGameTests.exe')), $assemblyInfo, (Join-Path $repoRoot 'tests\LocalGameTests.cs')) + $resources + $core)
Invoke-Compiler (@('/nologo','/optimize+','/warn:4','/target:exe','/platform:x86','/r:System.Core.dll','/r:System.Web.Extensions.dll',('/out:' + (Join-Path $buildRoot 'NativePatchTests.exe')), (Join-Path $repoRoot 'tests\NativePatchTests.cs'), (Join-Path $sourceRoot 'PatchProfile.cs'), (Join-Path $sourceRoot 'PatchEngine.cs')) + $resources)
Write-Output "Built installer and test executables in $buildRoot"
