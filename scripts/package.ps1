[CmdletBinding()]
param([string]$BuildDirectory, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $BuildDirectory) { $BuildDirectory = Join-Path $repoRoot 'artifacts\build' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts\release' }
$releaseRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
$version = ([System.IO.File]::ReadAllText((Join-Path $repoRoot 'VERSION'))).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid VERSION.' }
$stage = Join-Path $releaseRoot ('package-stage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $BuildDirectory 'War3FontFix.exe') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $BuildDirectory 'War3FontFix.Cli.exe') -Destination $stage
    foreach ($name in @('README.md','README.en.md','LICENSE','NOTICE.md','CONTRIBUTING.md','CHANGELOG.md','VERSION')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $stage
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination $stage -Recurse
    Copy-Item -LiteralPath (Join-Path $repoRoot 'patches') -Destination $stage -Recurse
    $manifest = @(Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length + 1).Replace('\','/')
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative
    })
    [System.IO.File]::WriteAllText((Join-Path $stage 'SHA256SUMS.txt'), ($manifest -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
    $archive = Join-Path $releaseRoot "war3-cjk-font-fix-v$version-windows.zip"
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -Force
    $digest = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    [System.IO.File]::WriteAllText(($archive + '.sha256'), $digest + '  ' + [System.IO.Path]::GetFileName($archive) + "`n", [System.Text.UTF8Encoding]::new($false))
    Write-Output "Packaged $archive"
    Write-Output "SHA-256: $digest"
} finally {
    $checked = [System.IO.Path]::GetFullPath($stage)
    $parent = $releaseRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $checked.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([System.IO.Path]::GetFileName($checked)).StartsWith('package-stage-', [StringComparison]::Ordinal)) {
        throw 'Refusing to remove a directory outside the packaging stage.'
    }
    Remove-Item -LiteralPath $checked -Recurse -Force
}
