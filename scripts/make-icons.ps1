# Renders the plugin's icons from Segoe Fluent Icons glyphs so no binary assets need to be
# hand-authored or kept in the repo history as opaque blobs.
# Run once after checkout, or whenever the glyph list below changes.

Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot '..\Images'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$icons = @(
    @{ Name = 'icon';     Glyph = [char]0xE7FC; Color = '#4CC2FF' }  # Game
    @{ Name = 'folder';   Glyph = [char]0xE8B7; Color = '#5DD98A' }  # FolderOpen
    @{ Name = 'config';   Glyph = [char]0xE713; Color = '#4CC2FF' }  # Setting
    @{ Name = 'install';  Glyph = [char]0xEDA2; Color = '#C7A2FF' }  # HardDrive
    @{ Name = 'missing';  Glyph = [char]0xE897; Color = '#9AA0A6' }  # Help
    @{ Name = 'refresh';  Glyph = [char]0xE72C; Color = '#4CC2FF' }  # Refresh
    @{ Name = 'download'; Glyph = [char]0xE896; Color = '#4CC2FF' }  # Download
    @{ Name = 'search';   Glyph = [char]0xE721; Color = '#4CC2FF' }  # Search
    @{ Name = 'copy';     Glyph = [char]0xE8C8; Color = '#9AA0A6' }  # Copy
    @{ Name = 'back';     Glyph = [char]0xE72B; Color = '#9AA0A6' }  # Back
)

$size = 256
$fontName = 'Segoe Fluent Icons'
if (-not ([System.Drawing.FontFamily]::Families | Where-Object Name -eq $fontName)) {
    $fontName = 'Segoe MDL2 Assets'
}

foreach ($icon in $icons) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)

    $font = New-Object System.Drawing.Font $fontName, ($size * 0.62), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($icon.Color))
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = 'Center'
    $format.LineAlignment = 'Center'

    $g.DrawString([string]$icon.Glyph, $font, $brush, (New-Object System.Drawing.RectangleF 0, 0, $size, $size), $format)

    $path = Join-Path $outDir "$($icon.Name).png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)

    $format.Dispose(); $brush.Dispose(); $font.Dispose(); $g.Dispose(); $bmp.Dispose()
    Write-Host "wrote $path"
}
