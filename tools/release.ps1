<#
  สร้างตัวติดตั้ง PC Hub สำหรับแจก

  วิธีใช้ (รันจากโฟลเดอร์โปรเจกต์):
    powershell -ExecutionPolicy Bypass -File tools\release.ps1

  ผลลัพธ์อยู่ในโฟลเดอร์ releases\
    PCHub-win-Setup.exe      ส่งไฟล์นี้ให้เพื่อน ดับเบิลคลิกติดตั้งได้เลย
    PCHub-win-Portable.zip   แบบไม่ต้องติดตั้ง แตกไฟล์แล้วเปิดได้เลย
    ไฟล์อื่นๆ                 ใช้กับระบบอัปเดต (อัปโหลดขึ้น GitHub Releases ทั้งหมด)

  เวอร์ชันอ่านจาก <Version> ใน src\PCHub\PCHub.csproj (ออกเวอร์ชันใหม่ต้องเพิ่มเลขทุกครั้ง)

  ใส่ -Publish เพื่ออัปโหลดขึ้น GitHub Releases ด้วย (ต้อง push โค้ดขึ้น GitHub และล็อกอิน gh ไว้แล้ว)
  แล้ว PC Hub ในเครื่องเพื่อนจะเจอเวอร์ชันใหม่และอัปเดตเอง
    powershell -ExecutionPolicy Bypass -File tools\release.ps1 -Publish
#>
param([switch]$Publish)

$ErrorActionPreference = 'Stop'

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'src\PCHub\PCHub.csproj'
$publishDir = Join-Path $root 'publish'
$releases = Join-Path $root 'releases'

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = if ($dotnet) { $dotnet.Source } else { 'C:\Program Files\dotnet\dotnet.exe' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

[xml]$xml = Get-Content $project
$version = @($xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
if (-not $version) { throw 'ไม่เจอ <Version> ใน PCHub.csproj' }
Write-Host "สร้าง PC Hub เวอร์ชัน $version ..."

# 1) build แบบรวม .NET ไว้ในตัว (เพื่อนไม่ต้องลง .NET เอง)
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
& $dotnet publish $project -c Release -r win-x64 --self-contained true -o $publishDir -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'build ไม่ผ่าน' }

# 2) แพ็กเป็นตัวติดตั้งด้วย Velopack
# เริ่มจากโฟลเดอร์ว่างทุกครั้ง: แต่ละเวอร์ชันบน GitHub มีไฟล์ของตัวเองครบ ไม่ต้องพึ่งเวอร์ชันเก่า
if (Test-Path $releases) { Remove-Item $releases -Recurse -Force }
Push-Location $root
try {
    & $dotnet tool restore | Out-Null
    & $dotnet vpk pack `
        --packId PCHub `
        --packVersion $version `
        --packDir $publishDir `
        --mainExe PCHub.exe `
        --packTitle 'PC Hub' `
        --icon (Join-Path $root 'src\PCHub\Assets\app.ico') `
        --outputDir $releases
    if ($LASTEXITCODE -ne 0) { throw 'แพ็กตัวติดตั้งไม่ผ่าน' }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "เสร็จแล้ว! ตัวติดตั้งอยู่ที่ $releases\PCHub-win-Setup.exe"

# 3) อัปโหลดขึ้น GitHub Releases (ถ้าสั่ง -Publish)
if ($Publish) {
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    $gh = if ($gh) { $gh.Source } else { 'C:\Program Files\GitHub CLI\gh.exe' }
    $files = Get-ChildItem $releases -File | ForEach-Object { $_.FullName }

    Push-Location $root
    try {
        & $gh release create "v$version" @files --title "PC Hub $version" --notes "PC Hub เวอร์ชัน $version  ดาวน์โหลด PCHub-win-Setup.exe แล้วดับเบิลคลิกติดตั้ง (ถ้าลงไว้แล้ว โปรแกรมจะอัปเดตเอง)"
        if ($LASTEXITCODE -ne 0) { throw 'อัปโหลดขึ้น GitHub ไม่ผ่าน' }
    }
    finally {
        Pop-Location
    }
    Write-Host "อัปโหลดขึ้น GitHub แล้ว: เวอร์ชัน $version"
}
