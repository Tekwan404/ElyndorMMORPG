[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$launcher = Join-Path $PSScriptRoot 'Elyndor.ps1'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) "elyndor-launcher-$([Guid]::NewGuid())"

try {
    $output = & $launcher -Action SelfTest -ControlDirectory $testDirectory -NoOpen | Out-String
    if ($output -notmatch 'Launcher self-test passed') {
        throw "Launcher self-test failed.`n$output"
    }

    $state = Get-Content -LiteralPath (Join-Path $testDirectory 'runtime-state.json') -Raw | ConvertFrom-Json
    if ($state.Mode -ne 'Public' -or $state.DashboardUrl -ne 'https://localhost:17239/test') {
        throw 'Launcher did not preserve public mode and dashboard URL.'
    }

    Write-Host 'Launcher behavior test passed.'
}
finally {
    if (Test-Path -LiteralPath $testDirectory) {
        Remove-Item -LiteralPath $testDirectory -Recurse -Force
    }
}
