[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$manifestPath = Join-Path $RepoRoot 'tools\assets\asset-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Asset manifest is missing. Run tools/assets/slice-pic-assets.ps1 first."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$missing = [System.Collections.Generic.List[string]]::new()

function Test-ManifestOutputs([object]$entries) {
    foreach ($entry in @($entries)) {
        $relativePath = ([string]$entry.output) -replace '^[.][\\/]', ''
        $fullPath = Join-Path $RepoRoot $relativePath
        if (-not (Test-Path -LiteralPath $fullPath)) {
            $missing.Add($relativePath)
        }
    }
}

Test-ManifestOutputs $manifest.talentIcons
Test-ManifestOutputs $manifest.itemSheetIcons
Test-ManifestOutputs $manifest.copiedItemIcons
Test-ManifestOutputs $manifest.fallbackItemIcons
Test-ManifestOutputs $manifest.characterArt
Test-ManifestOutputs $manifest.adminArt

$playerAssetRoot = Join-Path $RepoRoot 'web\elyndor-web\src\assets'
$playerAdminFiles = @(Get-ChildItem -LiteralPath $playerAssetRoot -File -Recurse | Where-Object { $_.FullName -match '[\\/]admin([\\/]|[-])' })
if ($playerAdminFiles.Count -gt 0) {
    throw "Admin artwork leaked into player assets: $($playerAdminFiles.FullName -join ', ')"
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'content') -Filter '*.json' -File -Recurse) {
    $document = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
    if ($document.PSObject.Properties.Name -contains 'talentTrees') {
        foreach ($tree in @($document.talentTrees)) {
            foreach ($node in @($tree.nodes)) {
                if ($node.PSObject.Properties.Name -notcontains 'iconId') { continue }
                if ([string]::IsNullOrWhiteSpace($node.iconId)) { continue }
                $talentSlug = $node.iconId.ToLowerInvariant() -replace '_', '-'
                $talentFile = Get-ChildItem -LiteralPath (Join-Path $playerAssetRoot 'game\talents') -File -Recurse | Where-Object { $_.BaseName -eq $talentSlug } | Select-Object -First 1
                if ($null -eq $talentFile) {
                    $missing.Add(([string]$node.iconId) + ' (talent content mapping)')
                }
            }
        }
    }

    if ($document.PSObject.Properties.Name -contains 'items') {
        foreach ($item in @($document.items)) {
            if ($item.PSObject.Properties.Name -notcontains 'iconId') { continue }
            if ([string]::IsNullOrWhiteSpace($item.iconId)) { continue }
            $itemFile = Get-ChildItem -LiteralPath $playerAssetRoot -File -Recurse | Where-Object { $_.BaseName -eq $item.iconId } | Select-Object -First 1
            if ($null -eq $itemFile) {
                $missing.Add(([string]$item.iconId) + ' (item content mapping)')
            }
        }
    }
}

if ($missing.Count -gt 0) {
    throw "Missing generated asset files: $($missing -join ', ')"
}

Write-Host "Asset manifest is valid: $($manifest.talentIcons.Count) talent icons, $($manifest.itemSheetIcons.Count) set icons and $($manifest.adminArt.Count) admin assets."
