param(
    [Parameter(Mandatory = $true)]
    [string] $SourceRoot,
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$SourceRoot = (Resolve-Path -LiteralPath $SourceRoot).Path
$WebRoot = Join-Path $RepositoryRoot 'web\elyndor-web\src\assets'
$ItemRoot = Join-Path $WebRoot 'items'
$MonsterRoot = Join-Path $WebRoot 'monsters'
$ContentRoot = Join-Path $RepositoryRoot 'content'
$PromptPath = Join-Path $SourceRoot 'md\Elyndor_Prompt_Pack_WoW_Classic_RU.md'
$CatalogPath = Join-Path $RepositoryRoot 'docs\source-of-truth\content\art-coverage-catalog.csv'
$ReportPath = Join-Path $RepositoryRoot 'docs\source-of-truth\content\art-coverage-report.md'

if (-not (Test-Path -LiteralPath $PromptPath)) {
    throw "The source prompt pack was not found: $PromptPath"
}

$Cyrillic1251 = [System.Text.Encoding]::GetEncoding(1251)
$Utf8 = [System.Text.Encoding]::UTF8
$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$Utf8Bom = [System.Text.UTF8Encoding]::new($true)
$CyrillicToLatin = @{
    'а' = 'a'; 'б' = 'b'; 'в' = 'v'; 'г' = 'g'; 'д' = 'd'; 'е' = 'e'; 'ё' = 'yo'
    'ж' = 'zh'; 'з' = 'z'; 'и' = 'i'; 'й' = 'y'; 'к' = 'k'; 'л' = 'l'; 'м' = 'm'
    'н' = 'n'; 'о' = 'o'; 'п' = 'p'; 'р' = 'r'; 'с' = 's'; 'т' = 't'; 'у' = 'u'
    'ф' = 'f'; 'х' = 'kh'; 'ц' = 'ts'; 'ч' = 'ch'; 'ш' = 'sh'; 'щ' = 'shch'; 'ъ' = ''
    'ы' = 'y'; 'ь' = ''; 'э' = 'e'; 'ю' = 'yu'; 'я' = 'ya'
}

function Repair-DisplayText([string] $Value) {
    if ($Value -match '[\u0452-\u045F\u0402-\u040F]|вЂ') {
        $candidate = $Utf8.GetString($Cyrillic1251.GetBytes($Value))
        if ($candidate -notmatch '\uFFFD') { return $candidate }
    }
    return $Value
}

function Get-VisualKey([string] $Value) {
    $normalized = (Repair-DisplayText $Value).ToLowerInvariant().Replace('ё', 'е')
    $normalized = $normalized.Normalize([System.Text.NormalizationForm]::FormKC)
    return [regex]::Replace($normalized, '[^\p{L}\p{Nd}]', '')
}

function Get-MonsterSourceArtKey([string] $Value) {
    $builder = [System.Text.StringBuilder]::new()
    $normalized = (Repair-DisplayText $Value).ToLowerInvariant().Normalize([System.Text.NormalizationForm]::FormKC)
    foreach ($character in $normalized.ToCharArray()) {
        if ($CyrillicToLatin.ContainsKey([string]$character)) {
            [void]$builder.Append($CyrillicToLatin[[string]$character])
        }
        elseif ([string]$character -cmatch '^[a-z0-9]$') {
            [void]$builder.Append([string]$character)
        }
        elseif ($builder.Length -gt 0 -and -not $builder.ToString().EndsWith('-')) {
            [void]$builder.Append('-')
        }
    }
    $slug = $builder.ToString().Trim('-')
    if ($slug.Length -eq 0) { throw "Could not create a stable portrait key from '$Value'." }
    return 'enemy-' + $slug
}

function Read-ContentRecords([string] $Directory, [string] $CollectionName) {
    $records = @{}
    foreach ($file in (Get-ChildItem -LiteralPath $Directory -Filter '*.json' -File | Sort-Object FullName)) {
        $document = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        foreach ($record in @($document.$CollectionName)) {
            if ($null -ne $record -and -not [string]::IsNullOrWhiteSpace([string]$record.id)) {
                $records[[string]$record.id] = [pscustomobject]@{ Data = $record; File = $file.FullName }
            }
        }
    }
    return $records
}

function Get-SectionNames([string] $Markdown, [string] $SectionNumber) {
    $sectionPattern = [regex]::Escape($SectionNumber)
    $section = [regex]::Match(
        $Markdown,
        "(?ms)^###\s+$sectionPattern[^`r`n]*\r?\n(?<body>.*?)(?=^###\s|\z)"
    )
    if (-not $section.Success) { throw "Authoring section $SectionNumber was not found." }

    $lists = [regex]::Matches($section.Groups['body'].Value, '\*\*(?<names>[^*]+)\*\*') |
        ForEach-Object { $_.Groups['names'].Value } |
        Where-Object { ($_ -split ',').Count -ge 3 } |
        Sort-Object Length -Descending
    if (@($lists).Count -eq 0) { throw "No ordered item list found in authoring section $SectionNumber." }

    return @($lists[0] -split ',\s*' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}

function Get-Grid([string] $Path, [int] $Count) {
    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        $ratio = [double]$image.Width / [double]$image.Height
    }
    finally { $image.Dispose() }

    $candidates = @(
        for ($rows = 2; $rows -le 6; $rows++) {
            for ($columns = 2; $columns -le 6; $columns++) {
                $capacity = $columns * $rows
                $difference = [math]::Abs($ratio - ([double]$columns / $rows))
                if ($capacity -ge $Count -and $difference -le 0.055) {
                    [pscustomobject]@{ Columns = $columns; Rows = $rows; Extra = $capacity - $Count; Difference = $difference }
                }
            }
        }
    )
    if ($candidates.Count -eq 0) { throw "Could not infer a square-cell grid for $Path ($Count icons)." }
    return $candidates | Sort-Object Difference, Extra | Select-Object -First 1
}

function Save-Cell([string] $Source, [string] $Destination, [int] $Columns, [int] $Rows, [int] $Cell) {
    $image = [System.Drawing.Image]::FromFile($Source)
    $bitmap = $null
    try {
        $column = $Cell % $Columns
        $row = [math]::Floor($Cell / $Columns)
        $left = [int][math]::Round($column * $image.Width / $Columns)
        $right = [int][math]::Round(($column + 1) * $image.Width / $Columns)
        $top = [int][math]::Round($row * $image.Height / $Rows)
        $bottom = [int][math]::Round(($row + 1) * $image.Height / $Rows)
        $inset = [math]::Min(8, [math]::Floor([math]::Min($right - $left, $bottom - $top) / 20))
        $rectangle = [System.Drawing.Rectangle]::new(
            $left + $inset,
            $top + $inset,
            ($right - $left) - 2 * $inset,
            ($bottom - $top) - 2 * $inset
        )
        $bitmap = $image.Clone($rectangle, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $directory = Split-Path -Parent $Destination
        if (-not (Test-Path -LiteralPath $directory)) {
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }
        $bitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        if ($null -ne $bitmap) { $bitmap.Dispose() }
        $image.Dispose()
    }
}

function Convert-ImportedArtToWebp {
    $pngRows = @($importRows | Where-Object { $_.Output.EndsWith('.png', [System.StringComparison]::OrdinalIgnoreCase) })
    if ($pngRows.Count -eq 0) { return }

    $webRoot = [System.IO.Path]::GetFullPath($WebRoot).TrimEnd('\') + '\'
    $frontendRoot = Join-Path $RepositoryRoot 'web\elyndor-web'
    Push-Location $frontendRoot
    try {
        foreach ($group in ($pngRows | Group-Object { Split-Path -Parent (Join-Path $RepositoryRoot $_.Output.Replace('/', '\')) })) {
            $groupRows = @($group.Group)
            $uniqueRows = @($groupRows | Group-Object Output | ForEach-Object { $_.Group[0] })
            $inputPaths = @($uniqueRows | ForEach-Object { Join-Path $RepositoryRoot $_.Output.Replace('/', '\') })
            foreach ($inputPath in $inputPaths) {
                $absoluteInputPath = [System.IO.Path]::GetFullPath($inputPath)
                if (-not $absoluteInputPath.StartsWith($webRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "Refusing to convert art outside the web assets directory: $absoluteInputPath"
                }
            }

            $batchSize = 20
            for ($offset = 0; $offset -lt $inputPaths.Count; $offset += $batchSize) {
                $last = [math]::Min($offset + $batchSize - 1, $inputPaths.Count - 1)
                $batch = @($inputPaths[$offset..$last])
                $previousErrorPreference = $ErrorActionPreference
                try {
                    $ErrorActionPreference = 'Continue'
                    & npm exec --yes --package=sharp-cli -- sharp -i $batch -o $group.Name -f webp -q 82 *> $null
                    $conversionExitCode = $LASTEXITCODE
                }
                finally {
                    $ErrorActionPreference = $previousErrorPreference
                }
                if ($conversionExitCode -ne 0) { throw "sharp-cli failed to convert generated art (exit $conversionExitCode)." }
            }

            $webpPaths = @($inputPaths | ForEach-Object { [System.IO.Path]::ChangeExtension($_, '.webp') })
            foreach ($webpPath in $webpPaths) {
                if (-not (Test-Path -LiteralPath $webpPath -PathType Leaf)) {
                    throw "Expected converted WebP was not created: $webpPath"
                }
            }

            foreach ($inputPath in $inputPaths) {
                if (Test-Path -LiteralPath $inputPath -PathType Leaf) {
                    Remove-Item -LiteralPath $inputPath -Force
                }
            }
            foreach ($row in $groupRows) {
                $row.Output = [System.IO.Path]::ChangeExtension($row.Output, '.webp')
            }
        }
    }
    finally { Pop-Location }
}

function Import-MonsterPortrait([string] $ArtId, [System.IO.FileInfo] $Source) {
    # Always re-import from the source pack. Reusing an asset that already exists keeps a
    # stale or wrongly mapped portrait while the manifest claims the current source produced
    # it, which makes the import non-reproducible.
    $outputPath = Join-Path $MonsterRoot "$ArtId.png"
    $stalePaths = @(
        Get-ChildItem -LiteralPath $MonsterRoot -File |
            Where-Object { $_.BaseName -eq $ArtId -and $_.FullName -ne $outputPath })
    foreach ($stalePath in $stalePaths) {
        Remove-Item -LiteralPath $stalePath.FullName -Force
    }

    Copy-Item -LiteralPath $Source.FullName -Destination $outputPath -Force
    return $outputPath
}

function Get-SourceRelativePath([string] $Path) {
    return $Path.Substring($SourceRoot.Length).TrimStart('\').Replace('\', '/')
}

function Set-DefinitionIconId([object] $Item, [string] $IconId) {
    if ($Item.PSObject.Properties.Name -contains 'iconId') {
        $Item.iconId = $IconId
    }
    else {
        $Item | Add-Member -MemberType NoteProperty -Name 'iconId' -Value $IconId
    }
}

function Set-MonsterArtId([object] $Monster, [string] $ArtId) {
    if ($Monster.PSObject.Properties.Name -contains 'artId') {
        $Monster.artId = $ArtId
    }
    else {
        $Monster | Add-Member -MemberType NoteProperty -Name 'artId' -Value $ArtId
    }
}

function Add-CatalogSourceRow([string] $ContentType, [string] $Name, [string] $Status, [string] $Source, [string] $Cell) {
    $script:SourceCatalogRows.Add([pscustomobject]@{
        ContentType = $ContentType
        Id = ''
        Name = $Name
        Rarity = ''
        Slot = ''
        ArtKey = ''
        Status = $Status
        AssetPath = ''
        Source = $Source
        Cell = $Cell
    })
}

$effectiveItems = Read-ContentRecords (Join-Path $ContentRoot 'items') 'items'
$effectiveMonsters = Read-ContentRecords (Join-Path $ContentRoot 'monsters') 'monsters'
$effectiveSets = Read-ContentRecords (Join-Path $ContentRoot 'sets') 'equipmentSets'
$prompt = Get-Content -LiteralPath $PromptPath -Raw -Encoding UTF8
$importRows = [System.Collections.Generic.List[object]]::new()
$catalogRows = [System.Collections.Generic.List[object]]::new()
$sourceCatalogRows = [System.Collections.Generic.List[object]]::new()
$script:SourceCatalogRows = $sourceCatalogRows
$script:IconAssignments = @{}
$script:MonsterArtAssignments = @{}
$unmatched = [System.Collections.Generic.List[object]]::new()
$mappedArtKeys = @{}
$setSlotsSix = @('Head', 'Shoulders', 'Chest', 'Hands', 'Legs', 'Feet')
$setSlotsEight = @('Head', 'Shoulders', 'Chest', 'Wrist', 'Hands', 'Waist', 'Legs', 'Feet')
$regionPrefixes = @{
    (Get-VisualKey 'Сердце Осквернённой Чащи') = 'SET_HEART_OF_BLIGHTED_GROVE_'
    (Get-VisualKey 'Цитадель Расколотого Ордена') = 'SET_SHATTERED_ORDER_RAID_'
    (Get-VisualKey 'Черный бастион') = 'SET_BLACK_BASTION_'
}

foreach ($sheet in (Get-ChildItem -LiteralPath (Join-Path $SourceRoot 'item set') -Filter '*.png' -File -Recurse | Sort-Object FullName)) {
    $regionKey = Get-VisualKey $sheet.Directory.Name
    if (-not $regionPrefixes.ContainsKey($regionKey)) {
        Add-CatalogSourceRow 'SetSheet' $sheet.BaseName 'SOURCE_HAS_NO_CURRENT_SET' (Get-SourceRelativePath $sheet.FullName) ''
        continue
    }

    $prefix = $regionPrefixes[$regionKey]
    $sheetNameKey = Get-VisualKey $sheet.BaseName
    $matchingSets = @($effectiveSets.Values | Where-Object {
        $_.Data.id.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Get-VisualKey ([string]$_.Data.name)) -eq $sheetNameKey
    })
    if ($matchingSets.Count -ne 1) {
        Add-CatalogSourceRow 'SetSheet' $sheet.BaseName 'SET_NAME_NOT_UNIQUE_OR_NOT_IN_CONTENT' (Get-SourceRelativePath $sheet.FullName) ''
        continue
    }

    $set = $matchingSets[0].Data
    $setItems = @($effectiveItems.Values | Where-Object { $_.Data.setId -eq $set.id })
    $slotOrder = if ($regionKey -eq (Get-VisualKey 'Черный бастион')) { $setSlotsEight } else { $setSlotsSix }
    $grid = Get-Grid $sheet.FullName $slotOrder.Count
    for ($cell = 0; $cell -lt $slotOrder.Count; $cell++) {
        $slotItems = @($setItems | Where-Object { $_.Data.slot -eq $slotOrder[$cell] })
        if ($slotItems.Count -ne 1) {
            Add-CatalogSourceRow 'SetPiece' "$($set.id) / $($slotOrder[$cell])" 'SET_SLOT_NOT_UNIQUE' (Get-SourceRelativePath $sheet.FullName) "$cell ($($grid.Columns)x$($grid.Rows))"
            continue
        }

        $entry = $slotItems[0]
        $item = $entry.Data
        $artKey = if ([string]::IsNullOrWhiteSpace([string]$item.iconId)) { ([string]$item.id).ToLowerInvariant() } else { [string]$item.iconId }
        if ($artKey -notmatch '^[a-z0-9_-]+$') { throw "Unsafe item icon key on $($item.id): $artKey" }
        $mapKey = "item:$artKey"
        if ($mappedArtKeys.ContainsKey($mapKey)) {
            Add-CatalogSourceRow 'SetPiece' $item.name 'ART_KEY_ALREADY_MAPPED' (Get-SourceRelativePath $sheet.FullName) "$cell ($($grid.Columns)x$($grid.Rows))"
            continue
        }

        $mappedArtKeys[$mapKey] = $true
        $destination = Join-Path (Join-Path $ItemRoot 'sets') "$artKey.png"
        Save-Cell $sheet.FullName $destination $grid.Columns $grid.Rows $cell
        $source = Get-SourceRelativePath $sheet.FullName
        $cellInfo = "$cell ($($grid.Columns)x$($grid.Rows))"
        $importRows.Add([pscustomobject]@{ ContentType = 'Item'; Id = $item.id; ArtKey = $artKey; Source = $source; Cell = $cellInfo; Output = ('web/elyndor-web/src/assets/items/sets/' + "$artKey.png") })
        if ([string]::IsNullOrWhiteSpace([string]$item.iconId)) {
            if (-not $script:IconAssignments.ContainsKey($entry.File)) { $script:IconAssignments[$entry.File] = @{} }
            $script:IconAssignments[$entry.File][$item.id] = $artKey
            Set-DefinitionIconId $item $artKey
        }
    }
}

$sourceSections = @(
    @{ Section = '5.1'; File = 'Предметы вне комплектов/Древняя шахта — вещи вне комплектов.png' }
    @{ Section = '5.2'; File = 'Предметы вне комплектов/Цитадель Затмения — вещи вне комплектов.png' }
    @{ Section = '5.3'; File = 'Предметы вне комплектов/Сердце Осквернённой Чащи — вещи вне комплектов.png' }
    @{ Section = '5.4'; File = 'Предметы вне комплектов/Цитадель Расколотого Ордена — вещи вне комплектов.png' }
    @{ Section = '5.5'; File = 'Предметы вне комплектов/Чёрный Бастион — вещи вне комплектов.png' }
    @{ Section = '5.6'; File = 'Предметы вне комплектов/Именные вещи открытого мира — ранний диапазон 1–16.png' }
    @{ Section = '5.7'; File = 'Предметы вне комплектов/Именные вещи открытого мира — средний диапазон 15–29.png' }
    @{ Section = '5.8'; File = 'Предметы вне комплектов/Именные вещи открытого мира — поздний диапазон 30–40.png' }
    @{ Section = '6.1'; File = 'Материал профессий/Снятие шкур — ранние материалы.png' }
    @{ Section = '6.2'; File = 'Материал профессий/Снятие шкур — средние материалы.png' }
    @{ Section = '6.3'; File = 'Материал профессий/Снятие шкур — поздние материалы.png' }
    @{ Section = '6.4'; File = 'Материал профессий/Собирательство — ранние материалы.png' }
    @{ Section = '6.5'; File = 'Материал профессий/Собирательство — средние материалы.png' }
    @{ Section = '6.6'; File = 'Материал профессий/Собирательство — поздние материалы.png' }
    @{ Section = '6.7'; File = 'Материал профессий/Горное дело — ранние материалы.png' }
    @{ Section = '6.8'; File = 'Материал профессий/Горное дело — средние материалы.png' }
    @{ Section = '6.9'; File = 'Материал профессий/Горное дело — поздние материалы.png' }
    @{ Section = '6.10'; File = 'Материал профессий/Группа материалов для кожевничества.png' }
    @{ Section = '7.1'; File = 'Материалы, не используемых в профессиях/Охотничьи и звериные трофеи.png' }
    @{ Section = '7.2'; File = 'Материалы, не используемых в профессиях/Тёмные и магические реагенты.png' }
    @{ Section = '7.3'; File = 'Материалы, не используемых в профессиях/Боссовые трофеи и особые находки.png' }
)

foreach ($sectionInfo in $sourceSections) {
    $sheetPath = Join-Path $SourceRoot $sectionInfo.File
    if (-not (Test-Path -LiteralPath $sheetPath)) { throw "Expected item sheet is missing: $sheetPath" }
    $names = Get-SectionNames $prompt $sectionInfo.Section
    $grid = Get-Grid $sheetPath $names.Count
    for ($cell = 0; $cell -lt $names.Count; $cell++) {
        $name = $names[$cell]
        $key = Get-VisualKey $name
        $matches = @($effectiveItems.Values | Where-Object { (Get-VisualKey ([string]$_.Data.name)) -eq $key })
        $source = Get-SourceRelativePath $sheetPath
        $cellInfo = "$cell ($($grid.Columns)x$($grid.Rows))"
        if ($matches.Count -ne 1) {
            $status = if ($matches.Count -eq 0) { 'NO_EXACT_RUNTIME_ITEM' } else { 'AMBIGUOUS_RUNTIME_ITEM_NAME' }
            Add-CatalogSourceRow 'SourceItem' $name $status $source $cellInfo
            continue
        }

        $entry = $matches[0]
        $item = $entry.Data
        $artKey = if ([string]::IsNullOrWhiteSpace([string]$item.iconId)) { ([string]$item.id).ToLowerInvariant() } else { [string]$item.iconId }
        if ($artKey -notmatch '^[a-z0-9_-]+$') { throw "Unsafe item icon key on $($item.id): $artKey" }
        $mapKey = "item:$artKey"
        if ($mappedArtKeys.ContainsKey($mapKey)) {
            Add-CatalogSourceRow 'SourceItem' $name 'DUPLICATE_ART_SOURCE_SKIPPED' $source $cellInfo
            continue
        }

        $mappedArtKeys[$mapKey] = $true
        $destination = Join-Path (Join-Path $ItemRoot 'imported') "$artKey.png"
        Save-Cell $sheetPath $destination $grid.Columns $grid.Rows $cell
        $importRows.Add([pscustomobject]@{ ContentType = 'Item'; Id = $item.id; ArtKey = $artKey; Source = $source; Cell = $cellInfo; Output = ('web/elyndor-web/src/assets/items/imported/' + "$artKey.png") })
        if ([string]::IsNullOrWhiteSpace([string]$item.iconId)) {
            if (-not $script:IconAssignments.ContainsKey($entry.File)) { $script:IconAssignments[$entry.File] = @{} }
            $script:IconAssignments[$entry.File][$item.id] = $artKey
            Set-DefinitionIconId $item $artKey
        }
    }
}

$enemySources = Get-ChildItem -LiteralPath (Join-Path $SourceRoot 'Враги') -Filter '*.png' -File -Recurse
$namesByArtId = @{}
foreach ($monsterEntry in $effectiveMonsters.Values) {
    $monster = $monsterEntry.Data
    if ([string]::IsNullOrWhiteSpace([string]$monster.artId)) { continue }
    if (-not $namesByArtId.ContainsKey([string]$monster.artId)) { $namesByArtId[[string]$monster.artId] = @{} }
    $display = if ([string]::IsNullOrWhiteSpace([string]$monster.displayName)) { [string]$monster.name } else { [string]$monster.displayName }
    $namesByArtId[[string]$monster.artId][(Get-VisualKey $display)] = $true
}

foreach ($enemySource in $enemySources) {
    $nameKey = Get-VisualKey $enemySource.BaseName
    $matchingMonsters = @($effectiveMonsters.Values | Where-Object {
        $display = if ([string]::IsNullOrWhiteSpace([string]$_.Data.displayName)) { [string]$_.Data.name } else { [string]$_.Data.displayName }
        (Get-VisualKey $display) -eq $nameKey
    })
    if ($matchingMonsters.Count -eq 0) {
        Add-CatalogSourceRow 'EnemyPortrait' $enemySource.BaseName 'NO_EXACT_RUNTIME_MONSTER' (Get-SourceRelativePath $enemySource.FullName) ''
        continue
    }

    $requiresDedicatedArtKey = @($matchingMonsters | Where-Object {
        $artId = [string]$_.Data.artId
        [string]::IsNullOrWhiteSpace($artId) -or
        -not $namesByArtId.ContainsKey($artId) -or
        $namesByArtId[$artId].Count -gt 1
    }).Count -gt 0

    if ($requiresDedicatedArtKey) {
        $artId = Get-MonsterSourceArtKey $enemySource.BaseName
        if ($namesByArtId.ContainsKey($artId) -and $namesByArtId[$artId].Count -gt 0) {
            throw "Dedicated portrait key '$artId' already belongs to a different content art mapping."
        }

        foreach ($monsterEntry in $matchingMonsters) {
            $monster = $monsterEntry.Data
            Set-MonsterArtId $monster $artId
            if (-not $script:MonsterArtAssignments.ContainsKey($monsterEntry.File)) {
                $script:MonsterArtAssignments[$monsterEntry.File] = @{}
            }
            $script:MonsterArtAssignments[$monsterEntry.File][[string]$monster.id] = $artId
        }

        $outputPath = Import-MonsterPortrait $artId $enemySource
        $source = Get-SourceRelativePath $enemySource.FullName
        foreach ($monsterEntry in $matchingMonsters) {
            $importRows.Add([pscustomobject]@{
                ContentType = 'Monster'; Id = $monsterEntry.Data.id; ArtKey = $artId; Source = $source; Cell = ''
                Output = ('web/elyndor-web/src/assets/monsters/' + (Split-Path -Leaf $outputPath))
            })
        }
        continue
    }

    foreach ($monsterEntry in $matchingMonsters) {
        $monster = $monsterEntry.Data
        $artId = [string]$monster.artId
        $outputPath = Import-MonsterPortrait $artId $enemySource
        $source = Get-SourceRelativePath $enemySource.FullName
        $importRows.Add([pscustomobject]@{ ContentType = 'Monster'; Id = $monster.id; ArtKey = $artId; Source = $source; Cell = ''; Output = ('web/elyndor-web/src/assets/monsters/' + (Split-Path -Leaf $outputPath)) })
    }
}

Convert-ImportedArtToWebp

foreach ($filePath in $script:IconAssignments.Keys) {
    $raw = [System.IO.File]::ReadAllText($filePath, $Utf8)
    foreach ($itemId in $script:IconAssignments[$filePath].Keys) {
        $iconId = [string]$script:IconAssignments[$filePath][$itemId]
        $pattern = '(?m)^(?<indent>\s*)"id":\s*"' + [regex]::Escape([string]$itemId) + '",\s*$'
        $matches = [regex]::Matches($raw, $pattern)
        if ($matches.Count -ne 1) { throw "Could not uniquely locate item $itemId in $filePath to set its iconId." }
        $lineEnding = if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" }
        $match = $matches[0]
        $indent = $match.Groups['indent'].Value
        $replacement = $match.Value + $lineEnding + $indent + '"iconId":  "' + $iconId + '",'
        $raw = $raw.Remove($match.Index, $match.Length).Insert($match.Index, $replacement)
    }
    [System.IO.File]::WriteAllText($filePath, $raw, $Utf8NoBom)
}

foreach ($filePath in $script:MonsterArtAssignments.Keys) {
    $raw = [System.IO.File]::ReadAllText($filePath, $Utf8)
    foreach ($monsterId in $script:MonsterArtAssignments[$filePath].Keys) {
        $idPattern = '(?m)^(?<indent>[ \t]*)"id":\s*"' + [regex]::Escape([string]$monsterId) + '",?\s*$'
        $idMatches = [regex]::Matches($raw, $idPattern)
        if ($idMatches.Count -ne 1) { throw "Could not uniquely locate monster $monsterId in $filePath to set its artId." }

        $idMatch = $idMatches[0]
        $nextIdPattern = '(?m)^' + [regex]::Escape($idMatch.Groups['indent'].Value) + '"id":\s*"'
        $nextIdMatch = ([regex]::new($nextIdPattern)).Match($raw, $idMatch.Index + $idMatch.Length)
        $recordEnd = if ($nextIdMatch.Success) { $nextIdMatch.Index } else { $raw.Length }
        $recordLength = $recordEnd - $idMatch.Index
        $record = $raw.Substring($idMatch.Index, $recordLength)
        $artPattern = '(?m)^(?<indent>[ \t]*)"artId":[ \t]*"(?<value>[^"]*)"(?<comma>,?)[ \t]*$'
        $artMatches = [regex]::Matches($record, $artPattern)
        if ($artMatches.Count -ne 1) { throw "Could not uniquely locate artId for monster $monsterId in $filePath." }

        $artMatch = $artMatches[0]
        $replacement = $artMatch.Groups['indent'].Value + '"artId":  "' +
            $script:MonsterArtAssignments[$filePath][$monsterId] + '"' + $artMatch.Groups['comma'].Value
        $raw = $raw.Remove($idMatch.Index + $artMatch.Index, $artMatch.Length).Insert($idMatch.Index + $artMatch.Index, $replacement)
    }
    [System.IO.File]::WriteAllText($filePath, $raw, $Utf8NoBom)
}

$itemAssetByKey = @{}
foreach ($file in (Get-ChildItem -LiteralPath $ItemRoot -File -Recurse)) { $itemAssetByKey[$file.BaseName] = $file.FullName }
$monsterAssetByKey = @{}
foreach ($file in (Get-ChildItem -LiteralPath $MonsterRoot -File)) { $monsterAssetByKey[$file.BaseName] = $file.FullName }

foreach ($entry in $effectiveItems.Values) {
    $item = $entry.Data
    $artKey = [string]$item.iconId
    $status = if ([string]::IsNullOrWhiteSpace($artKey)) { 'NO_ICON_ID' } elseif ($itemAssetByKey.ContainsKey($artKey)) { 'DIRECT_ASSET' } else { 'MISSING_ASSET' }
    $mapping = @($importRows | Where-Object { $_.ContentType -eq 'Item' -and $_.Id -eq $item.id } | Select-Object -First 1)
    $assetPath = if ($itemAssetByKey.ContainsKey($artKey)) { $itemAssetByKey[$artKey].Substring($RepositoryRoot.Length + 1).Replace('\', '/') } else { '' }
    $catalogRows.Add([pscustomobject]@{
        ContentType = 'Item'; Id = $item.id; Name = Repair-DisplayText ([string]$item.name); Rarity = [string]$item.rarity
        Slot = [string]$item.slot; ArtKey = $artKey; Status = $status; AssetPath = $assetPath
        Source = if ($mapping.Count) { $mapping[0].Source } else { '' }
        Cell = if ($mapping.Count) { $mapping[0].Cell } else { '' }
    })
}

foreach ($entry in $effectiveMonsters.Values) {
    $monster = $entry.Data
    $artKey = [string]$monster.artId
    $status = if ([string]::IsNullOrWhiteSpace($artKey)) { 'NO_ART_ID' } elseif ($monsterAssetByKey.ContainsKey($artKey)) { 'DIRECT_ASSET' } else { 'MISSING_DIRECT_ASSET' }
    $display = if ([string]::IsNullOrWhiteSpace([string]$monster.displayName)) { [string]$monster.name } else { [string]$monster.displayName }
    $mapping = @($importRows | Where-Object { $_.ContentType -eq 'Monster' -and $_.Id -eq $monster.id } | Select-Object -First 1)
    $assetPath = if ($monsterAssetByKey.ContainsKey($artKey)) { $monsterAssetByKey[$artKey].Substring($RepositoryRoot.Length + 1).Replace('\', '/') } else { '' }
    $catalogRows.Add([pscustomobject]@{
        ContentType = 'Monster'; Id = $monster.id; Name = Repair-DisplayText $display; Rarity = [string]$monster.rank
        Slot = ''; ArtKey = $artKey; Status = $status; AssetPath = $assetPath
        Source = if ($mapping.Count) { $mapping[0].Source } else { '' }
        Cell = if ($mapping.Count) { $mapping[0].Cell } else { '' }
    })
}

$catalogRows.AddRange($sourceCatalogRows)
$catalogRows = @($catalogRows | Sort-Object ContentType, Status, Id, Name)
$csv = $catalogRows | ConvertTo-Csv -NoTypeInformation
[System.IO.File]::WriteAllLines($CatalogPath, $csv, $Utf8Bom)

$summary = $catalogRows | Group-Object ContentType, Status | Sort-Object Name
$reportLines = [System.Collections.Generic.List[string]]::new()
$reportLines.Add('# Каталог соответствия игрового контента и изображений')
$reportLines.Add('')
$reportLines.Add("Снимок собран импортёром из текущих `content/items`, `content/monsters` и файлов `web/elyndor-web/src/assets`. Всего предметов: $($effectiveItems.Count); мобов: $($effectiveMonsters.Count).")
$reportLines.Add('')
$reportLines.Add('`DIRECT_ASSET` — файл найден по canonical `iconId` / `artId`; `MISSING_ASSET` и `MISSING_DIRECT_ASSET` перечисляют контент без собственного файла. Для предметов UI использует существующий glyph fallback. Для мобов без direct asset может отображаться существующий semantic/location fallback, поэтому он не гарантирует точный портрет конкретного вида. Неоднозначные источники не назначаются автоматически.')
$reportLines.Add('')
$reportLines.Add('Полный список по каждому шаблону, файлу, исходному листу и ячейке: [art-coverage-catalog.csv](art-coverage-catalog.csv).')
$reportLines.Add('')
$reportLines.Add('| Тип и статус | Количество |')
$reportLines.Add('| --- | ---: |')
foreach ($group in $summary) { $reportLines.Add("| $($group.Name) | $($group.Count) |") }
[System.IO.File]::WriteAllLines($ReportPath, $reportLines, $Utf8NoBom)

$importManifestPath = Join-Path $RepositoryRoot 'tools\assets\content-art-import-manifest.csv'
$importCsv = $importRows | Sort-Object ContentType, Id | ConvertTo-Csv -NoTypeInformation
[System.IO.File]::WriteAllLines($importManifestPath, $importCsv, $Utf8Bom)

Write-Host "Imported $($importRows.Count) explicit source mappings."
Write-Host "Wrote art catalog: $CatalogPath"
Write-Host "Wrote art report: $ReportPath"
Write-Host "Wrote source mapping: $importManifestPath"
