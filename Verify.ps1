$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references = @('System.dll','System.Core.dll','System.Web.dll','System.Web.Extensions.dll','System.Xml.dll','System.Xml.Linq.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /codepage:65001 /target:winexe /platform:x64 /utf8output /main:Codex2077.VerifyUi /resource:Panel.xaml,Panel.xaml /out:VerifyUi.exe @references Panel.cs Backend.cs Localization.cs VerifyUi.cs
    if ($LASTEXITCODE -ne 0) { throw '检查程序编译失败。' }
    $check = Start-Process -FilePath (Join-Path $PSScriptRoot 'VerifyUi.exe') -WindowStyle Hidden -Wait -PassThru
    Get-Content -LiteralPath (Join-Path $PSScriptRoot 'verification\ui-check.txt') -Encoding UTF8
    if ($check.ExitCode -ne 0) { throw '检查未通过。' }
} finally { Pop-Location }

