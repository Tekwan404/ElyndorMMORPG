[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$launcher = Join-Path $PSScriptRoot 'Elyndor.ps1'
$frontendDirectory = Join-Path $workspace 'web\elyndor-web'
$developmentTelegramUserId = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()

try {
    & $launcher -Action Stop
    & $launcher -Action Start -Development -NoOpen -DevelopmentTelegramUserId $developmentTelegramUserId

    $previousBaseUrl = [Environment]::GetEnvironmentVariable('ELYNDOR_E2E_BASE_URL', 'Process')
    $previousRealRuntime = [Environment]::GetEnvironmentVariable('ELYNDOR_E2E_REAL', 'Process')
    try {
        [Environment]::SetEnvironmentVariable(
            'ELYNDOR_E2E_BASE_URL',
            'http://127.0.0.1:5080',
            'Process')
        [Environment]::SetEnvironmentVariable('ELYNDOR_E2E_REAL', 'true', 'Process')
        & npm run test:e2e --prefix $frontendDirectory -- --reporter=line
        if ($LASTEXITCODE -ne 0) {
            throw "Playwright failed with exit code $LASTEXITCODE."
        }

        & $launcher -Action Stop
        & $launcher -Action Start -Development -NoOpen -DevelopmentTelegramUserId $developmentTelegramUserId
        $authentication = Invoke-RestMethod `
            -Uri 'http://127.0.0.1:5080/api/v1/auth/development' `
            -Method Post `
            -ContentType 'application/json' `
            -Body '{}'
        $headers = @{ Authorization = "Bearer $($authentication.accessToken)" }
        $snapshot = Invoke-RestMethod `
            -Uri 'http://127.0.0.1:5080/api/v1/bootstrap' `
            -Headers $headers
        if ($snapshot.world.currentLocation.id -ne 'DEEP_FOREST') {
            throw 'Restart verification did not restore the authoritative Deep Forest location.'
        }
        Write-Host 'Restart verification restored the character in Deep Forest.'
    }
    finally {
        [Environment]::SetEnvironmentVariable('ELYNDOR_E2E_BASE_URL', $previousBaseUrl, 'Process')
        [Environment]::SetEnvironmentVariable('ELYNDOR_E2E_REAL', $previousRealRuntime, 'Process')
    }
}
finally {
    & $launcher -Action Stop
}
