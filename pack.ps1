param(
    [ValidateSet("win-x64", "linux-x64")]
    [string]$Target = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

Write-Host "=== BAT DAU DONG GOI SHOPHOA ===" -ForegroundColor Cyan
Write-Host "Muc tieu: $Target" -ForegroundColor Yellow
Write-Host "Phien ban: $Version" -ForegroundColor Yellow

# 0. Chuan bi logo icon tu Assets/LOGO_APP.png
$logoSource = if (Test-Path "Assets/LOGO_APP.png") { "Assets/LOGO_APP.png" } else { "Assets/logo.png" }
if (Test-Path $logoSource) {
    Write-Host "`n[0/3] Dang tao file Assets/logo.ico tu $logoSource..." -ForegroundColor Yellow
    Add-Type -AssemblyName System.Drawing
    $srcImg = [System.Drawing.Image]::FromFile((Resolve-Path $logoSource))
    $bmp = New-Object System.Drawing.Bitmap(256, 256)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($srcImg, 0, 0, 256, 256)
    $g.Dispose()
    $srcImg.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngBytes = $ms.ToArray()
    $ms.Dispose()

    $icoPath = Join-Path (Get-Location) "Assets/logo.ico"
    $icoFs = [System.IO.File]::Create($icoPath)
    $bw = New-Object System.IO.BinaryWriter($icoFs)
    # ICONDIR
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]1)
    # ICONDIRENTRY
    $bw.Write([byte]0)   # 256 width
    $bw.Write([byte]0)   # 256 height
    $bw.Write([byte]0)   # color count
    $bw.Write([byte]0)   # reserved
    $bw.Write([uint16]1) # planes
    $bw.Write([uint16]32)# bpp
    $bw.Write([uint32]$pngBytes.Length)
    $bw.Write([uint32]22) # offset
    # PNG data
    $bw.Write($pngBytes)
    $bw.Flush()
    $bw.Close()
    $icoFs.Close()
    Write-Host "Da tao thanh cong Assets/logo.ico tu $logoSource!" -ForegroundColor Green
}

# 1. Kiem tra cong cu vpk (Velopack CLI)
Write-Host "`n[1/3] Kiem tra cong cu Velopack CLI (vpk)..." -ForegroundColor Green
$vpkInstalled = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpkInstalled) {
    Write-Host "Dang tu dong cai dat cong cu vpk (Velopack CLI)..." -ForegroundColor Yellow
    dotnet tool install -g vpk
} else {
    Write-Host "Velopack CLI da san sang." -ForegroundColor Green
}

# 2. Xuat ban ung dung (Self-contained)
Write-Host "`n[2/3] Dang xuat ban du an ShopHoa (Release, $Target, self-contained)..." -ForegroundColor Green

# Tat tien trinh ShopHoa neu dang chay ngam de tranh khoa file DLL
Get-Process -Name "ShopHoa", "ShopHoa-win-Setup", "vpk" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

$publishDir = "publish/$Target"
if (Test-Path $publishDir) {
    # Xoa thuoc tinh ReadOnly neu co de tranh loi Access is denied khi xoa file
    Get-ChildItem -Path $publishDir -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.IsReadOnly) { $_.IsReadOnly = $false }
    }
    try {
        Remove-Item $publishDir -Recurse -Force -ErrorAction Stop
    } catch {
        Start-Sleep -Seconds 1
        Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

dotnet publish ShopHoa.csproj -c Release /p:PublishProfile="Properties/PublishProfiles/$Target.pubxml" /p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Error "Xuat ban (dotnet publish) that bai!"
    exit 1
}

# 3. Dong goi cai dat bang Velopack
Write-Host "`n[3/3] Dang dong goi bo cai dat bang Velopack (vpk pack)..." -ForegroundColor Green
$releaseDir = "Releases/$Target"
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
} else {
    # Bo thuoc tinh ReadOnly tren tat ca file trong Releases
    Get-ChildItem -Path $releaseDir -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.IsReadOnly) { $_.IsReadOnly = $false }
    }

    # Xoa file RELEASES cu: Velopack khi hoan tat se goi DeleteFile('RELEASES'). Tren Windows,
    # neu file da ton tai, DeleteFile co the bi loi UnauthorizedAccessException do Antivirus/handle lock.
    # Xoa truoc giup vpk bo qua viec xoa va truc tiep tao moi RELEASES tu cac file .nupkg va releases.win.json san co.
    $releasesLegacy = Join-Path $releaseDir "RELEASES"
    if (Test-Path $releasesLegacy) {
        Remove-Item $releasesLegacy -Force -ErrorAction SilentlyContinue
    }

    # Xoa cac goi dở dang cua phien ban nay neu bi loi o lan dong goi truoc de tranh prompt hoi de de
    Get-ChildItem -Path $releaseDir -Filter "*$Version*" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

$mainExe = if ($Target -eq "win-x64") { "ShopHoa.exe" } else { "ShopHoa" }

$iconArgs = @()
if (Test-Path "Assets/logo.ico") {
    $iconArgs = @("-i", "Assets/logo.ico")
}

vpk pack -u ShopHoa -v $Version -p $publishDir -e $mainExe -o $releaseDir --packTitle "ShopHoa" --runtime $Target @iconArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Dong goi Velopack that bai!"
    exit 1
}

Write-Host "`n=== DONG GOI HOAN TAT THANH CONG ===" -ForegroundColor Green
Write-Host "Thu muc chua file cai dat: $(Resolve-Path $releaseDir)" -ForegroundColor Cyan
if ($Target -eq "win-x64") {
    Write-Host "File cai dat may khach: ShopHoa-win-Setup.exe" -ForegroundColor Yellow
} elseif ($Target -eq "linux-x64") {
    Write-Host "Dang tao goi ShopHoa-linux.tar.gz phuc vu cai dat 1 dong..." -ForegroundColor Yellow
    tar -czf "$releaseDir/ShopHoa-linux.tar.gz" -C $publishDir .
    Copy-Item "install.sh" "$releaseDir/install.sh" -Force -ErrorAction SilentlyContinue
    Write-Host "File cai dat Linux: $releaseDir/ShopHoa-linux.tar.gz va install.sh" -ForegroundColor Yellow
}
