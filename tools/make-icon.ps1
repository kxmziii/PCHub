<#
  สร้างไอคอนของโปรแกรม:
    src/PCHub/Assets/app.ico   ไอคอนหลายขนาดในไฟล์เดียว (Desktop, Taskbar, Alt+Tab, หัวหน้าต่าง)
    src/PCHub/Assets/logo.png  โลโก้ขนาด 256px (ใช้ในเมนูซ้ายของโปรแกรม)

  วิธีใช้:
    ไม่ใส่ -Source        วาดโลโก้ชั่วคราว (จอคอมบนกรอบสีฟ้า)
    -Source logo.png      ใช้รูปโลโก้ของเราเอง (ควรเป็นสี่เหลี่ยมจัตุรัส 1024px ขึ้นไป)

    powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1 -Source C:\path\logo.png
#>
param([string]$Source)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

$assets = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\PCHub\Assets'))
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

function New-Brush([string]$hex) {
    New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString($hex))
}

# วาดโลโก้ชั่วคราวที่ขนาด $s พิกเซล (วาดใหม่ทุกขนาด เส้นจะได้คมแม้ย่อเหลือ 16px)
function Draw-DefaultLogo([System.Windows.Media.DrawingContext]$dc, [double]$s) {
    $pt = { param($x, $y) New-Object System.Windows.Point ($x * $s), ($y * $s) }

    $radius = $s * 0.22
    $dc.DrawRoundedRectangle((New-Brush '#7AA2F7'), $null, (New-Object System.Windows.Rect 0, 0, $s, $s), $radius, $radius)

    $pen = New-Object System.Windows.Media.Pen (New-Brush '#12151C'), ([Math]::Max(1.5, $s * 0.075))
    $pen.StartLineCap = 'Round'
    $pen.EndLineCap = 'Round'
    $pen.LineJoin = 'Round'

    # จอ
    $screen = New-Object System.Windows.Rect ($s * 0.21), ($s * 0.24), ($s * 0.58), ($s * 0.39)
    $dc.DrawRoundedRectangle($null, $pen, $screen, $s * 0.05, $s * 0.05)
    # ขาตั้ง + ฐาน
    $dc.DrawLine($pen, (& $pt 0.5 0.63), (& $pt 0.5 0.75))
    $dc.DrawLine($pen, (& $pt 0.35 0.76), (& $pt 0.65 0.76))
}

function Render-Logo([int]$s, $sourceImage) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($visual, 'HighQuality')
    $dc = $visual.RenderOpen()
    if ($sourceImage) {
        $dc.DrawImage($sourceImage, (New-Object System.Windows.Rect 0, 0, $s, $s))
    } else {
        Draw-DefaultLogo $dc $s
    }
    $dc.Close()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $s, $s, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    # ไฟล์ .ico ต้องการสีแบบไม่ premultiply
    New-Object System.Windows.Media.Imaging.FormatConvertedBitmap $bitmap, ([System.Windows.Media.PixelFormats]::Bgra32), $null, 0
}

function Get-PngBytes($bitmap) {
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    $encoder.Save($stream)
    , $stream.ToArray()   # ใส่ , กัน PowerShell แตก byte[] ออกเป็นทีละตัว
}

# ขนาดเล็กเก็บแบบ BMP (โปรแกรมเก่าๆ อ่านได้ชัวร์), 256px เก็บแบบ PNG (ไฟล์เล็กกว่า)
function Get-IconImageBytes($bitmap, [int]$s) {
    if ($s -ge 256) { return , (Get-PngBytes $bitmap) }

    $stride = $s * 4
    $pixels = New-Object byte[] ($stride * $s)
    $bitmap.CopyPixels($pixels, $stride, 0)

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $stream
    # BITMAPINFOHEADER (ความสูง x2 เพราะรวม mask ไว้ด้วย)
    $writer.Write([int]40); $writer.Write([int]$s); $writer.Write([int]($s * 2))
    $writer.Write([int16]1); $writer.Write([int16]32); $writer.Write([int]0)
    $writer.Write([int]$pixels.Length); $writer.Write([int]0); $writer.Write([int]0)
    $writer.Write([int]0); $writer.Write([int]0)
    # พิกเซลเรียงจากล่างขึ้นบน
    for ($y = $s - 1; $y -ge 0; $y--) { $writer.Write($pixels, $y * $stride, $stride) }
    # AND mask (ว่างไว้ ใช้ความโปร่งใสจาก alpha แทน)
    $maskStride = [int][Math]::Ceiling($s / 32.0) * 4
    $writer.Write((New-Object byte[] ($maskStride * $s)))
    $writer.Flush()
    , $stream.ToArray()
}

$sourceImage = $null
if ($Source) {
    $sourceImage = New-Object System.Windows.Media.Imaging.BitmapImage
    $sourceImage.BeginInit()
    $sourceImage.UriSource = New-Object System.Uri ((Resolve-Path $Source).Path)
    $sourceImage.CacheOption = 'OnLoad'
    $sourceImage.EndInit()
}

$images = foreach ($s in $sizes) {
    , (Get-IconImageBytes (Render-Logo $s $sourceImage) $s)
}

# เขียนไฟล์ .ico: ส่วนหัว 6 ไบต์ + รายการละ 16 ไบต์ + ข้อมูลรูป
$ico = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $ico
$writer.Write([int16]0); $writer.Write([int16]1); $writer.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dim); $writer.Write([byte]$dim)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([int16]1); $writer.Write([int16]32)
    $writer.Write([int]$images[$i].Length); $writer.Write([int]$offset)
    $offset += $images[$i].Length
}
foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
$writer.Flush()

New-Item -ItemType Directory -Force $assets | Out-Null
[System.IO.File]::WriteAllBytes((Join-Path $assets 'app.ico'), $ico.ToArray())
[System.IO.File]::WriteAllBytes((Join-Path $assets 'logo.png'), (Get-PngBytes (Render-Logo 256 $sourceImage)))

Write-Host "สร้างไอคอนเสร็จแล้ว: $assets\app.ico และ logo.png"
