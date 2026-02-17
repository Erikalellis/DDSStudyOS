param(
    [string]$SourceImage = "src\DDSStudyOS.App\Assets\SplashBackground.png",
    [string]$OutputDir = "installer\inno\branding",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function New-CroppedBitmap {
    param(
        [System.Drawing.Bitmap]$SourceBitmap,
        [int]$TargetWidth,
        [int]$TargetHeight
    )

    $target = New-Object System.Drawing.Bitmap($TargetWidth, $TargetHeight, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $gfx = [System.Drawing.Graphics]::FromImage($target)

    try {
        $gfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $gfx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $gfx.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $gfx.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

        $scaleX = [double]$TargetWidth / $SourceBitmap.Width
        $scaleY = [double]$TargetHeight / $SourceBitmap.Height
        $scale = [Math]::Max($scaleX, $scaleY)

        $drawWidth = [int][Math]::Ceiling($SourceBitmap.Width * $scale)
        $drawHeight = [int][Math]::Ceiling($SourceBitmap.Height * $scale)
        $drawX = [int][Math]::Floor(($TargetWidth - $drawWidth) / 2.0)
        $drawY = [int][Math]::Floor(($TargetHeight - $drawHeight) / 2.0)

        $gfx.Clear([System.Drawing.Color]::Black)
        $gfx.DrawImage($SourceBitmap, $drawX, $drawY, $drawWidth, $drawHeight)

        return $target
    }
    finally {
        $gfx.Dispose()
    }
}

Add-Type -AssemblyName System.Drawing

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedSource = if ([System.IO.Path]::IsPathRooted($SourceImage)) { $SourceImage } else { Join-Path $repoRoot $SourceImage }
$resolvedOutputDir = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $repoRoot $OutputDir }

if (-not (Test-Path $resolvedSource)) {
    throw "Imagem fonte nao encontrada: $resolvedSource"
}

if (-not (Test-Path $resolvedOutputDir)) {
    New-Item -ItemType Directory -Path $resolvedOutputDir -Force | Out-Null
}

$sideOutput = Join-Path $resolvedOutputDir "wizard-side.bmp"
$smallOutput = Join-Path $resolvedOutputDir "wizard-small.bmp"

if (-not $Force -and (Test-Path $sideOutput) -and (Test-Path $smallOutput)) {
    Write-Host "Branding Inno ja existe. Use -Force para recriar."
    Write-Host $sideOutput
    Write-Host $smallOutput
    exit 0
}

$sourceBitmap = [System.Drawing.Bitmap]::new($resolvedSource)

try {
    $sideBitmap = New-CroppedBitmap -SourceBitmap $sourceBitmap -TargetWidth 164 -TargetHeight 314
    $smallBitmap = New-CroppedBitmap -SourceBitmap $sourceBitmap -TargetWidth 55 -TargetHeight 58

    try {
        $sideBitmap.Save($sideOutput, [System.Drawing.Imaging.ImageFormat]::Bmp)
        $smallBitmap.Save($smallOutput, [System.Drawing.Imaging.ImageFormat]::Bmp)
    }
    finally {
        $sideBitmap.Dispose()
        $smallBitmap.Dispose()
    }
}
finally {
    $sourceBitmap.Dispose()
}

Write-Host "Branding Inno gerado com sucesso:"
Write-Host " - $sideOutput"
Write-Host " - $smallOutput"
