<#
  Generates the SeniorDesktop application icon (a white cane with red bands on a blue rounded square):
    Source\Assets\SeniorDesktop.ico   (multi-size: DIB frames up to 64 px, PNG frames for 128/256 px)
    docs\images\icon.png              (256 px preview used in the README)

  Run from the repository root:   powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
  Windows PowerShell 5.1 + System.Drawing, no other dependencies.
#>
Add-Type -AssemblyName System.Drawing

$root   = Split-Path -Parent $PSScriptRoot
$icoOut = Join-Path $root 'Source\Assets\SeniorDesktop.ico'
$pngOut = Join-Path $root 'docs\images\icon.png'
New-Item -ItemType Directory -Force (Split-Path $icoOut), (Split-Path $pngOut) | Out-Null

function New-Pen([string]$hex, [double]$width, [string]$cap = 'Round') {
    $p = New-Object Drawing.Pen ([Drawing.ColorTranslator]::FromHtml($hex)), ([single]$width)
    $p.StartCap = $cap; $p.EndCap = $cap; $p.LineJoin = 'Round'
    $p
}

function New-IconBitmap([int]$size) {
    $ss = 4                                   # supersampling for crisp small sizes
    $S  = $size * $ss
    $big = New-Object Drawing.Bitmap $S, $S, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($big)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([Drawing.Color]::Transparent)

    # --- rounded-square background with a blue gradient (matches the tile colours of the app)
    $r = $S * 0.22; $d = $r * 2
    $bg = New-Object Drawing.Drawing2D.GraphicsPath
    $bg.AddArc(0, 0, $d, $d, 180, 90)
    $bg.AddArc($S - $d, 0, $d, $d, 270, 90)
    $bg.AddArc($S - $d, $S - $d, $d, $d, 0, 90)
    $bg.AddArc(0, $S - $d, $d, $d, 90, 90)
    $bg.CloseFigure()
    $grad = New-Object Drawing.Drawing2D.LinearGradientBrush ((New-Object Drawing.Point 0, 0)), ((New-Object Drawing.Point $S, $S)),
        ([Drawing.ColorTranslator]::FromHtml('#2F80F5')), ([Drawing.ColorTranslator]::FromHtml('#0F2A47'))
    $g.FillPath($grad, $bg)

    # --- white cane geometry (normalised 0..1 coordinates): straight shaft, red lower part, black grip
    $A = New-Object Drawing.PointF ($S * 0.15), ($S * 0.91)     # tip (bottom)
    $B = New-Object Drawing.PointF ($S * 0.80), ($S * 0.12)     # top of the grip
    $w = $S * 0.088                                             # cane thickness

    function Point-On([double]$t) {
        New-Object Drawing.PointF ([single]($A.X + ($B.X - $A.X) * $t)), ([single]($A.Y + ($B.Y - $A.Y) * $t))
    }

    # dark outline first (separates the white cane from the background), then the white body
    $outline = New-Pen '#0A1D33' ($w + $S * 0.05)
    $g.DrawLine($outline, $A, $B)
    $white = New-Pen '#FFFFFF' $w
    $g.DrawLine($white, $A, $B)

    # red lower part, dark rubber tip, black grip
    $red = New-Pen '#E53935' $w 'Flat'
    $g.DrawLine($red, (Point-On 0.07), (Point-On 0.42))
    $tip = New-Pen '#263238' $w 'Flat'
    $g.DrawLine($tip, (Point-On 0.0), (Point-On 0.07))
    $grip = New-Pen '#111418' ($w * 1.18) 'Round'
    $g.DrawLine($grip, (Point-On 0.80), (Point-On 1.0))
    $g.Dispose()

    # downsample to the final size
    $out = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [Drawing.Graphics]::FromImage($out)
    $g2.InterpolationMode = 'HighQualityBicubic'; $g2.SmoothingMode = 'HighQuality'; $g2.PixelOffsetMode = 'HighQuality'
    $g2.DrawImage($big, 0, 0, $size, $size)
    $g2.Dispose(); $big.Dispose()
    $out
}

function Get-DibFrame([Drawing.Bitmap]$bmp) {
    # Classic icon image: BITMAPINFOHEADER + bottom-up 32-bit BGRA pixels + 1-bit AND mask (all zero, alpha is used)
    $w = $bmp.Width; $h = $bmp.Height
    $ms = New-Object IO.MemoryStream; $bw = New-Object IO.BinaryWriter $ms
    $maskStride = [int]([math]::Ceiling($w / 32.0) * 4)
    $bw.Write([uint32]40); $bw.Write([int32]$w); $bw.Write([int32]($h * 2)); $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]0); $bw.Write([uint32]($w * $h * 4 + $maskStride * $h)); $bw.Write([int32]0); $bw.Write([int32]0)
    $bw.Write([uint32]0); $bw.Write([uint32]0)
    $rect = New-Object Drawing.Rectangle 0, 0, $w, $h
    $data = $bmp.LockBits($rect, 'ReadOnly', [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = New-Object byte[] ($data.Stride * $h)
    [Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $bmp.UnlockBits($data)
    for ($y = $h - 1; $y -ge 0; $y--) { $bw.Write($bytes, $y * $data.Stride, $w * 4) }
    $bw.Write((New-Object byte[] ($maskStride * $h)))
    $bw.Flush()
    ,$ms.ToArray()   # leading comma: return the byte[] as one object
}

$sizes  = 16, 24, 32, 48, 64, 128, 256
$frames = foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    if ($s -ge 128) {
        $ms = New-Object IO.MemoryStream
        $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
        $data = $ms.ToArray()
    } else {
        $data = Get-DibFrame $bmp
    }
    if ($s -eq 256) { $bmp.Save($pngOut, [Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    [pscustomobject]@{ Size = $s; Data = $data }
}
# --- write the .ico container (ICONDIR + ICONDIRENTRY[] + PNG frames)
$fs = [IO.File]::Create($icoOut)
$bw = New-Object IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$f.Data.Length); $bw.Write([uint32]$offset)
    $offset += $f.Data.Length
}
foreach ($f in $frames) { $bw.Write([byte[]]$f.Data) }
$bw.Close(); $fs.Close()

Write-Host "Wrote $icoOut ($((Get-Item $icoOut).Length) bytes) and $pngOut"
