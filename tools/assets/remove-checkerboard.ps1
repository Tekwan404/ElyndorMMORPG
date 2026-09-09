[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function Test-CheckerboardPixel([int]$red, [int]$green, [int]$blue) {
    $maximum = [Math]::Max($red, [Math]::Max($green, $blue))
    $minimum = [Math]::Min($red, [Math]::Min($green, $blue))
    return ($maximum - $minimum) -le 18 -and $minimum -ge 175
}

$sourceImage = $null
$source = $null
$graphics = $null
$output = $null
$sourceData = $null
$outputData = $null
try {
    $sourceImage = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $SourcePath).Path)
    $source = [System.Drawing.Bitmap]::new($sourceImage.Width, $sourceImage.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($source)
    $graphics.DrawImageUnscaled($sourceImage, 0, 0)
    $graphics.Dispose()
    $graphics = $null

    $width = [int]$source.Width
    $height = [int]$source.Height
    $lastColumn = $width - 1
    $lastRow = $height - 1
    $rectangle = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
    $sourceData = $source.LockBits($rectangle, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = [Math]::Abs([int]$sourceData.Stride)
    $byteLength = $stride * $height
    $pixels = [byte[]]::new($byteLength)
    [System.Runtime.InteropServices.Marshal]::Copy($sourceData.Scan0, $pixels, 0, $byteLength)
    $source.UnlockBits($sourceData)
    $sourceData = $null

    $background = [bool[]]::new($width * $height)
    $transparent = [bool[]]::new($width * $height)
    for ($y = 0; $y -lt $height; $y++) {
        $rowOffset = $y * $stride
        $pixelRowOffset = $y * $width
        for ($x = 0; $x -lt $width; $x++) {
            $offset = $rowOffset + ($x * 4)
            $background[$pixelRowOffset + $x] = Test-CheckerboardPixel $pixels[$offset + 2] $pixels[$offset + 1] $pixels[$offset]
        }
    }

    $queue = [System.Collections.Generic.Queue[int]]::new()
    for ($x = 0; $x -lt $width; $x++) {
        if ($background[$x]) { $queue.Enqueue($x) }
        if ($height -gt 1 -and $background[($lastRow * $width) + $x]) { $queue.Enqueue(($lastRow * $width) + $x) }
    }
    for ($y = 1; $y -lt $lastRow; $y++) {
        $rowOffset = $y * $width
        if ($background[$rowOffset]) { $queue.Enqueue($rowOffset) }
        if ($width -gt 1 -and $background[$rowOffset + $lastColumn]) { $queue.Enqueue($rowOffset + $lastColumn) }
    }

    $directions = @(
        @(-1, -1), @(0, -1), @(1, -1),
        @(-1, 0),           @(1, 0),
        @(-1, 1),  @(0, 1),  @(1, 1)
    )
    while ($queue.Count -gt 0) {
        $index = $queue.Dequeue()
        if (-not $background[$index] -or $transparent[$index]) { continue }
        $transparent[$index] = $true
        $x = $index % $width
        $y = [int][Math]::Floor($index / $width)

        foreach ($direction in $directions) {
            $nextX = $x + $direction[0]
            $nextY = $y + $direction[1]
            if ($nextX -ge 0 -and $nextX -lt $width -and $nextY -ge 0 -and $nextY -lt $height) {
                $nextIndex = ($nextY * $width) + $nextX
                if ($background[$nextIndex] -and -not $transparent[$nextIndex]) {
                    $queue.Enqueue($nextIndex)
                }
            }
        }
    }

    $outputPixels = [byte[]]$pixels.Clone()
    for ($y = 0; $y -lt $height; $y++) {
        $rowOffset = $y * $stride
        $pixelRowOffset = $y * $width
        for ($x = 0; $x -lt $width; $x++) {
            if ($transparent[$pixelRowOffset + $x]) {
                $outputPixels[$rowOffset + ($x * 4) + 3] = 0
            }
        }
    }

    $output = [System.Drawing.Bitmap]::new($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $outputData = $output.LockBits($rectangle, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    [System.Runtime.InteropServices.Marshal]::Copy($outputPixels, 0, $outputData.Scan0, $byteLength)
    $output.UnlockBits($outputData)
    $outputData = $null

    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not (Test-Path -LiteralPath $outputDirectory)) {
        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    }
    $output.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    if ($null -ne $outputData) { $output.UnlockBits($outputData) }
    if ($null -ne $sourceData) { $source.UnlockBits($sourceData) }
    if ($null -ne $graphics) { $graphics.Dispose() }
    if ($null -ne $output) { $output.Dispose() }
    if ($null -ne $source) { $source.Dispose() }
    if ($null -ne $sourceImage) { $sourceImage.Dispose() }
}

Write-Host "Removed checkerboard background: $OutputPath"
