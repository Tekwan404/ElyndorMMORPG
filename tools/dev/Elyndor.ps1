[CmdletBinding()]
param(
    [ValidateSet('Start', 'Stop', 'Status', 'Menu', 'Configure', 'ConfigureAdmin', 'ResetSecrets', 'Restart', 'Dashboard', 'Game', 'Open', 'SelfTest')]
    [string]$Action = 'Start',
    [switch]$Public,
    [switch]$Open,
    [switch]$KeepFunnel,
    [switch]$NoOpen,
    [switch]$Development,
    [string]$ControlDirectory,
    [ValidateRange(1, [long]::MaxValue)]
    [long]$DevelopmentTelegramUserId = 1000001,
    [long]$AdminTelegramUserId = 0
)

$ErrorActionPreference = 'Stop'

$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$appHostProject = Join-Path $workspace 'apphost\Elyndor.AppHost\Elyndor.AppHost.csproj'
$frontendDirectory = Join-Path $workspace 'web\elyndor-web'
$localUrl = 'http://127.0.0.1:5080'
$controlDirectory = if ([string]::IsNullOrWhiteSpace($ControlDirectory)) {
    Join-Path $workspace '.elyndor'
} else {
    [IO.Path]::GetFullPath($ControlDirectory)
}
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
        [Parameter(Mandatory)][Security.SecureString]$SigningKey,
        [Parameter(Mandatory)][Security.SecureString]$WebhookSecret,
        [Parameter(Mandatory)][long]$AdministratorTelegramUserId
    )

    Ensure-ControlDirectory
    @{
        BotToken = ConvertTo-ProtectedString $BotToken
        SigningKey = ConvertTo-ProtectedString $SigningKey
        WebhookSecret = ConvertTo-ProtectedString $WebhookSecret
        AdministratorTelegramUserId = $AdministratorTelegramUserId
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
            WebhookSecret = if ($null -ne $protected.WebhookSecret) {
                ConvertFrom-ProtectedString $protected.WebhookSecret
            } else { '' }
            AdministratorTelegramUserId = if ($null -ne $protected.AdministratorTelegramUserId) {
                [long]$protected.AdministratorTelegramUserId
            } else { 0 }
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
    $configuredAdminId = $AdminTelegramUserId
    if ($configuredAdminId -le 0) {
        $configuredAdminId = [long](Read-Host 'Your Telegram numeric user ID')
    }
    if ($configuredAdminId -le 0) {
        throw 'Administrator Telegram user ID must be positive.'
    }
    $plainWebhookSecret = New-RandomSecret
    $webhookSecret = ConvertTo-SecureString -String $plainWebhookSecret -AsPlainText -Force

    try {
        Save-LauncherSecrets `
            -BotToken $botToken `
            -SigningKey $signingKey `
            -WebhookSecret $webhookSecret `
            -AdministratorTelegramUserId $configuredAdminId
    }
    finally {
        $plainBotToken = $null
        $plainSigningKey = $null
        $plainWebhookSecret = $null
    }

    Write-Host 'Telegram Bot Token was encrypted for the current Windows user and saved locally.'
    Write-Host "File: $secretPath"
}

function New-RandomSecret {
    $bytes = [byte[]]::new(48)
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }
    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Set-TelegramAdminConfiguration {
    $secrets = Get-LauncherSecrets
    if ($null -eq $secrets) {
        throw 'Configure the Telegram Bot Token first.'
    }

    $configuredAdminId = $AdminTelegramUserId
    if ($configuredAdminId -le 0) {
        $configuredAdminId = [long](Read-Host 'Your Telegram numeric user ID')
    }
    if ($configuredAdminId -le 0) {
        throw 'Administrator Telegram user ID must be positive.'
    }

    $botToken = ConvertTo-SecureString -String $secrets.BotToken -AsPlainText -Force
    $signingKey = ConvertTo-SecureString -String $secrets.SigningKey -AsPlainText -Force
    $plainWebhookSecret = New-RandomSecret
    $webhookSecret = ConvertTo-SecureString -String $plainWebhookSecret -AsPlainText -Force
    Save-LauncherSecrets `
        -BotToken $botToken `
        -SigningKey $signingKey `
        -WebhookSecret $webhookSecret `
        -AdministratorTelegramUserId $configuredAdminId
    $secrets = $null
    $plainWebhookSecret = $null
    Write-Host 'Telegram administrator and webhook secret were saved locally.'
}

function Reset-LauncherSecrets {
    if (Test-Path -LiteralPath $secretPath -PathType Leaf) {
        Remove-Item -LiteralPath $secretPath -Force
        Write-Host 'Saved Telegram credentials were removed.'
        return
    }

    Write-Host 'No saved Telegram credentials were found.'
}

function Get-RuntimeState {
    if (-not (Test-Path -LiteralPath $runtimeStatePath -PathType Leaf)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $runtimeStatePath -Raw | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Get-RuntimeMode {
    $state = Get-RuntimeState
    if ($null -eq $state) { return $null }
    return $state.Mode
}

function Set-RuntimeState {
    param(
        [Parameter(Mandatory)][ValidateSet('Local', 'Public')][string]$Mode,
        [string]$DashboardUrl,
        [string]$PublicUrl
    )

    Ensure-ControlDirectory
    @{
        Mode = $Mode
        DashboardUrl = $DashboardUrl
        PublicUrl = $PublicUrl
        StartedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    } | ConvertTo-Json | Set-Content -LiteralPath $runtimeStatePath -Encoding UTF8
}

function Clear-RuntimeState {
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

function Set-TelegramWebhook {
    param(
        [Parameter(Mandatory)][string]$BotToken,
        [Parameter(Mandatory)][string]$WebhookSecret,
        [Parameter(Mandatory)][string]$PublicUrl
    )

    $response = Invoke-RestMethod `
        -Method Post `
        -Uri "https://api.telegram.org/bot$BotToken/setWebhook" `
        -Body @{
            url = "$PublicUrl/api/v1/administration/telegram/webhook"
            secret_token = $WebhookSecret
            allowed_updates = '["message"]'
            drop_pending_updates = 'true'
        } `
        -TimeoutSec 20
    if (-not $response.ok) {
        throw 'Telegram rejected webhook registration.'
    }
}

function Remove-TelegramWebhook {
    param([Parameter(Mandatory)][string]$BotToken)

    $response = Invoke-RestMethod `
        -Method Post `
        -Uri "https://api.telegram.org/bot$BotToken/deleteWebhook" `
        -Body @{ drop_pending_updates = 'true' } `
        -TimeoutSec 20
    if (-not $response.ok) {
        throw 'Telegram rejected webhook removal.'
    }
}

function Show-Status {
    $aspire = Resolve-AspireCli
    $application = Get-AspireApplication $aspire
    $runtimeState = Get-RuntimeState
    $resources = if ($null -ne $application) { Get-AspireResources $aspire } else { @() }
    $server = $resources | Where-Object { $_.displayName -eq 'server' } | Select-Object -First 1
    $database = $resources | Where-Object { $_.displayName -eq 'game' } | Select-Object -First 1
    $postgres = $resources | Where-Object { $_.displayName -eq 'postgres' } | Select-Object -First 1
    Write-Host ''
    Write-Host '================ ELYNDOR STATUS ================' -ForegroundColor Cyan
    Write-Host "Runtime:      $(if ($null -ne $application) { 'RUNNING' } else { 'OFFLINE' })"
    Write-Host "Mode:         $(if ($null -ne $runtimeState) { $runtimeState.Mode } else { 'n/a' })"
    Write-Host "Server:       $(if ($null -ne $server) { "$($server.state) / $($server.healthStatus)" } else { 'offline' })"
    Write-Host "PostgreSQL:   $(if ($null -ne $postgres) { "$($postgres.state) / $($postgres.healthStatus)" } else { 'offline' })"
    Write-Host "Game DB:      $(if ($null -ne $database) { "$($database.state) / $($database.healthStatus)" } else { 'offline' })"
    Write-Host "API:          $localUrl"
    try {
        $response = Invoke-WebRequest -Uri "$localUrl/alive" -UseBasicParsing -TimeoutSec 3
        Write-Host "API health:   HTTP $($response.StatusCode) $($response.Content)"
    }
    catch {
        Write-Host 'API health:   offline'
    }
    if ($null -ne $server) { Write-Host "Server load:  $(Get-ProcessLoadText ([int]$server.properties.'executable.pid'))" }
    $dockerCommand = Get-Command 'docker' -ErrorAction SilentlyContinue
    if ($null -ne $postgres -and $null -ne $dockerCommand) {
        Write-Host "DB load:      $(Get-ContainerLoadText $dockerCommand.Source $postgres.properties.'container.id')"
    }
    $dashboardUrl = if ($null -ne $application) { $application.dashboardUrl } else { $runtimeState.DashboardUrl }
    Write-Host "Dashboard:    $(if ($dashboardUrl) { $dashboardUrl } else { 'offline' })"
    $tailscaleCommand = Get-Command 'tailscale' -ErrorAction SilentlyContinue
    if ($null -eq $tailscaleCommand) {
        Write-Host 'Funnel:       unavailable'
        return
    }

    $tailscale = $tailscaleCommand.Source
    $funnelUrl = Get-FunnelUrl $tailscale
    if ($null -eq $funnelUrl) {
        Write-Host 'Funnel:       off'
    }
    else {
        Write-Host "Public game:  $funnelUrl"
    }
    Write-Host '==================================================' -ForegroundColor Cyan
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
        $webhookSecret = [Environment]::GetEnvironmentVariable(
            'Administration__Telegram__WebhookSecret',
            'Process')
        $administratorId = [Environment]::GetEnvironmentVariable(
            'Administration__Telegram__AllowedUserIds__0',
            'Process')
        if ([string]::IsNullOrWhiteSpace($webhookSecret) -or $webhookSecret.Length -lt 32) {
            throw 'Public mode requires a configured Telegram admin webhook secret.'
        }
        if ([string]::IsNullOrWhiteSpace($administratorId)) {
            throw 'Public mode requires a configured Telegram administrator ID.'
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
        Set-TelegramWebhook `
            -BotToken $botToken `
            -WebhookSecret $webhookSecret `
            -PublicUrl $launchUrl
    }

    $application = Get-AspireApplication $aspire
    $dashboardUrl = if ($null -ne $application) { $application.dashboardUrl } else { $null }
    Set-RuntimeState `
        -Mode $requestedMode `
        -DashboardUrl $dashboardUrl `
        -PublicUrl $(if ($Public) { $launchUrl } else { $null })

    Write-Host ''
    Write-Host 'Elyndor is running.'
    Write-Host "Local:  $localUrl"
    if ($Public) {
        Write-Host "Public: $launchUrl"
        Write-Host ''
        Write-Host 'Set the Public URL in @BotFather: /mybots -> Bot Settings -> Menu Button -> Configure menu button.'
    }

    if (-not $NoOpen -and -not [string]::IsNullOrWhiteSpace($dashboardUrl)) {
        Write-Host "Dashboard: $dashboardUrl"
        Start-Process -FilePath $dashboardUrl | Out-Null
    }
}

function Stop-Elyndor {
    $aspire = Resolve-AspireCli

    if (-not $KeepFunnel) {
        $secrets = Get-LauncherSecrets
        if ($null -ne $secrets -and -not [string]::IsNullOrWhiteSpace($secrets.BotToken)) {
            try {
                Remove-TelegramWebhook -BotToken $secrets.BotToken
            }
            catch {
                Write-Warning "Unable to remove Telegram webhook: $($_.Exception.Message)"
            }
            finally {
                $secrets = $null
            }
        }
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

    Clear-RuntimeState

    Write-Host 'Elyndor runtime stopped.'
}

function Invoke-PublicStart {
    $secrets = Get-LauncherSecrets
    if ($null -eq $secrets) {
        Write-Host 'A Telegram Bot Token must be configured first.'
        Set-TelegramSecrets
        $secrets = Get-LauncherSecrets
    }
    if ([string]::IsNullOrWhiteSpace($secrets.WebhookSecret) -or $secrets.AdministratorTelegramUserId -le 0) {
        throw 'Telegram admin is not configured. Run ConfigureAdmin once.'
    }

    $previousSigningKey = [Environment]::GetEnvironmentVariable('Authentication__SigningKey', 'Process')
    $previousBotToken = [Environment]::GetEnvironmentVariable('Authentication__Telegram__BotToken', 'Process')
    $previousWebhookSecret = [Environment]::GetEnvironmentVariable('Administration__Telegram__WebhookSecret', 'Process')
    $previousAdminId = [Environment]::GetEnvironmentVariable('Administration__Telegram__AllowedUserIds__0', 'Process')
    try {
        [Environment]::SetEnvironmentVariable('Authentication__SigningKey', $secrets.SigningKey, 'Process')
        [Environment]::SetEnvironmentVariable('Authentication__Telegram__BotToken', $secrets.BotToken, 'Process')
        [Environment]::SetEnvironmentVariable('Administration__Telegram__WebhookSecret', $secrets.WebhookSecret, 'Process')
        [Environment]::SetEnvironmentVariable(
            'Administration__Telegram__AllowedUserIds__0',
            $secrets.AdministratorTelegramUserId.ToString([System.Globalization.CultureInfo]::InvariantCulture),
            'Process')
        $script:Public = $true
        Start-Elyndor
    }
    finally {
        [Environment]::SetEnvironmentVariable('Authentication__SigningKey', $previousSigningKey, 'Process')
        [Environment]::SetEnvironmentVariable('Authentication__Telegram__BotToken', $previousBotToken, 'Process')
        [Environment]::SetEnvironmentVariable('Administration__Telegram__WebhookSecret', $previousWebhookSecret, 'Process')
        [Environment]::SetEnvironmentVariable('Administration__Telegram__AllowedUserIds__0', $previousAdminId, 'Process')
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
        Write-Host '╔══════════════════════════════════════════╗' -ForegroundColor Cyan
        Write-Host '║          ELYNDOR CONTROL CENTER          ║' -ForegroundColor Cyan
        Write-Host '╚══════════════════════════════════════════╝' -ForegroundColor Cyan
        Write-Host ' 1. Запустить игру + Tailscale + мониторинг'
        Write-Host ' 2. Перезапустить всё'
        Write-Host ' 3. Открыть Aspire Dashboard'
        Write-Host ' 4. Показать статус и нагрузку'
        Write-Host ' 5. Открыть игру в браузере'
        Write-Host ' 6. Настроить Telegram Bot Token'
        Write-Host ' 7. Настроить Telegram администратора'
        Write-Host ' 8. Выключить игру и Tailscale Funnel'
        Write-Host ' 9. Удалить сохранённые credentials'
        Write-Host ' 0. Закрыть панель'
        Write-Host ''
        $selection = Read-Host 'Select an action'
        if ($null -eq $selection) {
            return
        }
        $selection = $selection.Trim()

        try {
            switch ($selection) {
                '1' { Invoke-PublicStart }
                '2' { Restart-Elyndor }
                '3' { Open-Dashboard }
                '4' { Show-Status }
                '5' { Open-Elyndor }
                '6' { Set-TelegramSecrets }
                '7' { Set-TelegramAdminConfiguration }
                '8' { Stop-Elyndor }
                '9' { Reset-LauncherSecrets }
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

function Open-Dashboard {
    $aspire = Resolve-AspireCli
    $application = Get-AspireApplication $aspire
    $state = Get-RuntimeState
    $dashboardUrl = if ($null -ne $application) { $application.dashboardUrl } else { $state.DashboardUrl }
    if ([string]::IsNullOrWhiteSpace($dashboardUrl)) {
        throw 'Aspire Dashboard is unavailable. Start Elyndor first.'
    }
    Start-Process -FilePath $dashboardUrl | Out-Null
    Write-Host "Aspire Dashboard opened: $dashboardUrl"
}

function Get-AspireApplication {
    param([Parameter(Mandatory)][string]$AspirePath)
    $json = (& $AspirePath ps --format Json --non-interactive 2>$null | Out-String)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) { return $null }
    return @($json | ConvertFrom-Json) |
        Where-Object { $_.appHostPath -eq $appHostProject } |
        Select-Object -First 1
}

function Get-AspireResources {
    param([Parameter(Mandatory)][string]$AspirePath)
    $json = (& $AspirePath describe --apphost $appHostProject --format Json --non-interactive 2>$null | Out-String)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) { return @() }
    return @(($json | ConvertFrom-Json).resources)
}

function Format-Bytes {
    param([long]$Bytes)
    if ($Bytes -ge 1GB) { return '{0:N1} GB' -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return '{0:N0} MB' -f ($Bytes / 1MB) }
    return '{0:N0} KB' -f ($Bytes / 1KB)
}

function Get-ProcessLoadText {
    param([int]$ProcessId)
    if ($ProcessId -le 0) { return 'n/a' }
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -eq $process) { return 'n/a' }
    return "RAM $(Format-Bytes $process.WorkingSet64), CPU $([Math]::Round($process.CPU, 1))s"
}

function Get-ContainerLoadText {
    param([Parameter(Mandatory)][string]$DockerPath, [string]$ContainerId)
    if ([string]::IsNullOrWhiteSpace($ContainerId)) { return 'n/a' }
    $stats = (& $DockerPath stats $ContainerId --no-stream --format '{{.CPUPerc}}|{{.MemUsage}}' 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -eq 0 -and $stats) { return $stats -replace '\|', ', RAM ' }
    return 'n/a'
}

function Invoke-LauncherSelfTest {
    Set-RuntimeState `
        -Mode Public `
        -DashboardUrl 'https://localhost:17239/test' `
        -PublicUrl 'https://elyndor.test'
    $state = Get-RuntimeState
    if ($state.Mode -ne 'Public' -or $state.DashboardUrl -ne 'https://localhost:17239/test') {
        throw 'Runtime state round-trip failed.'
    }
    Write-Output 'Launcher self-test passed.'
}

switch ($Action) {
    'Start' { if ($Development) { Start-Elyndor } else { Invoke-PublicStart } }
    'Stop' { Stop-Elyndor }
    'Status' { Show-Status }
    'Menu' { Show-ControlMenu }
    'Configure' { Set-TelegramSecrets }
    'ConfigureAdmin' { Set-TelegramAdminConfiguration }
    'ResetSecrets' { Reset-LauncherSecrets }
    'Restart' { Restart-Elyndor }
    'Dashboard' { Open-Dashboard }
    'Game' { Open-Elyndor }
    'Open' { Open-Elyndor }
    'SelfTest' { Invoke-LauncherSelfTest }
}
