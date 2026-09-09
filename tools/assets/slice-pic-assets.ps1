[CmdletBinding()]
param(
    [string]$RepoRoot
)

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$picRoot = Join-Path $RepoRoot 'pic'
$talentSourceRoot = Join-Path $picRoot 'talant'
$itemSourceRoot = Join-Path $picRoot 'item'
$personalSourceRoot = Join-Path $picRoot 'PersonalArt'
$playerAssetRoot = Join-Path $RepoRoot 'web\elyndor-web\src\assets'
$adminAssetRoot = Join-Path $RepoRoot 'web\elyndor-admin\src\assets\admin'
$manifestPath = Join-Path $RepoRoot 'tools\assets\asset-manifest.json'
$abilityMapPath = Join-Path $RepoRoot 'web\elyndor-web\src\game\talents\talentAbilityArt.generated.ts'

function Ensure-Directory([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }
}

function Write-Utf8NoBom([string]$path, [string]$content) {
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $content, $utf8)
}

function Find-SourceFile([string]$root, [string]$pattern) {
    $files = @(Get-ChildItem -LiteralPath $root -File -Recurse |
        Where-Object { $_.Name -like $pattern } |
        Sort-Object FullName)
    if ($files.Count -eq 0) {
        throw "Asset source not found: $root/$pattern"
    }
    if ($files.Count -gt 1) {
        throw "Asset source pattern is ambiguous: $root/$pattern ($($files.FullName -join ', '))"
    }
    return $files[0].FullName
}

function Save-CroppedPng(
    [string]$sourcePath,
    [string]$outputPath,
    [int]$columns,
    [int]$rows,
    [int]$cellIndex,
    [int]$inset = 0
) {
    Ensure-Directory (Split-Path -Parent $outputPath)
    $source = $null
    $bitmap = $null
    $graphics = $null
    try {
        $source = [System.Drawing.Image]::FromFile($sourcePath)
        $cellWidth = [int][Math]::Floor($source.Width / $columns)
        $cellHeight = [int][Math]::Floor($source.Height / $rows)
        $column = $cellIndex % $columns
        $row = [int][Math]::Floor($cellIndex / $columns)
        if ($row -ge $rows) {
            throw "Cell index $cellIndex is outside ${columns}x${rows} grid for $sourcePath"
        }

        if ($inset -lt 0 -or ($inset * 2) -ge $cellWidth -or ($inset * 2) -ge $cellHeight) {
            throw "Inset $inset is too large for ${columns}x${rows} cell in $sourcePath"
        }

        $sourceY = [int]($row * $cellHeight)
        $sourceHeight = $cellHeight
        if ($columns -eq 6 -and $rows -eq 6 -and $source.Width -eq 1254 -and $source.Height -eq 1254) {
            # The supplied talent sheets are contact sheets, not six equal-height rows.
            # Their artwork rows are offset by the export process, so equal grid math
            # leaks the next row into the bottom of icons in the middle of the sheet.
            $rowStarts = @(14, 211, 407, 601, 793, 991)
            $rowEnds = @(192, 390, 584, 776, 974, 1188)
            $sourceY = $rowStarts[$row]
            $sourceHeight = $rowEnds[$row] - $sourceY
        }

        $croppedWidth = $cellWidth - ($inset * 2)
        $croppedHeight = $sourceHeight - ($inset * 2)
        if ($croppedHeight -le 0) {
            throw "Inset $inset is too large for source row $row in $sourcePath"
        }

        # Keep the generated icon square even when the source contact sheet row is not.
        $bitmap = [System.Drawing.Bitmap]::new($croppedWidth, $croppedWidth, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $sourceX = [int]($column * $cellWidth) + $inset
        $sourceY += $inset
        $sourceRectangle = [System.Drawing.Rectangle]::new($sourceX, $sourceY, $croppedWidth, $croppedHeight)
        $destinationRectangle = [System.Drawing.Rectangle]::new(0, 0, $croppedWidth, $croppedWidth)
        $graphics.DrawImage($source, $destinationRectangle, $sourceRectangle, [System.Drawing.GraphicsUnit]::Pixel)
        $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        if ($null -ne $graphics) { $graphics.Dispose() }
        if ($null -ne $bitmap) { $bitmap.Dispose() }
        if ($null -ne $source) { $source.Dispose() }
    }
}

function Copy-Asset([string]$sourcePath, [string]$outputPath) {
    Ensure-Directory (Split-Path -Parent $outputPath)
    Copy-Item -LiteralPath $sourcePath -Destination $outputPath -Force
}

function Add-IconIdsToJsonFile([string]$path, [hashtable]$assignments) {
    if ($assignments.Count -eq 0) { return }
    $raw = [System.IO.File]::ReadAllText($path)
    $newline = if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" }

    foreach ($id in $assignments.Keys) {
        $iconId = [string]$assignments[$id]
        $escapedId = [regex]::Escape([string]$id)
        $pattern = '(?m)^([ \t]*)"id": "' + $escapedId + '",\r?\n(?![ \t]*"iconId")'
        $replacement = '$1"id": "' + $id + '",' + $newline + '$1"iconId": "' + $iconId + '",' + $newline
        $updated = [regex]::Replace($raw, $pattern, $replacement, 1)
        if ($updated -eq $raw) {
            $existingPattern = '(?m)^([ \t]*)"id": "' + $escapedId + '",\r?\n[ \t]*"iconId": "[^"]*"'
            if (-not [regex]::IsMatch($raw, $existingPattern)) {
                throw "Could not add iconId for $id in $path"
            }
        }
        $raw = $updated
    }

    Write-Utf8NoBom $path $raw
}

function Get-JsonDocument([string]$path) {
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Get-NodeIconId([string]$branchId, [int]$index) {
    $prefix = switch ($branchId) {
        'GUARDIAN' { 'GUARDIAN' }
        'WARLORD' { 'WARLORD' }
        'FIRE' { 'FIRE' }
        'ARCANE' { 'ARCANE' }
        'FROST' { 'FROST' }
        'MARKSMAN' { 'MARKSMAN' }
        'BEAST_MASTERY' { 'BEAST_MASTERY' }
        'ARCANE_ARCHER' { 'ARCANE_ARCHER' }
        default { $null }
    }
    if ($null -eq $prefix) { return $null }

    $sourceIndex = if ($branchId -in @('MARKSMAN', 'BEAST_MASTERY', 'ARCANE_ARCHER')) {
        $index % 15
    } else {
        $index
    }
    $number = ($sourceIndex + 1).ToString('00')
    return "${prefix}_${number}"
}

function Add-TalentIcons(
    [string]$path,
    [hashtable]$fileAssignments,
    [hashtable]$abilityAssignments
) {
    $document = Get-JsonDocument $path
    $nodeIconIds = @{}
    foreach ($tree in @($document.talentTrees)) {
        $nodesByBranch = @{}
        foreach ($node in @($tree.nodes)) {
            if (-not $nodesByBranch.ContainsKey($node.branchId)) {
                $nodesByBranch[$node.branchId] = [System.Collections.Generic.List[object]]::new()
            }
            $nodesByBranch[$node.branchId].Add($node)
        }

        foreach ($branchId in $nodesByBranch.Keys) {
            $nodes = $nodesByBranch[$branchId]
            for ($index = 0; $index -lt $nodes.Count; $index++) {
                $node = $nodes[$index]
                $iconId = if ($node.PSObject.Properties.Name -contains 'iconId' -and -not [string]::IsNullOrWhiteSpace($node.iconId)) {
                    [string]$node.iconId
                } else {
                    Get-NodeIconId $branchId $index
                }
                if ($null -eq $iconId) { continue }

                $nodeIconIds[[string]$node.id] = $iconId
                if (-not ($node.PSObject.Properties.Name -contains 'iconId') -or [string]::IsNullOrWhiteSpace($node.iconId)) {
                    $fileAssignments[[string]$node.id] = $iconId
                }

                foreach ($modifier in @($node.modifiers)) {
                    if ($modifier.key -eq 'UNLOCK_ABILITY' -and -not [string]::IsNullOrWhiteSpace($modifier.targetId)) {
                        $abilityAssignments[[string]$modifier.targetId] = $iconId
                    }
                }
            }
        }
    }

    Add-IconIdsToJsonFile $path $fileAssignments
    return $nodeIconIds
}

function Get-SetIdForItemId([string]$itemId) {
    $patterns = [ordered]@{
        'WARRIOR_RARE_GREY_FANG_' = 'SET_WARRIOR_GREY_FANG'
        'WARRIOR_EPIC_CRIMSON_FURY_' = 'SET_WARRIOR_CRIMSON_FURY'
        'WARRIOR_LEGENDARY_FIRST_GUARD_' = 'SET_WARRIOR_FIRST_GUARD'
        'WARRIOR_LEGENDARY_BLACK_BASTION_' = 'SET_WARRIOR_BLACK_BASTION'
        'MAGE_RARE_THREE_ELEMENTS_' = 'SET_MAGE_THREE_ELEMENTS'
        'MAGE_EPIC_SHATTERED_STAR_' = 'SET_MAGE_SHATTERED_STAR'
        'MAGE_LEGENDARY_SILENT_ARCHON_' = 'SET_MAGE_SILENT_ARCHON'
        'MAGE_LEGENDARY_ECLIPSED_ORACLE_' = 'SET_MAGE_ECLIPSED_ORACLE'
    }
    foreach ($prefix in $patterns.Keys) {
        if ($itemId.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $patterns[$prefix]
        }
    }
    return $null
}

function Get-SetSlot([object]$item) {
    if ($item.slot -eq 'MainHand') {
        if ([string]$item.id -match '_STAFF$') { return 'Staff' }
        if ([string]$item.id -match '_WAND$') { return 'Wand' }
        return 'Weapon'
    }
    if ($item.slot -eq 'OffHand') {
        if ($item.offHandCategory -eq 'FOCUS') { return 'Focus' }
        return 'Shield'
    }
    return [string]$item.slot
}

function Get-FirstSourceByPattern([string]$root, [string]$pattern) {
    $files = @(Get-ChildItem -LiteralPath $root -File -Recurse |
        Where-Object { $_.BaseName -like $pattern } |
        Sort-Object FullName)
    if ($files.Count -eq 0) { return $null }
    if ($files.Count -gt 1) {
        throw "Item asset pattern is ambiguous: $root/$pattern ($($files.FullName -join ', '))"
    }
    return $files[0].FullName
}

if (-not (Test-Path -LiteralPath $picRoot)) {
    throw "Local source directory is missing: $picRoot"
}

$talentOutputRoot = Join-Path $playerAssetRoot 'game\talents'
$talentManifest = [System.Collections.Generic.List[object]]::new()
$talentGrids = @(
    @{ Branch = 'GUARDIAN'; Source = (Find-SourceFile $talentSourceRoot 'war1.png'); Columns = 6; Rows = 6; Count = 31 },
    @{ Branch = 'WARLORD'; Source = (Find-SourceFile $talentSourceRoot 'war3.png'); Columns = 6; Rows = 6; Count = 33 },
    @{ Branch = 'FIRE'; Source = (Find-SourceFile $talentSourceRoot 'mage1.png'); Columns = 6; Rows = 6; Count = 32 },
    @{ Branch = 'ARCANE'; Source = (Find-SourceFile $talentSourceRoot 'mage2.png'); Columns = 6; Rows = 6; Count = 32 },
    @{ Branch = 'FROST'; Source = (Find-SourceFile $talentSourceRoot 'mage3.png'); Columns = 6; Rows = 6; Count = 32 },
    @{ Branch = 'MARKSMAN'; Source = (Find-SourceFile $talentSourceRoot 'arc1.png'); Columns = 5; Rows = 3; Count = 15 },
    @{ Branch = 'BEAST_MASTERY'; Source = (Find-SourceFile $talentSourceRoot 'arc2.png'); Columns = 5; Rows = 3; Count = 15 },
    @{ Branch = 'ARCANE_ARCHER'; Source = (Find-SourceFile $talentSourceRoot 'arc3.png'); Columns = 5; Rows = 3; Count = 15 }
)

foreach ($grid in $talentGrids) {
    $branchFolder = ($grid.Branch.ToLowerInvariant() -replace '_', '-')
    $outputFolder = Join-Path $talentOutputRoot $branchFolder
    for ($index = 0; $index -lt $grid.Count; $index++) {
        $number = ($index + 1).ToString('00')
        $iconName = "$($branchFolder)-$number.png"
        $outputPath = Join-Path $outputFolder $iconName
        Save-CroppedPng $grid.Source $outputPath $grid.Columns $grid.Rows $index 8
        $talentManifest.Add([ordered]@{
                iconId = "$( $grid.Branch )_$number"
                branch = $grid.Branch
                source = (Resolve-Path -LiteralPath $grid.Source -Relative)
                cell = $index
                output = (Resolve-Path -LiteralPath $outputPath -Relative)
            })
    }
}

$itemOutputRoot = Join-Path $playerAssetRoot 'items'
Ensure-Directory $itemOutputRoot
$copiedItemAssets = [System.Collections.Generic.List[object]]::new()
foreach ($source in Get-ChildItem -LiteralPath $itemSourceRoot -File -Recurse |
    Where-Object { $_.FullName -notlike ((Join-Path $itemSourceRoot 'set') + '\*') } |
    Sort-Object FullName) {
    $outputPath = Join-Path $itemOutputRoot $source.Name
    Copy-Asset $source.FullName $outputPath
    $copiedItemAssets.Add([ordered]@{
            iconId = $source.BaseName
            source = (Resolve-Path -LiteralPath $source.FullName -Relative)
            output = (Resolve-Path -LiteralPath $outputPath -Relative)
        })
}

$warriorSetRoot = Join-Path $itemSourceRoot 'set\warrior'
$mageSetRoot = Join-Path $itemSourceRoot 'set\mage'
$mageSetSheets = @(Get-ChildItem -LiteralPath $mageSetRoot -Filter '*.png' -File | Sort-Object FullName)
if ($mageSetSheets.Count -lt 5) {
    throw "Expected five mage set sheets under $mageSetRoot, found $($mageSetSheets.Count)"
}

$setDefinitions = [ordered]@{
    SET_WARRIOR_GREY_FANG = @{ Source = (Find-SourceFile $warriorSetRoot '*Rare Set*'); Columns = 4; Rows = 2; Slots = @('Head', 'Shoulders', 'Chest', 'Hands', 'Legs', 'Feet', 'Weapon', 'Shield') };
    SET_WARRIOR_CRIMSON_FURY = @{ Source = (Find-SourceFile $warriorSetRoot '*Epic Set*'); Columns = 4; Rows = 2; Slots = @('Head', 'Shoulders', 'Chest', 'Hands', 'Legs', 'Feet', 'Weapon', 'Shield') };
    SET_WARRIOR_FIRST_GUARD = @{ Source = (Find-SourceFile $warriorSetRoot '*Legendary Set*'); Columns = 4; Rows = 2; Slots = @('Head', 'Shoulders', 'Chest', 'Hands', 'Legs', 'Feet', 'Weapon', 'Shield') };
    SET_WARRIOR_BLACK_BASTION = @{ Source = (Find-SourceFile $warriorSetRoot '*Legendary II Set*'); Columns = 4; Rows = 2; Slots = @('Head', 'Shoulders', 'Chest', 'Hands', 'Legs', 'Feet', 'Weapon', 'Shield') };
    SET_MAGE_THREE_ELEMENTS = @{ Source = $mageSetSheets[4].FullName; Columns = 3; Rows = 3; Slots = @('Head', 'Shoulders', 'Chest', 'Hands', 'Legs', 'Feet', 'Staff', 'Wand', 'Focus') };
    SET_MAGE_SHATTERED_STAR = @{ Source = $mageSetSheets[1].FullName; Columns = 3; Rows = 3; Slots = @('Head', 'Shoulders', 'Chest', 'Hands', 'Legs', 'Feet', 'Staff', 'Wand', 'Focus') };
    SET_MAGE_SILENT_ARCHON = @{ Source = $mageSetSheets[2].FullName; Columns = 3; Rows = 3; Slots = @('Head', 'Shoulders', 'Chest', 'Hands', 'Legs', 'Feet', 'Staff', 'Wand', 'Focus') };
    SET_MAGE_ECLIPSED_ORACLE = @{ Source = $mageSetSheets[3].FullName; Columns = 3; Rows = 3; Slots = @('Head', 'Shoulders', 'Chest', 'Hands', 'Legs', 'Feet', 'Staff', 'Wand', 'Focus') }
}

$allItemFiles = Get-ChildItem -LiteralPath (Join-Path $RepoRoot 'content\items') -Filter '*.json' -File | Sort-Object FullName
$allItems = [System.Collections.Generic.List[object]]::new()
foreach ($itemFile in $allItemFiles) {
    $document = Get-JsonDocument $itemFile.FullName
    foreach ($item in @($document.items)) {
        if ($null -ne $item) {
            $allItems.Add([pscustomobject]@{ File = $itemFile.FullName; Item = $item })
        }
    }
}

$itemIconAssignmentsByFile = @{}
$itemManifest = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $allItems) {
    $item = $entry.Item
    $itemId = [string]$item.id
    $iconId = $null
    $standaloneSource = Get-FirstSourceByPattern $itemSourceRoot ($itemId.ToLowerInvariant())
    if ($null -ne $standaloneSource) {
        $iconId = $itemId.ToLowerInvariant()
    }

    $setId = Get-SetIdForItemId $itemId
    if ($null -ne $setId -and $setDefinitions.Contains($setId)) {
        $iconId = $itemId.ToLowerInvariant()
    }

    if ($itemId -eq 'DUNGEON_MINES_WARRIOR_HELM_RARE') { $iconId = 'dungeon_mines_warrior_helm_rare' }
    if ($itemId -eq 'DUNGEON_MINES_WARRIOR_GAUNTLETS_EPIC') { $iconId = 'dungeon_mines_warrior_gauntlets_epic' }
    if ($itemId -eq 'DUNGEON_MINES_WARRIOR_LEGENDARY_SHIELD') { $iconId = 'dungeon_mines_warrior_legendary_shield' }
    if ($itemId -eq 'DUNGEON_MINES_MAGE_HOOD_RARE') { $iconId = 'dungeon_mines_mage_hood_rare' }
    if ($itemId -eq 'DUNGEON_MINES_MAGE_BOOTS_EPIC') { $iconId = 'dungeon_mines_mage_boots_epic' }
    if ($itemId -eq 'DUNGEON_MINES_MAGE_LEGENDARY_STAFF') { $iconId = 'dungeon_mines_mage_legendary_staff' }

    if ($null -ne $iconId) {
        if (-not $itemIconAssignmentsByFile.ContainsKey($entry.File)) {
            $itemIconAssignmentsByFile[$entry.File] = @{}
        }
        if (-not ($item.PSObject.Properties.Name -contains 'iconId') -or [string]::IsNullOrWhiteSpace($item.iconId)) {
            $itemIconAssignmentsByFile[$entry.File][$itemId] = $iconId
        }
    }
}

$setManifest = [System.Collections.Generic.List[object]]::new()
foreach ($setId in $setDefinitions.Keys) {
    $definition = $setDefinitions[$setId]
    $sourcePath = $definition.Source
    $slotIndexes = @{}
    for ($index = 0; $index -lt $definition.Slots.Count; $index++) {
        $slotIndexes[$definition.Slots[$index]] = $index
    }

    foreach ($entry in $allItems | Where-Object { (Get-SetIdForItemId ([string]$_.Item.id)) -eq $setId }) {
        $item = $entry.Item
        $slot = Get-SetSlot $item
        if (-not $slotIndexes.ContainsKey($slot)) { continue }
        $iconId = ([string]$item.id).ToLowerInvariant()
        $outputPath = Join-Path (Join-Path $itemOutputRoot 'sets') "$iconId.png"
        Save-CroppedPng $sourcePath $outputPath $definition.Columns $definition.Rows $slotIndexes[$slot]
        $setManifest.Add([ordered]@{
                setId = $setId
                itemId = $item.id
                slot = $slot
                source = (Resolve-Path -LiteralPath $sourcePath -Relative)
                cell = $slotIndexes[$slot]
                iconId = $iconId
                output = (Resolve-Path -LiteralPath $outputPath -Relative)
            })
    }
}

$generatedFallbacks = @(
    @{ Name = 'dungeon_mines_warrior_helm_rare'; Source = 'warrior_rare_grey_fang_head' },
    @{ Name = 'dungeon_mines_warrior_gauntlets_epic'; Source = 'warrior_epic_crimson_fury_hands' },
    @{ Name = 'dungeon_mines_warrior_legendary_shield'; Source = 'warrior_legendary_black_bastion_shield' },
    @{ Name = 'dungeon_mines_mage_hood_rare'; Source = 'mage_rare_three_elements_head' },
    @{ Name = 'dungeon_mines_mage_boots_epic'; Source = 'mage_epic_shattered_star_feet' },
    @{ Name = 'dungeon_mines_mage_legendary_staff'; Source = 'mage_legendary_silent_archon_staff' }
)
$fallbackManifest = [System.Collections.Generic.List[object]]::new()
foreach ($fallback in $generatedFallbacks) {
    $sourcePath = Join-Path (Join-Path $itemOutputRoot 'sets') "$($fallback.Source).png"
    $outputPath = Join-Path $itemOutputRoot "$($fallback.Name).png"
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Expected generated set source is missing: $sourcePath"
    }
    Copy-Asset $sourcePath $outputPath
    $fallbackManifest.Add([ordered]@{ iconId = $fallback.Name; source = $fallback.Source; output = (Resolve-Path -LiteralPath $outputPath -Relative) })
}

$archerMineFallbackSource = Join-Path $itemOutputRoot 'ranger_fang_charm.png'
foreach ($name in @('dungeon_mines_archer_legs_rare', 'dungeon_mines_archer_gloves_epic', 'dungeon_mines_archer_legendary_bow')) {
    $outputPath = Join-Path $itemOutputRoot "$name.png"
    Copy-Asset $archerMineFallbackSource $outputPath
    $fallbackManifest.Add([ordered]@{ iconId = $name; source = 'ranger_fang_charm'; output = (Resolve-Path -LiteralPath $outputPath -Relative) })
}

foreach ($file in $itemIconAssignmentsByFile.Keys) {
    Add-IconIdsToJsonFile $file $itemIconAssignmentsByFile[$file]
}

$talentAssignments = @{}
$abilityAssignments = @{}
$talentFiles = @(
    (Join-Path $RepoRoot 'content\package.json'),
    (Join-Path $RepoRoot 'content\talents\mage-pyromancer.json'),
    (Join-Path $RepoRoot 'content\talents\archer.json')
)
foreach ($talentFile in $talentFiles) {
    $fileAssignments = @{}
    $nodeIcons = Add-TalentIcons $talentFile $fileAssignments $abilityAssignments
    $talentAssignments[$talentFile] = $fileAssignments
}

$abilityLines = [System.Collections.Generic.List[string]]::new()
$abilityLines.Add('// Generated by tools/assets/slice-pic-assets.ps1. Do not edit manually.')
$abilityLines.Add('')
$abilityLines.Add('export const talentAbilityArt: Readonly<Record<string, string>> = {')
foreach ($entry in $abilityAssignments.GetEnumerator() | Sort-Object Name) {
    $abilityLines.Add("  $($entry.Key): '$($entry.Value)',")
}
$abilityLines.Add('}')
$abilityLines.Add('')
Ensure-Directory (Split-Path -Parent $abilityMapPath)
Write-Utf8NoBom $abilityMapPath ($abilityLines -join "`n")

$personalOutputRoot = Join-Path $playerAssetRoot 'characters\personal'
$personalMappings = @(
    @{ Pattern = '*19_01_44*'; Name = 'archer-female-transparent.png' },
    @{ Pattern = '*19_01_46 (1)*'; Name = 'archer-male-transparent.png' },
    @{ Pattern = '*19_01_46 (2)*'; Name = 'archer-male-scene.png' },
    @{ Pattern = '*19_01_57 (1)*'; Name = 'mage-female-transparent.png' },
    @{ Pattern = '*19_01_57 (2)*'; Name = 'mage-female-scene.png' },
    @{ Pattern = '*19_02_04 (1)*'; Name = 'mage-male-transparent.png' },
    @{ Pattern = '*19_02_04 (2)*'; Name = 'mage-male-scene.png' },
    @{ Pattern = '*19_02_12 (1)*'; Name = 'warrior-male-transparent.png' },
    @{ Pattern = '*19_02_12 (2)*'; Name = 'warrior-male-scene.png' }
)
$characterManifest = [System.Collections.Generic.List[object]]::new()
foreach ($mapping in $personalMappings) {
    $sourcePath = Find-SourceFile $personalSourceRoot $mapping.Pattern
    $outputPath = Join-Path $personalOutputRoot $mapping.Name
    if ($mapping.Name -eq 'archer-female-transparent.png') {
        & (Join-Path $PSScriptRoot 'remove-checkerboard.ps1') -SourcePath $sourcePath -OutputPath $outputPath
        if (-not $?) { throw "Could not remove checkerboard background from $sourcePath" }
    } else {
        Copy-Asset $sourcePath $outputPath
    }
    $characterManifest.Add([ordered]@{ name = $mapping.Name; source = (Resolve-Path -LiteralPath $sourcePath -Relative); output = (Resolve-Path -LiteralPath $outputPath -Relative) })
}

$splitSources = @(Get-ChildItem -LiteralPath $personalSourceRoot -File |
    Where-Object { $_.Name -like 'Раздвоенный*' } |
    Sort-Object FullName)
if ($splitSources.Count -eq 0) {
    $splitSources = @(Get-ChildItem -LiteralPath $personalSourceRoot -File |
        Where-Object { $_.Name -match '\u0420\u0430\u0437\u0434\u0432\u043e\u0435\u043d\u043d\u044b\u0439' } |
        Sort-Object FullName)
}
if ($splitSources.Count -ne 1) {
    throw "Expected exactly one split personal character artwork in $personalSourceRoot, found $($splitSources.Count)"
}
$splitSource = $splitSources[0].FullName
$splitOutput = Join-Path $personalOutputRoot 'archer-female-scene.png'
Save-CroppedPng $splitSource $splitOutput 2 1 1
$characterManifest.Add([ordered]@{ name = 'archer-female-scene.png'; source = (Resolve-Path -LiteralPath $splitSource -Relative); cell = 1; output = (Resolve-Path -LiteralPath $splitOutput -Relative) })

foreach ($generatedAsset in @(
        @{ Name = 'warrior-female-transparent.webp'; Source = 'generated/player-art' },
        @{ Name = 'warrior-female-scene.webp'; Source = 'generated/player-art' }
    )) {
    $generatedPath = Join-Path $personalOutputRoot $generatedAsset.Name
    if (Test-Path -LiteralPath $generatedPath) {
        $characterManifest.Add([ordered]@{
                name = $generatedAsset.Name
                source = $generatedAsset.Source
                output = (Resolve-Path -LiteralPath $generatedPath -Relative)
            })
    }
}

Ensure-Directory $adminAssetRoot
$adminManifest = [System.Collections.Generic.List[object]]::new()
$adminIndex = 1
foreach ($source in Get-ChildItem -LiteralPath (Join-Path $personalSourceRoot 'admin') -File | Sort-Object FullName) {
    $name = "admin-{0:00}{1}" -f $adminIndex, $source.Extension.ToLowerInvariant()
    $outputPath = Join-Path $adminAssetRoot $name
    Copy-Asset $source.FullName $outputPath
    $adminManifest.Add([ordered]@{ originalName = $source.Name; output = (Resolve-Path -LiteralPath $outputPath -Relative) })
    $adminIndex++
}

$manifest = [ordered]@{
    schemaVersion = 1
    sourceDirectories = @('pic/talant', 'pic/item', 'pic/PersonalArt')
    talentIcons = $talentManifest
    itemSheetIcons = $setManifest
    copiedItemIcons = $copiedItemAssets
    fallbackItemIcons = $fallbackManifest
    characterArt = $characterManifest
    adminArt = $adminManifest
    talentAbilityArt = $abilityAssignments
    limitations = @(
        'Archer talent sheets contain 15 source cells per branch; node icons reuse those cells cyclically for nodes 16-32.',
        'No war2.png source sheet was present; the root talant PNG set remains the existing Berserker art registry.',
        'No archer set sheets were present in pic/item/set, so archer armor and bow definitions without standalone source art keep the existing glyph fallback.',
        'Female warrior player art is bundled as generated player art; admin-only source artwork remains restricted to admin assets.'
    )
}
Ensure-Directory (Split-Path -Parent $manifestPath)
Write-Utf8NoBom $manifestPath (($manifest | ConvertTo-Json -Depth 12) + "`n")

Write-Host "Generated $($talentManifest.Count) talent icons, $($setManifest.Count) set icons, $($copiedItemAssets.Count) standalone item icons, $($characterManifest.Count) player character assets and $($adminManifest.Count) admin assets."
