Add-Type -AssemblyName System.Drawing

$sourcePath = Join-Path $PSScriptRoot '..\textures\characters\c1\001.png'
$outputRoot = Join-Path $PSScriptRoot '..\textures\characters\c1_spine\layers'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$source = [System.Drawing.Bitmap]::new((Resolve-Path $sourcePath).Path)
$width = $source.Width
$height = $source.Height

function New-Point([int]$x, [int]$y) { return [System.Drawing.Point]::new($x, $y) }

function Convert-ToPointArray($value) {
    $points = [System.Collections.Generic.List[System.Drawing.Point]]::new()

    function Visit($item) {
        if ($null -eq $item) { return }
        if ($item -is [System.Drawing.Point]) {
            [void]$points.Add($item)
            return
        }
        if ($item -is [System.Array]) {
            foreach ($child in $item) { Visit $child }
        }
    }

    Visit $value
    return ,([System.Drawing.Point[]]$points.ToArray())
}

function Export-Layer([string]$name, $polygons) {
    $output = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($output)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $region = [System.Drawing.Region]::new([System.Drawing.Rectangle]::new(0, 0, $width, $height))
    $region.MakeEmpty()

    foreach ($polygon in $polygons) {
        $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $points = Convert-ToPointArray $polygon
        if ($points.Count -ge 3) {
            $path.AddPolygon($points)
            $region.Union($path)
        }
        $path.Dispose()
    }

    $graphics.SetClip($region, [System.Drawing.Drawing2D.CombineMode]::Replace)
    $graphics.DrawImageUnscaled($source, 0, 0)
    $graphics.ResetClip()
    $graphics.Dispose()
    $output.Save((Join-Path $outputRoot "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $output.Dispose()
}

Export-Layer '01_hair_back' @(
    ,@((New-Point 155 70),(New-Point 250 20),(New-Point 350 55),(New-Point 365 190),(New-Point 325 330),(New-Point 285 455),(New-Point 175 470),(New-Point 150 335),(New-Point 175 210)),
    ,@((New-Point 180 125),(New-Point 275 115),(New-Point 245 305),(New-Point 175 300))
)
Export-Layer '02_head_face' @(
    ,@((New-Point 295 70),(New-Point 390 65),(New-Point 445 110),(New-Point 438 190),(New-Point 395 245),(New-Point 320 240),(New-Point 278 175))
)
Export-Layer '03_hair_front' @(
    ,@((New-Point 320 70),(New-Point 430 65),(New-Point 445 155),(New-Point 420 220),(New-Point 365 235),(New-Point 325 180))
)
Export-Layer '04_body_clothes' @(
    ,@((New-Point 270 185),(New-Point 420 180),(New-Point 460 250),(New-Point 500 430),(New-Point 515 775),(New-Point 475 875),(New-Point 230 875),(New-Point 195 760),(New-Point 225 455),(New-Point 245 245))
)
Export-Layer '05_sleeve_right_hand' @(
    ,@((New-Point 395 200),(New-Point 500 195),(New-Point 575 235),(New-Point 615 305),(New-Point 570 370),(New-Point 535 510),(New-Point 450 500),(New-Point 420 355))
)
Export-Layer '06_sleeve_left_scroll' @(
    ,@((New-Point 180 365),(New-Point 315 350),(New-Point 345 500),(New-Point 300 625),(New-Point 185 580)),
    ,@((New-Point 205 445),(New-Point 330 470),(New-Point 335 575),(New-Point 220 600))
)
Export-Layer '07_waist_accessories' @(
    ,@((New-Point 250 350),(New-Point 445 345),(New-Point 465 620),(New-Point 270 625))
)
Export-Layer '08_feet_shoes' @(
    ,@((New-Point 195 805),(New-Point 345 805),(New-Point 345 960),(New-Point 185 960)),
    ,@((New-Point 365 805),(New-Point 525 805),(New-Point 525 960),(New-Point 355 960))
)

$source.Dispose()
Write-Output "Exported Spine draft layers to $outputRoot"
