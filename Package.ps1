param(
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$')]
    [string]$Version = '1.0.0',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') }
& node (Join-Path $PSScriptRoot 'Theme/scripts/theme.mjs') check
if ($LASTEXITCODE -ne 0) { throw 'Theme validation failed.' }

Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$dist = Join-Path $PSScriptRoot 'dist'
[void][System.IO.Directory]::CreateDirectory($dist)
$archivePath = Join-Path $dist ("Codex2077-v{0}-windows-x64.zip" -f $Version)
$files = @('2077主题控制台.exe','README.md','README.en.md','LICENSE','CHANGELOG.md','使用说明.txt')
$files += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Theme') -Recurse -File | ForEach-Object { $_.FullName.Substring($PSScriptRoot.Length + 1) }
$files += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'docs') -Recurse -File | ForEach-Object { $_.FullName.Substring($PSScriptRoot.Length + 1) }
foreach ($relative in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $relative) -PathType Leaf)) { throw "Missing release file: $relative" }
    if ($relative -match '(?i)(settings\.json|\.env$|node_modules|verification)') { throw "Unexpected release file: $relative" }
}
$stream = [System.IO.File]::Open($archivePath,[System.IO.FileMode]::Create,[System.IO.FileAccess]::ReadWrite)
$archive = New-Object System.IO.Compression.ZipArchive($stream,[System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($relative in $files) {
        $entry = 'Codex2077/' + $relative.Replace('\','/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $PSScriptRoot $relative),$entry,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $stream.Dispose() }
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum = "$hash  $([System.IO.Path]::GetFileName($archivePath))`n"
[System.IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'),$checksum,(New-Object System.Text.UTF8Encoding($false)))
Write-Output $archivePath
Write-Output $checksum
