Add-Type -AssemblyName System.Drawing
$assetRoot = $PSScriptRoot
foreach ($sex in @('male', 'female')) {
    $sourcePath = Join-Path (Split-Path $assetRoot -Parent) "ink_portrait_pair_20261003_v1/$($sex)_portrait.png"
    $source = [System.Drawing.Bitmap]::new($sourcePath)
    $roi = [System.Drawing.Rectangle]::new(408, 330, 216, 84)
    $crop = $source.Clone($roi, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $preview = [System.Drawing.Bitmap]::new(1024, 512)
    $graphics = [System.Drawing.Graphics]::FromImage($preview)
    $graphics.Clear([System.Drawing.Color]::FromArgb(235, 228, 216))
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.DrawImage($crop, [System.Drawing.Rectangle]::new(80, 88, 864, 336))
    $preview.Save((Join-Path $assetRoot "source/$($sex)_eye_reference.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $preview.Dispose()
    $crop.Dispose()
    $source.Dispose()
}
