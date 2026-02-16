param(
    [Parameter(Mandatory = $true)]
    [string]$SourceImage,
    [Parameter(Mandatory = $true)]
    [string]$OutputIco,
    [int[]]$Sizes = @(16, 24, 32, 48, 64, 128, 256)
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$resolvedSource = (Resolve-Path $SourceImage).Path
$outputDir = Split-Path -Parent $OutputIco
if (-not [string]::IsNullOrWhiteSpace($outputDir) -and -not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$sizesToUse = $Sizes | Where-Object { $_ -ge 16 -and $_ -le 256 } | Sort-Object -Unique
if (-not $sizesToUse -or $sizesToUse.Count -eq 0) {
    throw "Informe ao menos um tamanho valido entre 16 e 256."
}

$sourceBitmap = [System.Drawing.Bitmap]::FromFile($resolvedSource)
$iconEntries = New-Object System.Collections.Generic.List[object]

try {
    foreach ($size in $sizesToUse) {
        $resized = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($resized)
        $stream = New-Object System.IO.MemoryStream

        try {
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.DrawImage($sourceBitmap, 0, 0, $size, $size)

            $resized.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $iconEntries.Add([pscustomobject]@{
                Size = $size
                Data = $stream.ToArray()
            })
        }
        finally {
            $graphics.Dispose()
            $resized.Dispose()
            $stream.Dispose()
        }
    }

    $fileStream = [System.IO.File]::Open($OutputIco, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    $writer = New-Object System.IO.BinaryWriter($fileStream)

    try {
        $count = $iconEntries.Count
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$count)

        $offset = 6 + (16 * $count)
        foreach ($entry in $iconEntries) {
            $size = [int]$entry.Size
            $widthByte = if ($size -ge 256) { 0 } else { [byte]$size }
            $heightByte = if ($size -ge 256) { 0 } else { [byte]$size }

            $writer.Write([byte]$widthByte)
            $writer.Write([byte]$heightByte)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$entry.Data.Length)
            $writer.Write([UInt32]$offset)

            $offset += $entry.Data.Length
        }

        foreach ($entry in $iconEntries) {
            $writer.Write([byte[]]$entry.Data)
        }
    }
    finally {
        $writer.Dispose()
        $fileStream.Dispose()
    }
}
finally {
    $sourceBitmap.Dispose()
}

Write-Host "Icone gerado com sucesso: $OutputIco"
