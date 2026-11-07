$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'The 64-bit .NET Framework compiler is required on Windows.'
}

# This check never inspects installed applications or connects to Codex.
$verificationRoot = Join-Path $PSScriptRoot 'verification'
[void][System.IO.Directory]::CreateDirectory($verificationRoot)
$fixtureRoot = Join-Path $verificationRoot ('backend-fixture-' + [Guid]::NewGuid().ToString('N'))
$realDirectory = Join-Path $fixtureRoot 'real'
$junctionDirectory = Join-Path $fixtureRoot 'junction'
$realFile = Join-Path $realDirectory 'sample.exe'
$junctionFile = Join-Path $junctionDirectory 'sample.exe'
$testExecutable = Join-Path $fixtureRoot 'BackendTests.exe'
$createdJunction = $false
$references = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Xml.dll','System.Xml.Linq.dll') |
    ForEach-Object { '/r:' + (Join-Path $framework $_) }

try {
    [void][System.IO.Directory]::CreateDirectory($realDirectory)
    [System.IO.File]::WriteAllText($realFile, 'isolated path identity fixture')
    New-Item -ItemType Junction -Path $junctionDirectory -Target $realDirectory -ErrorAction Stop | Out-Null
    $createdJunction = $true

    & $compiler /nologo /codepage:65001 /target:exe /platform:x64 /utf8output /main:BackendTests "/out:$testExecutable" @references `
        (Join-Path $PSScriptRoot 'Backend.cs') (Join-Path $PSScriptRoot 'Localization.cs') (Join-Path $PSScriptRoot 'Validation\BackendTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Backend test compilation failed.' }
    & $testExecutable $realFile $junctionFile
    if ($LASTEXITCODE -ne 0) { throw 'Backend verification failed.' }
} finally {
    # Remove the junction itself first; never recursively traverse its target.
    if ($createdJunction) { [System.IO.Directory]::Delete($junctionDirectory) }
    if (Test-Path -LiteralPath $testExecutable -PathType Leaf) { [System.IO.File]::Delete($testExecutable) }
    if (Test-Path -LiteralPath $realFile -PathType Leaf) { [System.IO.File]::Delete($realFile) }
    if (Test-Path -LiteralPath $realDirectory -PathType Container) { [System.IO.Directory]::Delete($realDirectory) }
    if (Test-Path -LiteralPath $fixtureRoot -PathType Container) { [System.IO.Directory]::Delete($fixtureRoot) }
}
