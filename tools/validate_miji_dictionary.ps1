param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string[]]$FocusPaths = @()
)

$ErrorActionPreference = 'Stop'

function Text([int[]]$CodePoints) {
    return -join ($CodePoints | ForEach-Object { [char]$_ })
}

$dictionaryRoot = Join-Path $ProjectRoot ((Text @(0x0064,0x006F,0x0063,0x0073)) + '\' + (Text @(0x79D8,0x7C4D,0x8BCD,0x5178)))
$errors = [System.Collections.Generic.List[string]]::new()
$badTalent = (Text @(0x624D,0x667A))
$badIntelligence = (Text @(0x667A,0x529B))
$zeroExhaust = (Text @(0x96F6,0x6D88,0x8017))
$noExhaust = (Text @(0x65E0,0x6D88,0x8017))
$heartType = (Text @(0x7C7B,0x578B,0xFF1A,0x5FC3,0x6CD5))
$costLabel = (Text @(0x8017,0x8D39,0xFF1A))
$typeLabel = (Text @(0x7C7B,0x578B,0xFF1A))
$keywordLabel = (Text @(0x5173,0x952E,0x8BCD,0xFF1A))
$tagLabel = (Text @(0x6807,0x7B7E,0xFF1A))
$keywordSeparatorPattern = '[' + (Text @(0x3001)) + (Text @(0xFF0C)) + ',/]'
$targetLabel = (Text @(0x76EE,0x6807))
$exhaustKeyword = (Text @(0x5173,0x952E,0x8BCD,0xFF1A)) + '.*' + (Text @(0x6D88,0x8017))
$specFileName = (Text @(0x79D8,0x7C4D,0x89C4,0x8303)) + '.md'
$pseudoCounterType = (Text @(0x5E94,0x5BF9,0x724C))
$pseudoReactionType = (Text @(0x53CD,0x5E94,0x724C))
$cardTypes = @(
    (Text @(0x653B,0x4F10)), (Text @(0x9041,0x8EAB)),
    (Text @(0x672F,0x8BC0)), (Text @(0x5B88,0x5FA1)),
    (Text @(0x5FC3,0x6CD5)), (Text @(0x4E1A,0x969C))
)
$legacyCardTypes = @(
    (Text @(0x653B,0x4F10,0x724C)), (Text @(0x9041,0x8EAB,0x724C)),
    (Text @(0x672F,0x8BC0,0x724C)), (Text @(0x5B88,0x5FA1,0x724C)),
    (Text @(0x5FC3,0x6CD5,0x724C)), (Text @(0x4E1A,0x969C,0x724C))
)
$allCardTypes = @($cardTypes + $legacyCardTypes | Select-Object -Unique)
$passiveType = (Text @(0x5FC3,0x6CD5))

if (-not (Test-Path -LiteralPath $dictionaryRoot)) {
    throw "Dictionary directory not found: $dictionaryRoot"
}

$documentFiles = Get-ChildItem -LiteralPath $dictionaryRoot -Recurse -File |
    Where-Object { $_.Extension -in '.md', '.drawio' }
$focusSet = $null
if ($FocusPaths.Count -gt 0) {
    $focusSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($focusPath in $FocusPaths) {
        $resolvedPath = if ([System.IO.Path]::IsPathRooted($focusPath)) { $focusPath } else { Join-Path $ProjectRoot $focusPath }
        [void]$focusSet.Add([System.IO.Path]::GetFullPath($resolvedPath))
    }
    $documentFiles = @($documentFiles | Where-Object { $focusSet.Contains([System.IO.Path]::GetFullPath($_.FullName)) })
}
$cardDictionaryRoot = Join-Path $ProjectRoot ((Text @(0x0064,0x006F,0x0063,0x0073)) + '\' + (Text @(0x5361,0x724C,0x8BCD,0x5178)))
$allowedKeywords = @(
    (Text @(0x65E0)),
    (Text @(0x6D88,0x8017)),
    (Text @(0x56FA,0x6709)),
    (Text @(0x5E94,0x5BF9)),
    (Text @(0x4E58,0x52BF))
)
$buildKeywords = @(
    (Text @(0x751F,0x547D)), (Text @(0x95EA,0x907F)), (Text @(0x56DE,0x590D)),
    (Text @(0x7CBE,0x529B)), (Text @(0x589E,0x4F24)), (Text @(0x4F24,0x52BF)),
    (Text @(0x9632,0x5FA1)), (Text @(0x653B,0x9632,0x8054,0x52A8)),
    (Text @(0x6CD5,0x529B)), (Text @(0x5FAA,0x73AF)), (Text @(0x7206,0x53D1))
)
$combatMechanicKeywords = @(
    (Text @(0x6C14,0x8840)), (Text @(0x62A4,0x76FE)), (Text @(0x5316,0x89E3)), (Text @(0x5468,0x5929)),
    (Text @(0x6563,0x8840)), (Text @(0x7A7F,0x76FE))
)
$allowedKeywords += $buildKeywords + $combatMechanicKeywords

# Legacy mechanic keywords and the shared build keywords are accepted in keyword
# lines. The older tag line remains for other systems' faction/element labels.
$mechanicTags = @(
    (Text @(0x65E0)), (Text @(0x6D88,0x8017)), (Text @(0x56FA,0x6709)),
    (Text @(0x5E94,0x5BF9)), (Text @(0x4E58,0x52BF)), (Text @(0x91CD,0x51FB)),
    (Text @(0x4FDD,0x7559)), (Text @(0x865A,0x65E0)), (Text @(0x5F03,0x9876)),
    (Text @(0x89C2,0x52BF)), (Text @(0x5316,0x89E3)), (Text @(0x6C14,0x8840)),
    (Text @(0x8EAB,0x6CD5)), (Text @(0x6E38,0x9AB8)), (Text @(0x8840,0x7089)),
    (Text @(0x4F24,0x52BF)), (Text @(0x501F,0x529B))
)
$mechanicTags += $buildKeywords + $combatMechanicKeywords
$keywordFiles = @($documentFiles | Where-Object { $_.Name -ne $specFileName })
if ($FocusPaths.Count -eq 0 -and (Test-Path -LiteralPath $cardDictionaryRoot)) {
    $keywordFiles += Get-ChildItem -LiteralPath $cardDictionaryRoot -Recurse -File |
        Where-Object { $_.Extension -in '.drawio', '.md' -and -not $_.Name.StartsWith('_') }
}
foreach ($file in $keywordFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    if ($file.Extension -eq '.drawio') {
        [xml]$diagram = $content
        $content = (@($diagram.SelectNodes('//mxCell[@value]') | ForEach-Object { $_.GetAttribute('value') }) -join "`n")
    }
    $keywordMatches = [regex]::Matches($content, [regex]::Escape($keywordLabel) + '([^<&\r\n]+)')
    foreach ($keywordMatch in $keywordMatches) {
        $keywordText = $keywordMatch.Groups[1].Value.Trim()
        foreach ($keyword in ($keywordText -split $keywordSeparatorPattern)) {
            $normalized = $keyword.Trim().TrimEnd([char[]]@((Text @(0x3002)), (Text @(0xFF1B)), ';'))
            if ([string]::IsNullOrWhiteSpace($normalized) -or $normalized -notin $allowedKeywords) {
                $errors.Add("$($file.FullName): invalid card keyword '$keywordText'.")
                break
            }
        }
    }
    $tagMatches = [regex]::Matches($content, [regex]::Escape($tagLabel) + '([^<&\r\n]+)')
    foreach ($tagMatch in $tagMatches) {
        $tagText = $tagMatch.Groups[1].Value.Trim()
        foreach ($tag in ($tagText -split $keywordSeparatorPattern)) {
            $normalized = $tag.Trim().TrimEnd([char[]]@((Text @(0x3002)), (Text @(0xFF1B)), ';'))
            if ([string]::IsNullOrWhiteSpace($normalized) -or $normalized -in $mechanicTags) {
                $errors.Add("$($file.FullName): mechanic or build keyword '$normalized' cannot be used in the legacy faction/element tag line.")
                break
            }
        }
    }
}

# Round timing is valid under the current combat rules. Only reject wording
# that leaves the relevant action or settlement point unspecified.
$forbiddenTimingTerms = @(
    (Text @(0x5DF2,0x884C,0x52A8))
)
foreach ($file in $keywordFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    foreach ($term in $forbiddenTimingTerms) {
        if ($content.Contains($term)) {
            $errors.Add("$($file.FullName): ambiguous round-based timing term '$term' found.")
        }
    }
}

# Validate every concrete card cell rather than only the diagram-level text.
# This keeps a complete card header mandatory in both dictionaries.
$allDrawioFiles = @($documentFiles | Where-Object { $_.Extension -eq '.drawio' })
if ($FocusPaths.Count -eq 0 -and (Test-Path -LiteralPath $cardDictionaryRoot)) {
    $allDrawioFiles += Get-ChildItem -LiteralPath $cardDictionaryRoot -Recurse -File -Filter '*.drawio'
}
$allDrawioFiles = $allDrawioFiles | Sort-Object FullName -Unique
$resourceCostPattern = '(?:' + (Text @(0x4F53,0x529B)) + '|' + (Text @(0x6CD5,0x529B)) + ')\s*\d+'
$costHeaderPattern = '^' + [regex]::Escape($costLabel) + '\s*' + $resourceCostPattern + '(?:\s*[、，,]\s*' + $resourceCostPattern + ')?\s*$'
$costHeaderLegacyPattern = '^' + [regex]::Escape($costLabel) + (Text @(0x4F53,0x529B)) + '\s*\d+.*' + (Text @(0x6CD5,0x529B)) + '\s*\d+.*' + (Text @(0x8C03,0x5EA6,0x6CD5,0x529B)) + '\s*\d+.*' + (Text @(0x7075,0x8BC6)) + '\s*\d+$'
$targetRangeLabel = (Text @(0x76EE,0x6807,0x4E0E,0x5C04,0x7A0B,0xFF1A))
$simpleTargetLabel = (Text @(0x76EE,0x6807,0xFF1A))
$damageFormulaLabel = (Text @(0x4F24,0x5BB3,0x516C,0x5F0F,0xFF1A))
$calculationFormulaLabel = (Text @(0x8BA1,0x7B97,0x516C,0x5F0F,0xFF1A))
$damageTypeLabel = (Text @(0x4F24,0x5BB3,0x7C7B,0x578B,0xFF1A))

foreach ($file in $allDrawioFiles) {
    try {
        [xml]$xml = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        foreach ($cell in @($xml.SelectNodes('//mxCell[@value]'))) {
            $lines = @((($cell.GetAttribute('value').Replace('&lt;br&gt;', "`n").Replace('<br>', "`n") -replace '<[^>]+>', '') -split "`r?`n") |
                ForEach-Object { $_.Trim() })
            for ($index = 0; $index -lt $lines.Count; $index++) {
                if (-not $lines[$index].StartsWith($typeLabel)) { continue }
                $typeValue = $lines[$index].Substring($typeLabel.Length)
                if ($typeValue -notin $allCardTypes) {
                    $errors.Add("$($file.FullName): invalid card type '$typeValue'.")
                    continue
                }
                if ($index + 1 -ge $lines.Count -or (-not $lines[$index + 1].StartsWith($tagLabel) -and -not $lines[$index + 1].StartsWith($keywordLabel))) {
                    $errors.Add("$($file.FullName): card type must be followed by a dedicated tag line (or a legacy keyword line during migration).")
                    continue
                }
                if ($typeValue -eq $passiveType) {
                    $nextType = $lines.Count
                    for ($scan = $index + 1; $scan -lt $lines.Count; $scan++) {
                        if ($lines[$scan].StartsWith($typeLabel)) { $nextType = $scan; break }
                    }
                    if (($lines[$index..($nextType - 1)] -join "`n") -match ('(?m)^' + [regex]::Escape($costLabel) + '|^' + [regex]::Escape($simpleTargetLabel) + '|^' + [regex]::Escape($targetRangeLabel) + '|^' + (Text @(0x7075,0x8BC6,0x5360,0x7528)))) {
                        $errors.Add("$($file.FullName): heart passive must not declare cost, spirit occupancy, or target fields.")
                    }
                    continue
                }
                if ($index + 2 -ge $lines.Count -or ($lines[$index + 2] -notmatch $costHeaderPattern -and $lines[$index + 2] -notmatch $costHeaderLegacyPattern)) {
                    $errors.Add("$($file.FullName): card tag line must be followed by a cost line listing the stamina/mana costs used.")
                    continue
                }
                if ($index + 3 -ge $lines.Count -or (-not $lines[$index + 3].StartsWith($targetRangeLabel) -and -not $lines[$index + 3].StartsWith($simpleTargetLabel))) {
                    $errors.Add("$($file.FullName): card cost must be followed by a target line.")
                    continue
                }
                if ($typeValue -eq $cardTypes[0] -or $typeValue -eq $legacyCardTypes[0]) {
                    $cardTail = $lines[$index..($lines.Count - 1)] -join "`n"
                    if (-not $cardTail.Contains($damageTypeLabel) -or (-not $cardTail.Contains($damageFormulaLabel) -and -not $cardTail.Contains($calculationFormulaLabel) -and $cardTail -notmatch '(?:^|[\s。；，、])[A-Z]\s*=')) {
                        $errors.Add("$($file.FullName): attack card lacks damage type or damage formula.")
                    }
                }
            }
        }
    }
    catch {
        $errors.Add("$($file.FullName): draw.io XML cannot be parsed: $($_.Exception.Message)")
    }
}

foreach ($file in $documentFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    if ($file.Name -ne $specFileName -and ($content.Contains($badTalent) -or $content.Contains($badIntelligence))) {
        $errors.Add("$($file.FullName): forbidden intelligence display name found.")
    }

    if ($file.Name -ne $specFileName -and ($content.Contains($zeroExhaust) -or $content.Contains($noExhaust))) {
        $errors.Add("$($file.FullName): ambiguous zero-resource wording found.")
    }

    $pseudoTypePattern = '(?m)' + [regex]::Escape($typeLabel) + '(?:' + [regex]::Escape($pseudoCounterType) + '|' + [regex]::Escape($pseudoReactionType) + ')'
    if ($file.Name -ne $specFileName -and [regex]::IsMatch($content, $pseudoTypePattern)) {
        $errors.Add("$($file.FullName): a keyword was incorrectly written as a card type.")
    }

    if ($file.Extension -eq '.drawio') {
        try {
            $xml = [xml]$content
            if ($null -eq $xml.mxfile) { $errors.Add("$($file.FullName): invalid draw.io root element.") }
            foreach ($cell in @($xml.SelectNodes("//mxCell[contains(@id, '_cards')]"))) {
                $lines = (($cell.GetAttribute('value').Replace('&lt;br&gt;', "`n").Replace('<br>', "`n") -replace '<[^>]+>', '') -split "`r?`n")
                for ($index = 0; $index -lt $lines.Count; $index++) {
                    foreach ($cardType in $allCardTypes) {
                        if ($lines[$index] -ne ($typeLabel + $cardType)) { continue }
                        if ($index + 1 -ge $lines.Count -or (-not $lines[$index + 1].StartsWith($tagLabel) -and -not $lines[$index + 1].StartsWith($keywordLabel))) {
                            $errors.Add("$($file.FullName): card type must be followed by a dedicated tag line (or a legacy keyword line during migration).")
                            continue
                        }
                        if ($cardType -eq $passiveType) {
                            continue
                        }
                        if ($index + 2 -ge $lines.Count -or ($lines[$index + 2] -notmatch $costHeaderPattern -and $lines[$index + 2] -notmatch $costHeaderLegacyPattern)) {
                            $errors.Add("$($file.FullName): card tag line must be followed by a cost line listing the stamina/mana costs used.")
                            continue
                        }
                        if ($index + 3 -ge $lines.Count -or (-not $lines[$index + 3].StartsWith($targetLabel) -and -not $lines[$index + 3].StartsWith($targetRangeLabel))) {
                            $errors.Add("$($file.FullName): card cost must be followed by a dedicated target line.")
                        }
                    }
                }
            }
        }
        catch {
            $errors.Add("$($file.FullName): draw.io XML cannot be parsed: $($_.Exception.Message)")
        }
    }
}

$skipNames = @(
    $specFileName,
    '_' + (Text @(0x79D8,0x7C4D,0x6A21,0x677F)) + '.md',
    (Text @(0x5251,0x4FEE,0x4EC5,0x8F85,0x4FEE,0x79D8,0x7C4D,0x603B,0x89C8)) + '.md'
)
$markdownFiles = $documentFiles | Where-Object { $_.Extension -eq '.md' -and $_.Name -notin $skipNames }
$sectionPattern = '^###\s+(.+)$'
$cardMarker = (Text @(0x724C))
$costPattern = (Text @(0x8017,0x8D39,0xFF1A,0x7CBE,0x529B)) + '\s*\d+.*' + (Text @(0x6CD5,0x529B)) + '\s*\d+.*' + (Text @(0x8C03,0x5EA6,0x6CD5,0x529B)) + '\s*\d+.*' + (Text @(0x7075,0x8BC6)) + '\s*\d+'

foreach ($file in $markdownFiles) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName, [System.Text.Encoding]::UTF8)
    $section = ''
    $isHeartCard = $false
    $hasCost = $false
    $hasTarget = $false
    $hasExhaustKeyword = $false
    $hasValidType = $false
    $checkSection = {
        if ([string]::IsNullOrWhiteSpace($section)) { return }
        if ($isHeartCard -and $hasExhaustKeyword) {
            $errors.Add("$($file.FullName) [$section]: heart card must not use exhaust keyword.")
        }
        if ($section.Contains($cardMarker) -and -not $hasCost) {
            $errors.Add("$($file.FullName) [$section]: card lacks the four-field cost header.")
        }
        if ($section.Contains($cardMarker) -and -not $hasTarget) {
            $errors.Add("$($file.FullName) [$section]: card lacks an explicit target line.")
        }
        if ($section.Contains($cardMarker) -and -not $hasValidType) {
            $errors.Add("$($file.FullName) [$section]: card type is not one of the SkillType enum display names.")
        }
    }

    foreach ($line in $lines) {
        if ($line -match $sectionPattern) {
            & $checkSection
            $section = $Matches[1]
            $isHeartCard = $false
            $hasCost = $false
            $hasTarget = $false
            $hasExhaustKeyword = $false
            $hasValidType = $false
            continue
        }
        if ($line.Contains($heartType)) { $isHeartCard = $true }
        foreach ($cardType in $allCardTypes) {
            if ($line.Contains((Text @(0x7C7B,0x578B,0xFF1A)) + $cardType)) { $hasValidType = $true }
        }
        if ($line -match $costPattern) { $hasCost = $true }
        if ($line.Contains($targetLabel)) { $hasTarget = $true }
        if ($line -match $exhaustKeyword) { $hasExhaustKeyword = $true }
    }
    & $checkSection
}

if ($errors.Count -gt 0) {
    Write-Host 'Miji dictionary validation failed:' -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "- $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'Miji dictionary validation passed.' -ForegroundColor Green
