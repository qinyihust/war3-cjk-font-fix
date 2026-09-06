[CmdletBinding()]
param([string]$BuildDirectory, [string]$GameDll)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $BuildDirectory) { $BuildDirectory = Join-Path $repoRoot 'artifacts\build' }
$buildRoot = [System.IO.Path]::GetFullPath($BuildDirectory)
function Invoke-Checked([string]$Executable, [string[]]$Arguments) {
    # Redirect explicitly and wait: the installer is a GUI-subsystem executable.
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Arguments = (@($Arguments | ForEach-Object { '"' + ($_ -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"' }) -join ' ')
    $process = [System.Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        if ($stdout.Result) { Write-Output $stdout.Result.TrimEnd() }
        if ($stderr.Result) { Write-Output $stderr.Result.TrimEnd() }
        if ($process.ExitCode -ne 0) { throw "$Executable failed with exit code $($process.ExitCode)." }
    } finally { $process.Dispose() }
}
Invoke-Checked (Join-Path $buildRoot 'CoreTests.exe') @()
Invoke-Checked (Join-Path $buildRoot 'NativePatchTests.exe') @()
Invoke-Checked (Join-Path $buildRoot 'War3FontFix.exe') @('--version')
Invoke-Checked (Join-Path $buildRoot 'War3FontFix.Cli.exe') @('--help')
Invoke-Checked (Join-Path $buildRoot 'War3FontFix.Cli.exe') @('--list')
if ($GameDll) {
    $gameFile = (Resolve-Path -LiteralPath $GameDll).Path
    Invoke-Checked (Join-Path $buildRoot 'War3FontFix.Cli.exe') @('--self-test', $gameFile)
    Invoke-Checked (Join-Path $buildRoot 'NativePatchTests.exe') @($gameFile)
    Invoke-Checked (Join-Path $buildRoot 'LocalGameTests.exe') @($gameFile)
}
Write-Output 'PASS: all requested checks completed. No installed game file was modified.'
