# Generate a multi-resolution Windows icon from a simple camera vector.
Add-Type -AssemblyName System.Drawing
$iconPath = Join-Path $PSScriptRoot '..\src\MyGyazo\Assets\App.ico'
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($iconPath)) | Out-Null
$images = @()
foreach ($size in @(16, 24, 32, 48, 64, 256)) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 64.0), ($size / 64.0))
    $shape = New-Object System.Drawing.Drawing2D.GraphicsPath
    $shape.AddArc(2, 2, 20, 20, 180, 90)
    $shape.AddArc(42, 2, 20, 20, 270, 90)
    $shape.AddArc(42, 42, 20, 20, 0, 90)
    $shape.AddArc(2, 42, 20, 20, 90, 90)
    $shape.CloseFigure()
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(80, 88, 214))
    $graphics.FillPath($brush, $shape)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 3)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $camera = New-Object System.Drawing.Drawing2D.GraphicsPath
    $camera.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(14, 23), [System.Drawing.PointF]::new(24, 23),
        [System.Drawing.PointF]::new(27, 18), [System.Drawing.PointF]::new(37, 18),
        [System.Drawing.PointF]::new(40, 23), [System.Drawing.PointF]::new(50, 23),
        [System.Drawing.PointF]::new(50, 46), [System.Drawing.PointF]::new(14, 46)))
    $camera.CloseFigure()
    $graphics.DrawPath($pen, $camera)
    $graphics.DrawEllipse($pen, 25, 28, 14, 14)
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose(); $camera.Dispose(); $pen.Dispose(); $brush.Dispose(); $shape.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$output = [System.IO.File]::Create($iconPath)
$writer = New-Object System.IO.BinaryWriter($output)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($entry in $images) {
        $dimension = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$entry.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $entry.Bytes.Length
    }
    foreach ($entry in $images) { $writer.Write([byte[]]$entry.Bytes) }
} finally { $writer.Dispose(); $output.Dispose() }
