param(
    [string]$CaptureDirectory = 'E:/stacklands/tmp/ink_sequence_capture',
    [string]$OutputPath = 'E:/stacklands/docs/美术规范/局内水墨序列帧_20261015/preview_motion.gif',
    [int]$CropX = 0,
    [int]$CropY = 0,
    [int]$CropWidth = 0,
    [int]$CropHeight = 0
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
$encoder = [Windows.Media.Imaging.GifBitmapEncoder]::new()
$captureFiles = Get-ChildItem -LiteralPath $CaptureDirectory -Filter 'motion_*.png' | Sort-Object Name
$frameIndex = 0
foreach ($captureFile in $captureFiles) {
    $bitmap = [Windows.Media.Imaging.BitmapImage]::new()
    $bitmap.BeginInit()
    $bitmap.CacheOption = [Windows.Media.Imaging.BitmapCacheOption]::OnLoad
    $bitmap.UriSource = [Uri]::new($captureFile.FullName)
    if ($CropWidth -le 0 -or $CropHeight -le 0) { $bitmap.DecodePixelWidth = 960 }
    $bitmap.EndInit()
    $bitmap.Freeze()
    $encodedFrame = $bitmap
    if ($CropWidth -gt 0 -and $CropHeight -gt 0) {
        $region = [Windows.Media.Imaging.CroppedBitmap]::new($bitmap, [Windows.Int32Rect]::new($CropX,$CropY,$CropWidth,$CropHeight))
        $region.Freeze()
        $encodedFrame = $region
    }
    $metadata = [Windows.Media.Imaging.BitmapMetadata]::new('gif')
    $metadata.SetQuery('/grctlext/Delay', [UInt16]$(if ($frameIndex % 3 -eq 2) { 4 } else { 3 }))
    $metadata.SetQuery('/grctlext/Disposal', [byte]2)
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($encodedFrame, $null, $metadata, $null))
    $frameIndex++
}
$parentDirectory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputPath))
[IO.Directory]::CreateDirectory($parentDirectory) | Out-Null
$memory = [IO.MemoryStream]::new()
$encoder.Save($memory)
$gifBytes = $memory.ToArray()
$memory.Dispose()
# Add the standard GIF looping extension after its global color table.
$tableLength = if (($gifBytes[10] -band 128) -ne 0) { 3 * [math]::Pow(2, ($gifBytes[10] -band 7) + 1) } else { 0 }
$extensionOffset = [int](13 + $tableLength)
$extension = [byte[]]@(33,255,11,78,69,84,83,67,65,80,69,50,46,48,3,1,0,0,0)
$destination = [IO.File]::Create($OutputPath)
try {
    $destination.Write($gifBytes,0,$extensionOffset)
    $destination.Write($extension,0,$extension.Length)
    # WIC may omit supplied per-frame timing. Write explicit GIF control blocks.
    $cursor = $extensionOffset
    $animationFrame = 0
    while ($cursor -lt $gifBytes.Length) {
        $blockStart = $cursor
        $marker = $gifBytes[$cursor]
        $cursor++
        if ($marker -eq 59) {
            $destination.WriteByte(59)
            break
        } elseif ($marker -eq 33) {
            $label = $gifBytes[$cursor]
            $cursor++
            do {
                $blockSize = [int]$gifBytes[$cursor]
                $cursor += 1 + $blockSize
            } while ($blockSize -ne 0)
            if ($label -ne 249) { $destination.Write($gifBytes,$blockStart,$cursor-$blockStart) }
        } elseif ($marker -eq 44) {
            $descriptorFlags = $gifBytes[$cursor+8]
            $cursor += 9
            if (($descriptorFlags -band 128) -ne 0) { $cursor += [int](3 * [math]::Pow(2, ($descriptorFlags -band 7)+1)) }
            $cursor++
            do {
                $blockSize = [int]$gifBytes[$cursor]
                $cursor += 1 + $blockSize
            } while ($blockSize -ne 0)
            $delay = if ($animationFrame % 3 -eq 2) { 4 } else { 3 }
            $controlBlock = [byte[]]@(33,249,4,8,$delay,0,0,0)
            $destination.Write($controlBlock,0,$controlBlock.Length)
            $destination.Write($gifBytes,$blockStart,$cursor-$blockStart)
            $animationFrame++
        } else { throw "Unexpected GIF block marker $marker at $blockStart" }
    }
} finally { $destination.Dispose() }
Write-Output "Encoded $frameIndex captured game frames: $OutputPath"
