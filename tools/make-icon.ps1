<#
  Generates the SeniorDesktop application icon (a grey walking cane with a dark handle on a blue rounded square):
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

    # --- walking cane (grey shaft, light collar, dark rubber tip, dark "derby" handle), normalised 0..1
    $A = New-Object Drawing.PointF ($S * 0.17), ($S * 0.90)     # tip (bottom)
    $B = New-Object Drawing.PointF ($S * 0.68), ($S * 0.30)     # top of the shaft
    $w = $S * 0.090                                             # shaft thickness
    $dx = $B.X - $A.X; $dy = $B.Y - $A.Y; $len = [math]::Sqrt($dx * $dx + $dy * $dy)
    $nx = -$dy / $len; $ny = $dx / $len                         # unit normal (for highlights)

    function Point-On([double]$t) {
        New-Object Drawing.PointF ([single]($A.X + $dx * $t)), ([single]($A.Y + $dy * $t))
    }
    function Shift([Drawing.PointF]$p, [double]$d) {
        New-Object Drawing.PointF ([single]($p.X + $nx * $d)), ([single]($p.Y + $ny * $d))
    }

    # handle path: from the shaft top up and over to the left (derby grip)
    $h0 = $B
    $h1 = New-Object Drawing.PointF ($S * 0.71), ($S * 0.15)
    $h2 = New-Object Drawing.PointF ($S * 0.55), ($S * 0.06)
    $h3 = New-Object Drawing.PointF ($S * 0.37), ($S * 0.17)
    $hw = $w * 1.85                                             # handle thickness

    # 1) light outline behind everything keeps the cane readable on the blue background
    $halo = New-Pen '#0A1D33' ($hw + $S * 0.05)
    $g.DrawBezier($halo, $h0, $h1, $h2, $h3)
    $haloShaft = New-Pen '#0A1D33' ($w + $S * 0.05)
    $g.DrawLine($haloShaft, $A, $B)

    # 2) shaft: grey body, lighter highlight, darker shade
    $g.DrawLine((New-Pen '#9C9C9C' $w), $A, $B)
    $g.DrawLine((New-Pen '#D2D2D2' ($w * 0.28)), (Shift $A (-$w * 0.22)), (Shift $B (-$w * 0.22)))
    $g.DrawLine((New-Pen '#6F6F6F' ($w * 0.22)), (Shift (Point-On 0.04) ($w * 0.32)), (Shift $B ($w * 0.32)))

    # 3) light metal collar below the handle and dark rubber tip
    $g.DrawLine((New-Pen '#E6E6E6' ($w * 1.04) 'Flat'), (Point-On 0.86), (Point-On 0.90))
    $g.DrawLine((New-Pen '#2B2B2B' ($w * 1.04) 'Flat'), (Point-On 0.0), (Point-On 0.07))

    # 4) handle: dark body with a soft highlight along the top
    $g.DrawBezier((New-Pen '#3A2F2B' $hw), $h0, $h1, $h2, $h3)
    $g.DrawBezier((New-Pen '#7A6A62' ($hw * 0.22)), (New-Object Drawing.PointF ($B.X + $w * 0.1), ($B.Y - $w * 0.1)),
        (New-Object Drawing.PointF ($S * 0.705), ($S * 0.125)), (New-Object Drawing.PointF ($S * 0.55), ($S * 0.035)),
        (New-Object Drawing.PointF ($S * 0.38), ($S * 0.135)))
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
