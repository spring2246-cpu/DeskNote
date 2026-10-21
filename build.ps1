param([string]$OutputDirectory = 'dist-v0.6')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '需要 Windows .NET Framework 4.8。' }
$output = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Force -Path $output | Out-Null
$refs = @('System.dll','System.Core.dll','System.Xml.dll','System.Security.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll')
$argsList = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',('/out:' + (Join-Path $output 'DeskNote.exe')))
foreach ($ref in $refs) { $argsList += '/reference:' + (Join-Path $framework $ref) }
$argsList += @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$argsList += '/win32manifest:' + (Join-Path $root 'src\DeskNote.manifest')
$argsList += '/win32icon:' + (Join-Path $root 'assets\DeskNote.ico')
$argsList += '/resource:' + (Join-Path $root 'assets\DeskNote.ico') + ',DeskNote.AppIcon'
$argsList += '/resource:' + (Join-Path $root 'src\DarkTheme.xaml') + ',DeskNote.DarkTheme'
& $compiler @argsList
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Copy-Item -LiteralPath (Join-Path $root 'src\DeskNote.exe.config') -Destination $output -Force
Write-Host ('构建完成：' + (Join-Path $output 'DeskNote.exe'))








