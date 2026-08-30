[CmdletBinding()]
param(
    [ValidateSet('Start', 'Stop', 'Status', 'Menu', 'Configure', 'ResetSecrets', 'Restart', 'Open')]
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
$controlDirectory = Join-Path $workspace '.elyndor'
$secretPath = Join-Path $controlDirectory 'launcher-secrets.json'
$runtimeStatePath = Join-Path $controlDirectory 'runtime-state.json'

function Ensure-ControlDirectory {
    if (-not (Test-Path -LiteralPath $controlDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $controlDirectory | Out-Null
    }
}

function ConvertTo-ProtectedString {
    param([Parameter(Mandatory)][Security.SecureString]$Value)

    return ConvertFrom-SecureString -SecureString $Value
}

function ConvertFrom-ProtectedString {
    param([Parameter(Mandatory)][string]$Value)

    $secureValue = ConvertTo-SecureString -String $Value
    return [Net.NetworkCredential]::new('', $secureValue).Password
}

function Save-LauncherSecrets {
    param(
        [Parameter(Mandatory)][Security.SecureString]$BotToken,
        [Parameter(Mandatory)][Security.SecureString]$SigningKey
    )

    Ensure-ControlDirectory
    @{
        BotToken = ConvertTo-ProtectedString $BotToken
        SigningKey = ConvertTo-ProtectedString $SigningKey
    } | ConvertTo-Json | Set-Content -LiteralPath $secretPath -Encoding UTF8
}

function Get-LauncherSecrets {
    if (-not (Test-Path -LiteralPath $secretPath -PathType Leaf)) {
        return $null
    }

    try {
        $protected = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json
        return @{
            BotToken = ConvertFrom-ProtectedString $protected.BotToken
            SigningKey = ConvertFrom-ProtectedString $protected.SigningKey
        }
    }
    catch {
        throw 'Unable to read local secrets. Use menu option 4 to save the token again.'
    }
}

function Set-TelegramSecrets {
    Write-Host ''
    Write-Host 'Get a token from @BotFather with /newbot or /token.'
    $botToken = Read-Host 'Paste Telegram Bot Token (hidden input)' -AsSecureString
    $plainBotToken = [Net.NetworkCredential]::new('', $botToken).Password
    if ($plainBotToken -notmatch '^\d+:[A-Za-z0-9_-]{20,}$') {
        throw 'Telegram Bot Token format was not recognized. Nothing was saved.'
    }

    $signingBytes = [byte[]]::new(48)
    $randomNumberGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $randomNumberGenerator.GetBytes($signingBytes)
    }
    finally {
        $randomNumberGenerator.Dispose()
    }
    $plainSigningKey = [Convert]::ToBase64String($signingBytes)
    $signingKey = ConvertTo-SecureString -String $plainSigningKey -AsPlainText -Force

    try {
        Save-LauncherSecrets -BotToken $botToken -SigningKey $signingKey
    }
    finally {
        $plainBotToken = $null
        $plainSigningKey = $null
    }

    Write-Host 'Telegram Bot Token was encrypted for the current Windows user and saved locally.'
    Write-Host "File: $secretPath"
}

function Reset-LauncherSecrets {
    if (Test-Path -LiteralPath $secretPath -PathType Leaf) {
        Remove-Item -LiteralPath $secretPath -Force
        Write-Host 'Saved Telegram credentials were removed.'
        return
    }

    Write-Host 'No saved Telegram credentials were found.'
}

function Get-RuntimeMode {
    if (-not (Test-Path -LiteralPath $runtimeStatePath -PathType Leaf)) {
        return $null
    }

    try {
        return (Get-Content -LiteralPath $runtimeStatePath -Raw | ConvertFrom-Json).Mode
    }
    catch {
        return $null
    }
}

function Set-RuntimeMode {
    param([Parameter(Mandatory)][ValidateSet('Local', 'Public')][string]$Mode)

    Ensure-ControlDirectory
    @{ Mode = $Mode } | ConvertTo-Json | Set-Content -LiteralPath $runtimeStatePath -Encoding UTF8
}

function Clear-RuntimeMode {
    if (Test-Path -LiteralPath $runtimeStatePath -PathType Leaf) {
        Remove-Item -LiteralPath $runtimeStatePath -Force
    }
}

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

    Write-Host "API URL:    $localUrl"
    try {
        $response = Invoke-WebRequest -Uri "$localUrl/alive" -UseBasicParsing -TimeoutSec 3
        Write-Host "API health: HTTP $($response.StatusCode) $($response.Content)"
    }
    catch {
        Write-Host 'API health: offline'
    }

    $runtimeState = if (Test-AppHostRunning $aspire) { 'running' } else { 'offline' }
    Write-Host "Runtime:    $runtimeState"
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

    $requestedMode = if ($Public) { 'Public' } else { 'Local' }
    $runningMode = Get-RuntimeMode
    if ((Test-AppHostRunning $aspire) -and $runningMode -ne $requestedMode) {
        $displayMode = if ([string]::IsNullOrWhiteSpace($runningMode)) { 'unknown' } else { $runningMode }
        Write-Host "Switching Elyndor from $displayMode mode to $requestedMode mode..."
        Invoke-CheckedCommand $aspire @(
            'stop',
            '--apphost', $appHostProject,
            '--non-interactive')
    }

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

    Set-RuntimeMode $requestedMode

    Write-Host ''
    Write-Host 'Elyndor is running.'
    Write-Host "Local:  $localUrl"
    if ($Public) {
        Write-Host "Public: $launchUrl"
        Write-Host ''
        Write-Host 'Set the Public URL in @BotFather: /mybots -> Bot Settings -> Menu Button -> Configure menu button.'
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

    Clear-RuntimeMode

    Write-Host 'Elyndor runtime stopped.'
}

function Invoke-PublicStart {
    $secrets = Get-LauncherSecrets
    if ($null -eq $secrets) {
        Write-Host 'A Telegram Bot Token must be configured first.'
        Set-TelegramSecrets
        $secrets = Get-LauncherSecrets
    }

    $previousSigningKey = [Environment]::GetEnvironmentVariable('Authentication__SigningKey', 'Process')
    $previousBotToken = [Environment]::GetEnvironmentVariable('Authentication__Telegram__BotToken', 'Process')
    try {
        [Environment]::SetEnvironmentVariable('Authentication__SigningKey', $secrets.SigningKey, 'Process')
        [Environment]::SetEnvironmentVariable('Authentication__Telegram__BotToken', $secrets.BotToken, 'Process')
        $script:Public = $true
        Start-Elyndor
    }
    finally {
        [Environment]::SetEnvironmentVariable('Authentication__SigningKey', $previousSigningKey, 'Process')
        [Environment]::SetEnvironmentVariable('Authentication__Telegram__BotToken', $previousBotToken, 'Process')
        $secrets = $null
        $script:Public = $false
    }
}

function Restart-Elyndor {
    Stop-Elyndor
    Invoke-PublicStart
}

function Open-Elyndor {
    $tailscaleCommand = Get-Command 'tailscale' -ErrorAction SilentlyContinue
    if ($null -ne $tailscaleCommand) {
        $funnelUrl = Get-FunnelUrl $tailscaleCommand.Source
        if ($null -ne $funnelUrl) {
            Start-Process $funnelUrl | Out-Null
            return
        }
    }

    Start-Process $localUrl | Out-Null
}

function Show-ControlMenu {
    while ($true) {
        Clear-Host
        Write-Host '========================================'
        Write-Host '          ELYNDOR CONTROL CENTER'
        Write-Host '========================================'
        Write-Host '1. Start Elyndor in Telegram through Tailscale'
        Write-Host '2. Restart Elyndor'
        Write-Host '3. Show status and public URL'
        Write-Host '4. Configure Telegram Bot Token'
        Write-Host '5. Open the game'
        Write-Host '6. Stop Elyndor and Tailscale Funnel'
        Write-Host '7. Remove saved Telegram credentials'
        Write-Host '0. Exit control center'
        Write-Host ''
        $selection = Read-Host 'Select an action'
        if ($null -eq $selection) {
            return
        }
        $selection = $selection.Trim()

        try {
            switch ($selection) {
                '1' { $script:Open = $true; Invoke-PublicStart }
                '2' { $script:Open = $true; Restart-Elyndor }
                '3' { Show-Status }
                '4' { Set-TelegramSecrets }
                '5' { Open-Elyndor }
                '6' { Stop-Elyndor }
                '7' { Reset-LauncherSecrets }
                '0' { return }
                default { Write-Host 'Unknown menu option.' }
            }
        }
        catch {
            Write-Host ''
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }

        Write-Host ''
        Read-Host 'Press Enter to return to the menu' | Out-Null
    }
}

switch ($Action) {
    'Start' { Start-Elyndor }
    'Stop' { Stop-Elyndor }
    'Status' { Show-Status }
    'Menu' { Show-ControlMenu }
    'Configure' { Set-TelegramSecrets }
    'ResetSecrets' { Reset-LauncherSecrets }
    'Restart' { Restart-Elyndor }
    'Open' { Open-Elyndor }
}
