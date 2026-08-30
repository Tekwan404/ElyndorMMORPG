[CmdletBinding()]
param(
    [ValidateSet('Start', 'Stop', 'Status')]
    [string]$Action = 'Start',
    [switch]$Public,
    [switch]$Open,
    [switch]$KeepFunnel,
    [ValidateRange(1, [long]::MaxValue)]
    [long]$DevelopmentTelegramUserId = 1000001
)

$ErrorActionPreference = 'Stop'

$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$appHostProject = Join-Path $workspace 'apphost\Elyndor.AppHost\Elyndor.AppHost.csproj'
$frontendDirectory = Join-Path $workspace 'web\elyndor-web'
$localUrl = 'http://127.0.0.1:5080'

function Resolve-Executable {
    param([Parameter(Mandatory)][string]$Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "Required executable '$Name' was not found in PATH."
    }

    return $command.Source
}

function Resolve-AspireCli {
    $command = Get-Command 'aspire' -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $packageRoot = Join-Path $env:USERPROFILE '.nuget\packages\aspire.cli.win-x64'
    if (Test-Path -LiteralPath $packageRoot -PathType Container) {
        $candidate = Get-ChildItem -LiteralPath $packageRoot -Directory |
            Sort-Object LastWriteTimeUtc -Descending |
            ForEach-Object {
                Get-ChildItem -LiteralPath $_.FullName -Filter 'aspire.exe' -File -Recurse |
                    Select-Object -First 1
            } |
            Where-Object { $null -ne $_ } |
            Select-Object -First 1

        if ($null -ne $candidate) {
            return $candidate.FullName
        }
    }

    throw "Aspire CLI was not found. Run 'dotnet restore $appHostProject' and retry."
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

function Test-DockerReady {
    param([Parameter(Mandatory)][string]$DockerPath)

    & $DockerPath info --format '{{.ServerVersion}}' *> $null
    return $LASTEXITCODE -eq 0
}

function Ensure-DockerReady {
    param([Parameter(Mandatory)][string]$DockerPath)

    if (Test-DockerReady $DockerPath) {
        return
    }

    $dockerDesktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if (-not (Test-Path -LiteralPath $dockerDesktop -PathType Leaf)) {
        throw 'Docker daemon is not running and Docker Desktop was not found.'
    }

    Write-Host 'Starting Docker Desktop...'
    Start-Process -FilePath $dockerDesktop -WindowStyle Hidden | Out-Null

    $deadline = [DateTimeOffset]::UtcNow.AddMinutes(2)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-DockerReady $DockerPath) {
            return
        }

        Start-Sleep -Seconds 2
    }

    throw 'Docker daemon did not become ready within two minutes.'
}

function Test-AppHostRunning {
    param([Parameter(Mandatory)][string]$AspirePath)

    $output = (& $AspirePath ps --format Json --non-interactive 2>$null | Out-String)
    return $LASTEXITCODE -eq 0 -and $output.Contains('Elyndor.AppHost.csproj')
}

function Get-FunnelUrl {
    param([Parameter(Mandatory)][string]$TailscalePath)

    $json = (& $TailscalePath funnel status --json | Out-String)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        return $null
    }

    $status = $json | ConvertFrom-Json
    $property = $status.AllowFunnel.PSObject.Properties |
        Where-Object { $_.Value -eq $true } |
        Select-Object -First 1

    if ($null -eq $property) {
        return $null
    }

    $hostName = $property.Name -replace ':443$', ''
    return "https://$hostName"
}

function Show-Status {
    $aspire = Resolve-AspireCli

    Write-Host "Local URL:  $localUrl"
    try {
        $response = Invoke-WebRequest -Uri "$localUrl/alive" -UseBasicParsing -TimeoutSec 3
        Write-Host "Local state: HTTP $($response.StatusCode) $($response.Content)"
    }
    catch {
        Write-Host 'Local state: offline'
    }

    & $aspire ps --non-interactive
    $tailscaleCommand = Get-Command 'tailscale' -ErrorAction SilentlyContinue
    if ($null -eq $tailscaleCommand) {
        Write-Host 'Funnel:     unavailable (Tailscale is not installed)'
        return
    }

    $tailscale = $tailscaleCommand.Source
    $funnelUrl = Get-FunnelUrl $tailscale
    if ($null -eq $funnelUrl) {
        Write-Host 'Funnel:     off'
    }
    else {
        Write-Host "Funnel:     $funnelUrl"
        & $tailscale funnel status
    }
}

function Start-Elyndor {
    $dotnet = Resolve-Executable 'dotnet'
    $npm = Resolve-Executable 'npm'
    $docker = Resolve-Executable 'docker'
    $tailscale = if ($Public) { Resolve-Executable 'tailscale' } else { $null }

    Ensure-DockerReady $docker

    if (-not (Test-Path -LiteralPath (Join-Path $frontendDirectory 'node_modules') -PathType Container)) {
        Invoke-CheckedCommand $npm @('ci', '--prefix', $frontendDirectory)
    }

    Invoke-CheckedCommand $npm @('run', 'build', '--prefix', $frontendDirectory)

    if ($Public) {
        $signingKey = [Environment]::GetEnvironmentVariable(
            'Authentication__SigningKey',
            'Process')
        $botToken = [Environment]::GetEnvironmentVariable(
            'Authentication__Telegram__BotToken',
            'Process')
        if ([string]::IsNullOrWhiteSpace($signingKey) -or $signingKey.Length -lt 32) {
            throw 'Public mode requires Authentication__SigningKey (at least 32 characters) in the process environment.'
        }
        if ([string]::IsNullOrWhiteSpace($botToken)) {
            throw 'Public mode requires Authentication__Telegram__BotToken in the process environment.'
        }
    }

    Invoke-CheckedCommand $dotnet @('restore', $appHostProject)
    $aspire = Resolve-AspireCli

    if (-not (Test-AppHostRunning $aspire)) {
        $previousPublicTest = [Environment]::GetEnvironmentVariable('Elyndor__PublicTest', 'Process')
        $previousDevelopmentUserId = [Environment]::GetEnvironmentVariable(
            'Elyndor__DevelopmentTelegramUserId',
            'Process')
        try {
            if ($Public) {
                [Environment]::SetEnvironmentVariable('Elyndor__PublicTest', 'true', 'Process')
            }
            else {
                [Environment]::SetEnvironmentVariable('Elyndor__PublicTest', $null, 'Process')
                [Environment]::SetEnvironmentVariable(
                    'Elyndor__DevelopmentTelegramUserId',
                    $DevelopmentTelegramUserId.ToString(
                        [System.Globalization.CultureInfo]::InvariantCulture),
                    'Process')
            }

            Invoke-CheckedCommand $aspire @(
                'start',
                '--apphost', $appHostProject,
                '--format', 'Json',
                '--non-interactive')
        }
        finally {
            [Environment]::SetEnvironmentVariable(
                'Elyndor__PublicTest',
                $previousPublicTest,
                'Process')
            [Environment]::SetEnvironmentVariable(
                'Elyndor__DevelopmentTelegramUserId',
                $previousDevelopmentUserId,
                'Process')
        }
    }

    foreach ($resource in @('postgres', 'game', 'server')) {
        Invoke-CheckedCommand $aspire @(
            'wait', $resource,
            '--status', 'healthy',
            '--timeout', '180',
            '--apphost', $appHostProject,
            '--non-interactive')
    }

    $response = Invoke-WebRequest -Uri "$localUrl/alive" -UseBasicParsing -TimeoutSec 10
    if ($response.StatusCode -ne 200 -or $response.Content -ne 'Healthy') {
        throw "Elyndor aliveness check failed at $localUrl/alive."
    }

    $launchUrl = $localUrl
    if ($Public) {
        Invoke-CheckedCommand $tailscale @('funnel', '--bg', '--yes', '5080')
        $launchUrl = Get-FunnelUrl $tailscale
        if ($null -eq $launchUrl) {
            throw 'Tailscale Funnel did not report a public HTTPS URL.'
        }
    }

    Write-Host ''
    Write-Host 'Elyndor is running.'
    Write-Host "Local:  $localUrl"
    if ($Public) {
        Write-Host "Public: $launchUrl"
    }

    if ($Open) {
        Start-Process $launchUrl | Out-Null
    }
}

function Stop-Elyndor {
    $aspire = Resolve-AspireCli

    if (-not $KeepFunnel) {
        $tailscaleCommand = Get-Command 'tailscale' -ErrorAction SilentlyContinue
        if ($null -ne $tailscaleCommand) {
            $funnelUrl = Get-FunnelUrl $tailscaleCommand.Source
            if ($null -ne $funnelUrl) {
                Invoke-CheckedCommand $tailscaleCommand.Source @(
                    'funnel', '--https=443', '--yes', 'off')
            }
        }
    }

    if (Test-AppHostRunning $aspire) {
        Invoke-CheckedCommand $aspire @(
            'stop',
            '--apphost', $appHostProject,
            '--non-interactive')
    }

    Write-Host 'Elyndor runtime stopped.'
}

switch ($Action) {
    'Start' { Start-Elyndor }
    'Stop' { Stop-Elyndor }
    'Status' { Show-Status }
}
