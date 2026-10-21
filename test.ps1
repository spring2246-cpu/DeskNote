$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'dist-v0.6\DeskNote.exe'
if (!(Test-Path -LiteralPath $exe)) { & (Join-Path $PSScriptRoot 'build.ps1') }
Push-Location $PSScriptRoot
try {
    foreach ($test in @('self-test', 'render-test', 'smoke-test', 'desktop-test')) {
        $process = Start-Process -FilePath $exe -ArgumentList ('--' + $test), 'test-output' -WindowStyle Hidden -PassThru
        if (!$process.WaitForExit(120000)) { $process.Kill(); throw ($test + ' 超时') }
        $report = Join-Path $PSScriptRoot ('test-output\' + $test + '.txt')
        Get-Content -LiteralPath $report
        if ($process.ExitCode -ne 0 -or (Select-String -LiteralPath $report -Pattern '^FAIL')) { throw ($test + ' 失败') }
    }
} finally { Pop-Location }








