param(
    [string]$Path = (Join-Path $PSScriptRoot '..\textures\characters\c1_spine_ingame_rig_v6\runtime\c1_spine_unified.skel')
)

$bytes = [IO.File]::ReadAllBytes($Path)
$from = [Text.Encoding]::ASCII.GetBytes('3.8.75')
$to = [Text.Encoding]::ASCII.GetBytes('3.8.99')
$matches = @()

for ($i = 0; $i -le $bytes.Length - $from.Length; $i++) {
    $match = $true
    for ($j = 0; $j -lt $from.Length; $j++) {
        if ($bytes[$i + $j] -ne $from[$j]) {
            $match = $false
            break
        }
    }
    if ($match) {
        $matches += $i
    }
}

if ($matches.Count -ne 1) {
    throw "Expected exactly one Spine version marker '3.8.75' in '$Path', found $($matches.Count)."
}

for ($j = 0; $j -lt $to.Length; $j++) {
    $bytes[$matches[0] + $j] = $to[$j]
}

[IO.File]::WriteAllBytes($Path, $bytes)
Write-Output "Patched Spine binary export version marker at byte offset $($matches[0]): 3.8.75 -> 3.8.99"
