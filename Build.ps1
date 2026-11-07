$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references = @('System.dll','System.Core.dll','System.Web.dll','System.Web.Extensions.dll','System.Xml.dll','System.Xml.Linq.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll') | ForEach-Object { '/r:' + (Join-Path $framework $_) }
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /codepage:65001 /target:winexe /platform:x64 /optimize+ /utf8output /win32manifest:app.manifest /win32icon:Panel.ico /resource:Panel.xaml,Panel.xaml '/out:2077主题控制台.exe' @references Panel.cs Backend.cs Localization.cs
    if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
} finally { Pop-Location }

