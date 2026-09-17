[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$SourceRoot,
    [ValidateRange(1, 100)]
    [int]$Quality = 90
)

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}
if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Join-Path $RepoRoot 'pic\V2'
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$talentSourceRoot = Join-Path $SourceRoot 'talant'
$spellSourceRoot = Join-Path $SourceRoot 'icon spell'
$talentOutputRoot = Join-Path $RepoRoot 'web\elyndor-web\src\assets\game\talents'
$spellOutputRoot = Join-Path $RepoRoot 'web\elyndor-web\src\assets\abilities'
$manifestPath = Join-Path $RepoRoot 'tools\assets\v2-art-manifest.json'

$gridColumns = 6
$gridRows = 6
$inset = 8
# V2 sheets use 204px row spacing with 178px of icon artwork; the remaining
# pixels are the sheet gutters and must not leak into the next talent/spell.
$rowPitch = 204
$artHeight = 178

function Ensure-Directory([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Save-GridCell([string]$SourcePath, [string]$OutputPath, [int]$CellIndex) {
    $source = $null
    $bitmap = $null
    $graphics = $null
    try {
        $source = [System.Drawing.Image]::FromFile($SourcePath)
        if ($source.Width -ne 1254 -or $source.Height -ne 1254) {
            throw "Unexpected sheet dimensions for $SourcePath ($($source.Width)x$($source.Height)); expected 1254x1254."
        }
        if ($CellIndex -lt 0 -or $CellIndex -ge ($gridColumns * $gridRows)) {
            throw "Cell index $CellIndex is outside the ${gridColumns}x${gridRows} sheet."
        }

        $cellWidth = [int][Math]::Floor($source.Width / $gridColumns)
        $column = $CellIndex % $gridColumns
        $row = [int][Math]::Floor($CellIndex / $gridColumns)
        $cropWidth = $cellWidth - ($inset * 2)
        $cropHeight = $artHeight
        if ($cropWidth -le 0 -or $cropHeight -le 0) {
            throw "Invalid crop bounds for cell $CellIndex in $SourcePath."
        }

        Ensure-Directory (Split-Path -Parent $OutputPath)
        $bitmap = [System.Drawing.Bitmap]::new(
            $cropWidth,
            $cropWidth,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $sourceRectangle = [System.Drawing.Rectangle]::new(
            ($column * $cellWidth) + $inset,
            ($row * $rowPitch) + $inset,
            $cropWidth,
            $cropHeight)
        $destinationRectangle = [System.Drawing.Rectangle]::new(0, 0, $cropWidth, $cropWidth)
        $graphics.DrawImage(
            $source,
            $destinationRectangle,
            $sourceRectangle,
            [System.Drawing.GraphicsUnit]::Pixel)
        $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        if ($null -ne $graphics) { $graphics.Dispose() }
        if ($null -ne $bitmap) { $bitmap.Dispose() }
        if ($null -ne $source) { $source.Dispose() }
    }
}

function Set-JsonArrayObjectField(
    [string]$Path,
    [string]$ArrayName,
    [string]$ObjectId,
    [string]$FieldName,
    [string]$Value
) {
    $raw = [System.IO.File]::ReadAllText($Path)
    $arrayMarker = '"' + $ArrayName + '"'
    $arrayMarkerIndex = $raw.IndexOf($arrayMarker, [System.StringComparison]::Ordinal)
    if ($arrayMarkerIndex -lt 0) { throw "Array '$ArrayName' not found in $Path." }
    $arrayStart = $raw.IndexOf('[', $arrayMarkerIndex + $arrayMarker.Length)
    if ($arrayStart -lt 0) { throw "Array '$ArrayName' is malformed in $Path." }

    $depth = 0
    $inString = $false
    $objectStart = -1
    for ($index = $arrayStart + 1; $index -lt $raw.Length; $index++) {
        $character = $raw[$index]
        if ($inString) {
            if ($character -eq '\') { $index++; continue }
            if ($character -eq '"') { $inString = $false }
            continue
        }

        if ($character -eq '"') { $inString = $true; continue }
        if ($character -eq '{') {
            if ($depth -eq 0) { $objectStart = $index }
            $depth++
            continue
        }
        if ($character -ne '}') { continue }

        $depth--
        if ($depth -ne 0 -or $objectStart -lt 0) { continue }

        $objectLength = $index - $objectStart + 1
        $objectText = $raw.Substring($objectStart, $objectLength)
        $parsed = $objectText | ConvertFrom-Json
        if ($parsed.PSObject.Properties.Name -notcontains 'id' -or
            -not [string]::Equals([string]$parsed.id, $ObjectId, [System.StringComparison]::Ordinal)) {
            $objectStart = -1
            continue
        }

        $fieldPattern = '"' + [regex]::Escape($FieldName) + '"\s*:\s*"[^"]*"'
        if ([regex]::IsMatch($objectText, $fieldPattern)) {
            $replacement = '"' + $FieldName + '": "' + $Value + '"'
            $objectText = [regex]::Replace($objectText, $fieldPattern, $replacement, 1)
        }
        else {
            $idMatch = [regex]::Match($objectText, '"id"\s*:\s*"' + [regex]::Escape($ObjectId) + '"')
            if (-not $idMatch.Success) { throw "Could not find id '$ObjectId' in $Path." }
            $replacement = $idMatch.Value + ', "' + $FieldName + '": "' + $Value + '"'
            $objectText = (
                $objectText.Substring(0, $idMatch.Index) +
                $replacement +
                $objectText.Substring($idMatch.Index + $idMatch.Length)
            )
        }

        $updated = (
            $raw.Substring(0, $objectStart) +
            $objectText +
            $raw.Substring($index + 1)
        )
        Write-Utf8NoBom $Path $updated
        return
    }

    throw "Object '$ObjectId' was not found in '$ArrayName' of $Path."
}

function Convert-GeneratedPngsToWebp([string]$Directory) {
    $pngFiles = @(Get-ChildItem -LiteralPath $Directory -Filter '*.png' -File | Sort-Object FullName)
    if ($pngFiles.Count -eq 0) { return }

    $inputs = @($pngFiles | ForEach-Object { $_.FullName })
    & npx --yes sharp-cli@6.1.0 -i $inputs -o $Directory -f webp -q $Quality | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sharp-cli failed for $Directory with exit code $LASTEXITCODE." }

    foreach ($png in $pngFiles) {
        $webpPath = Join-Path $Directory ($png.BaseName + '.webp')
        if (-not (Test-Path -LiteralPath $webpPath)) { throw "WebP output missing: $webpPath." }
        [System.IO.File]::Delete($png.FullName)
    }
}

$talentSheets = @(
    @{ ClassId = 'ARCHER'; BranchId = 'MARKSMAN'; Content = 'content\talents\archer.json'; SourceDirectory = 'arch'; SourceIndex = 2; Prefix = 'MARKSMAN'; Folder = 'marksman' },
    @{ ClassId = 'ARCHER'; BranchId = 'BEAST_MASTERY'; Content = 'content\talents\archer.json'; SourceDirectory = 'arch'; SourceIndex = 1; Prefix = 'BEAST_MASTERY'; Folder = 'beast-mastery' },
    @{ ClassId = 'ARCHER'; BranchId = 'SURVIVAL'; Content = 'content\talents\archer.json'; SourceDirectory = 'arch'; SourceIndex = 0; Prefix = 'SURVIVAL'; Folder = 'survival' },
    @{ ClassId = 'MAGE'; BranchId = 'FIRE'; Content = 'content\talents\mage-pyromancer.json'; SourceDirectory = 'mage'; SourceIndex = 1; Prefix = 'FIRE'; Folder = 'fire' },
    @{ ClassId = 'MAGE'; BranchId = 'ARCANE'; Content = 'content\talents\mage-pyromancer.json'; SourceDirectory = 'mage'; SourceIndex = 2; Prefix = 'ARCANE'; Folder = 'arcane' },
    @{ ClassId = 'MAGE'; BranchId = 'FROST'; Content = 'content\talents\mage-pyromancer.json'; SourceDirectory = 'mage'; SourceIndex = 0; Prefix = 'FROST'; Folder = 'frost' },
    @{ ClassId = 'PALADIN'; BranchId = 'HOLY'; Content = 'content\talents\paladin.json'; SourceDirectory = 'paladin'; SourceIndex = 2; Prefix = 'PALADIN_HOLY'; Folder = 'paladin-holy' },
    @{ ClassId = 'PALADIN'; BranchId = 'PROTECTION'; Content = 'content\talents\paladin.json'; SourceDirectory = 'paladin'; SourceIndex = 1; Prefix = 'PALADIN_PROTECTION'; Folder = 'paladin-protection' },
    @{ ClassId = 'PALADIN'; BranchId = 'RETRIBUTION'; Content = 'content\talents\paladin.json'; SourceDirectory = 'paladin'; SourceIndex = 0; Prefix = 'PALADIN_RETRIBUTION'; Folder = 'paladin-retribution' },
    @{ ClassId = 'WARRIOR'; BranchId = 'BERSERKER'; Content = 'content\package.json'; SourceDirectory = 'war'; SourceIndex = 0; Prefix = 'BERSERKER'; Folder = 'berserker' },
    @{ ClassId = 'WARRIOR'; BranchId = 'GUARDIAN'; Content = 'content\package.json'; SourceDirectory = 'war'; SourceIndex = 2; Prefix = 'GUARDIAN'; Folder = 'guardian' },
    @{ ClassId = 'WARRIOR'; BranchId = 'WARLORD'; Content = 'content\package.json'; SourceDirectory = 'war'; SourceIndex = 1; Prefix = 'WARLORD'; Folder = 'warlord' }
)

$spellSheets = @(
    @{
        ClassId = 'MAGE'; Folder = 'mage'; Source = 'mage spell.jpg'; AbilityFile = 'content\abilities\mage-pyromancer.json'
        Cells = @{
            0 = 'MAGE_FIREBALL'; 1 = 'MAGE_FIRE_BLAST'; 2 = 'MAGE_SCORCH'; 3 = 'MAGE_ICE_SHARD'
            4 = 'MAGE_BLIZZARD'; 5 = 'MAGE_FROST_NOVA'; 6 = 'MAGE_ARCANE_MISSILES'; 7 = 'MAGE_ARCANE_EXPLOSION'
            8 = 'MAGE_MANA_SHIELD'; 9 = 'MAGE_ICE_BARRIER'; 10 = 'MAGE_FLAMESTRIKE'; 11 = 'MAGE_CONE_OF_COLD'
            14 = 'MAGE_ARCANE_SPARK'; 15 = 'MAGE_PRESENCE_OF_MIND'; 18 = 'MAGE_COUNTERSPELL'; 20 = 'MAGE_EVOCATION'
            21 = 'MAGE_ICE_BLOCK'; 23 = 'MAGE_COMBUSTION'; 24 = 'MAGE_ICE_LANCE'; 25 = 'MAGE_PYROBLAST'
            26 = 'MAGE_COLD_SNAP'; 27 = 'MAGE_ARCANE_POWER'; 29 = 'MAGE_BLAST_WAVE'
        }
    },
    @{
        ClassId = 'PALADIN'; Folder = 'paladin'; Source = 'paladin spell.jpg'; AbilityFile = 'content\abilities\paladin.json'
        Cells = @{
            0 = 'HOLY_LIGHT'; 1 = 'FLASH_OF_LIGHT'; 2 = 'JUDGEMENT'; 3 = 'SEAL_OF_RIGHTEOUSNESS'
            4 = 'DEVOTION_AURA'; 5 = 'LAY_ON_HANDS'; 6 = 'BLESSING_OF_WISDOM'; 7 = 'CLEANSE'
            8 = 'DIVINE_FAVOR'; 9 = 'HOLY_SHOCK'; 10 = 'HOLY_SHOCK_OFFENSIVE'; 11 = 'BEACON_OF_LIGHT'
            12 = 'CONCENTRATION_AURA'; 13 = 'DIVINE_REPLENISHMENT'; 14 = 'BLESSING_OF_PROTECTION'; 15 = 'AURA_MASTERY'
            16 = 'CONSECRATION'; 17 = 'HOLY_SHIELD'; 18 = 'BLESSING_OF_SANCTUARY'; 19 = 'AVENGERS_SHIELD'
            20 = 'HAMMER_OF_THE_RIGHTEOUS'; 21 = 'DIVINE_PROTECTION'; 22 = 'INTERCESSION'; 23 = 'HAMMER_OF_JUSTICE'
            24 = 'SEAL_OF_COMMAND'; 25 = 'CRUSADER_STRIKE'; 26 = 'SANCTITY_AURA'; 27 = 'REPENTANCE'
            28 = 'TEMPLARS_VERDICT'; 29 = 'BLESSING_OF_MIGHT'; 30 = 'DIVINE_STORM'; 31 = 'AVENGING_WRATH'
        }
    },
    @{
        ClassId = 'WARRIOR'; Folder = 'warrior'; Source = 'war spell.jpg'; AbilityFile = 'content\package.json'
        ExtraAbilityFile = 'content\abilities\warrior-guardian.json'
        Cells = @{
            0 = 'STRIKE'; 1 = 'SHIELD_BASH'; 2 = 'WILD_STRIKE'; 3 = 'HEAVY_BLOW'; 4 = 'BATTLE_FOCUS'; 5 = 'BATTLE_SHOUT'
            6 = 'BASTION'; 7 = 'PROVOKE'; 8 = 'SHIELD_SLAM'; 9 = 'REVENGE'; 10 = 'CONCUSSION_BLOW'; 11 = 'SUNDER_ARMOR'
            12 = 'SHIELD_BLOCK'; 14 = 'BERSERK'; 15 = 'BATTLE_CRY'; 18 = 'WAR_BANNER'; 19 = 'CRY_OF_VENGEANCE'
            20 = 'ENDURANCE_CRY'; 21 = 'WHIRLWIND'; 24 = 'RALLY_CRY'; 25 = 'VICTORY_FLAG'
            27 = 'BATTLE_STANDARD'; 30 = 'CHALLENGING_SHOUT'; 31 = 'LAST_STAND'
        }
    }
)

if (-not (Test-Path -LiteralPath $talentSourceRoot)) { throw "Talent source directory is missing: $talentSourceRoot." }
if (-not (Test-Path -LiteralPath $spellSourceRoot)) { throw "Spell source directory is missing: $spellSourceRoot." }

$talentManifest = [System.Collections.Generic.List[object]]::new()
$spellManifest = [System.Collections.Generic.List[object]]::new()
$generatedDirectories = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($sheet in $talentSheets) {
    $sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $talentSourceRoot $sheet.SourceDirectory) -Filter '*.jpg' -File | Sort-Object Name)
    if ($sheet.SourceIndex -ge $sourceFiles.Count) {
        throw "Expected source cell $($sheet.SourceIndex) in $($sheet.SourceDirectory); found $($sourceFiles.Count) files."
    }
    $sourcePath = $sourceFiles[$sheet.SourceIndex].FullName
    $contentPath = Join-Path $RepoRoot $sheet.Content
    if (-not (Test-Path -LiteralPath $sourcePath)) { throw "Talent sheet is missing: $sourcePath." }
    if (-not (Test-Path -LiteralPath $contentPath)) { throw "Talent content is missing: $contentPath." }

    $document = Get-Content -LiteralPath $contentPath -Raw | ConvertFrom-Json
    $tree = @($document.talentTrees | Where-Object { $_.ClassId -eq $sheet.ClassId })
    if ($tree.Count -ne 1) { throw "Expected one $($sheet.ClassId) talent tree in $contentPath; found $($tree.Count)." }
    $nodes = @($tree[0].nodes | Where-Object { $_.branchId -eq $sheet.BranchId })
    if ($nodes.Count -eq 0 -or $nodes.Count -gt ($gridColumns * $gridRows)) {
        throw "Unexpected node count $($nodes.Count) for $($sheet.ClassId)/$($sheet.BranchId)."
    }

    $outputDirectory = Join-Path $talentOutputRoot $sheet.Folder
    Ensure-Directory $outputDirectory
    [void]$generatedDirectories.Add($outputDirectory)
    for ($index = 0; $index -lt $nodes.Count; $index++) {
        $node = $nodes[$index]
        $number = ($index + 1).ToString('00')
        $iconId = "$($sheet.Prefix)_$number"
        $fileName = "$($sheet.Folder)-$number"
        $pngPath = Join-Path $outputDirectory "$fileName.png"
        $webpPath = Join-Path $outputDirectory "$fileName.webp"
        Save-GridCell $sourcePath $pngPath $index
        Set-JsonArrayObjectField $contentPath 'nodes' ([string]$node.id) 'iconId' $iconId
        $talentManifest.Add([ordered]@{
                classId = $sheet.ClassId
                branchId = $sheet.BranchId
                talentId = [string]$node.id
                iconId = $iconId
                source = "pic/V2/talant/$($sheet.SourceDirectory)/$($sourceFiles[$sheet.SourceIndex].Name)"
                cell = $index
                output = "web/elyndor-web/src/assets/game/talents/$($sheet.Folder)/$fileName.webp"
            })
    }
}

foreach ($sheet in $spellSheets) {
    $sourcePath = Join-Path $spellSourceRoot $sheet.Source
    if (-not (Test-Path -LiteralPath $sourcePath)) { throw "Spell sheet is missing: $sourcePath." }
    $outputDirectory = Join-Path $spellOutputRoot $sheet.Folder
    Ensure-Directory $outputDirectory
    [void]$generatedDirectories.Add($outputDirectory)

    $cellCount = switch ($sheet.ClassId) {
        'MAGE' { 32 }
        'PALADIN' { 33 }
        'WARRIOR' { 33 }
        default { throw "No cell count configured for $($sheet.ClassId)." }
    }
    for ($index = 0; $index -lt $cellCount; $index++) {
        $abilityId = if ($sheet.Cells.ContainsKey($index)) { [string]$sheet.Cells[$index] } else { $null }
        $iconId = if ($null -ne $abilityId) {
            $abilitySlug = $abilityId.ToLowerInvariant() -replace '^mage_', '' -replace '_', '-'
            "$($sheet.Folder)-$abilitySlug"
        }
        else {
            $number = ($index + 1).ToString('00')
            "$($sheet.Folder)-unmapped-$number"
        }
        $pngPath = Join-Path $outputDirectory "$iconId.png"
        $webpPath = Join-Path $outputDirectory "$iconId.webp"
        Save-GridCell $sourcePath $pngPath $index
        if ($null -ne $abilityId) {
            $abilityPaths = @((Join-Path $RepoRoot $sheet.AbilityFile))
            if ($sheet.ContainsKey('ExtraAbilityFile')) {
                $abilityPaths += Join-Path $RepoRoot $sheet.ExtraAbilityFile
            }
            $updatedAbility = $false
            foreach ($abilityPath in $abilityPaths) {
                $abilityDocument = Get-Content -LiteralPath $abilityPath -Raw | ConvertFrom-Json
                if (@($abilityDocument.abilities | Where-Object { $_.id -eq $abilityId }).Count -eq 0) { continue }
                Set-JsonArrayObjectField $abilityPath 'abilities' $abilityId 'iconId' $iconId
                $updatedAbility = $true
            }
            if (-not $updatedAbility) {
                throw "Ability '$abilityId' was not found in configured content files for $($sheet.ClassId)."
            }
        }
        $spellManifest.Add([ordered]@{
                classId = $sheet.ClassId
                abilityId = $abilityId
                iconId = $iconId
                source = "pic/V2/icon spell/$($sheet.Source)"
                cell = $index
                output = "web/elyndor-web/src/assets/abilities/$($sheet.Folder)/$iconId.webp"
            })
    }
}

foreach ($directory in $generatedDirectories) {
    Convert-GeneratedPngsToWebp $directory
}

$manifest = [ordered]@{
    schemaVersion = 1
    sourceRoot = 'pic/V2'
    talentIcons = @($talentManifest)
    spellIcons = @($spellManifest)
}
Write-Utf8NoBom $manifestPath ($manifest | ConvertTo-Json -Depth 8)

Write-Host "Imported $($talentManifest.Count) V2 talent icons and $($spellManifest.Count) class spell sheet icons."
Write-Host "Manifest: $manifestPath"
