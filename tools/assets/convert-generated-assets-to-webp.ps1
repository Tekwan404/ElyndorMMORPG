[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [ValidateRange(1, 100)]
    [int]$Quality = 85
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-DirectoryToWebp([string]$directory) {
    $files = @(Get-ChildItem -LiteralPath $directory -Filter '*.png' -File)
    if ($files.Count -eq 0) { return 0 }

    $inputPaths = @($files | ForEach-Object { $_.FullName })
    & npx --yes sharp-cli -i $inputPaths -o $directory -f webp -q $Quality | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "sharp-cli failed for $directory with exit code $LASTEXITCODE"
    }

    $converted = 0
    foreach ($file in $files) {
        $webpPath = Join-Path $directory ($file.BaseName + '.webp')
        if (-not (Test-Path -LiteralPath $webpPath)) {
            throw "Expected WebP output is missing: $webpPath"
        }
        [System.IO.File]::Delete($file.FullName)
        $converted++
    }
    return $converted
}

$playerAssetRoot = Join-Path $RepoRoot 'web\elyndor-web\src\assets'
$adminAssetRoot = Join-Path $RepoRoot 'web\elyndor-admin\src\assets\admin'
$directories = @(
    @(Get-ChildItem -LiteralPath (Join-Path $playerAssetRoot 'game\talents') -Directory | ForEach-Object { $_.FullName })
    (Join-Path $playerAssetRoot 'items')
    (Join-Path $playerAssetRoot 'items\sets')
    (Join-Path $playerAssetRoot 'characters\personal')
    $adminAssetRoot
)

$convertedCount = 0
foreach ($directory in $directories) {
    if (Test-Path -LiteralPath $directory) {
        $convertedCount += Convert-DirectoryToWebp $directory
    }
}

$manifestPath = Join-Path $RepoRoot 'tools\assets\asset-manifest.json'
if (Test-Path -LiteralPath $manifestPath) {
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $manifest = [System.IO.File]::ReadAllText($manifestPath)
    $manifest = [regex]::Replace($manifest, '(\"output\":\s*\"[^\"]*)\.png(?=\")', '$1.webp')
    [System.IO.File]::WriteAllText($manifestPath, $manifest, $utf8)
}

Write-Host "Converted $convertedCount generated PNG files to WebP at quality $Quality."
