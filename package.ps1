$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $PSScriptRoot 'dist-v0.6\使用说明.md') -Force
$release = Join-Path $PSScriptRoot 'release'
New-Item -ItemType Directory -Path $release -Force | Out-Null
$archive = Join-Path $release '桌面随记-v0.6-Windows.zip'
$files = @('DeskNote.exe', 'DeskNote.exe.config', '使用说明.md') | ForEach-Object { Join-Path (Join-Path $PSScriptRoot 'dist-v0.6') $_ }
Compress-Archive -LiteralPath $files -DestinationPath $archive -Force
Write-Host ('便携版：' + $archive)








