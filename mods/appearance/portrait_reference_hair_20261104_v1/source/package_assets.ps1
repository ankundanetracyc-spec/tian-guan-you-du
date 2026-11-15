$ErrorActionPreference = 'Stop'
$bundleRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$bundleZip = Join-Path $bundleRoot 'portrait_hair_three_sets_1024x1536.zip'
if (Test-Path -LiteralPath $bundleZip) { throw 'Final ZIP already exists; choose a new version name before packaging again.' }
$bundleFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $bundleRoot 'female') -Recurse -File -Filter '*.png'
    Get-ChildItem -LiteralPath (Join-Path $bundleRoot 'preview') -File -Filter '*.png' | Where-Object { $_.Name -notmatch 'front0(75|85)' }
    Get-Item -LiteralPath (Join-Path $bundleRoot 'qa/metadata.json')
)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$bundleArchive = [System.IO.Compression.ZipFile]::Open($bundleZip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($bundleFile in ($bundleFiles | Sort-Object FullName)) {
        $bundleRelative = [System.IO.Path]::GetRelativePath($bundleRoot, $bundleFile.FullName).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($bundleArchive, $bundleFile.FullName, $bundleRelative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $bundleArchive.Dispose() }
$bundleArchive = [System.IO.Compression.ZipFile]::OpenRead($bundleZip)
try {
    $bundleArchive.Entries | ForEach-Object { [PSCustomObject]@{ Entry = $_.FullName; Size = $_.Length } }
} finally { $bundleArchive.Dispose() }
Get-Item -LiteralPath $bundleZip | Select-Object FullName, Length
