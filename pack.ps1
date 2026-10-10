param(
    [ValidateSet("win-x64", "linux-x64")]
    [string]$Target = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

Write-Host "=== BẮT ĐẦU ĐÓNG GÓI SHOPHOA ===" -ForegroundColor Cyan
Write-Host "Mục tiêu: $Target" -ForegroundColor Yellow
Write-Host "Phiên bản: $Version" -ForegroundColor Yellow

# 1. Kiểm tra công cụ vpk (Velopack CLI)
Write-Host "`n[1/3] Kiểm tra công cụ Velopack CLI (vpk)..." -ForegroundColor Green
$vpkInstalled = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpkInstalled) {
    Write-Host "Đang tự động cài đặt công cụ vpk (Velopack CLI)..." -ForegroundColor Yellow
    dotnet tool install -g vpk
} else {
    Write-Host "Velopack CLI đã sẵn sàng." -ForegroundColor Green
}

# 2. Xuất bản ứng dụng (Self-contained)
Write-Host "`n[2/3] Đang xuất bản dự án ShopHoa (Release, $Target, self-contained)..." -ForegroundColor Green
$publishDir = "publish/$Target"
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish ShopHoa.csproj -c Release /p:PublishProfile="Properties/PublishProfiles/$Target.pubxml" /p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Error "Xuất bản (dotnet publish) thất bại!"
    exit 1
}

# 3. Đóng gói cài đặt bằng Velopack
Write-Host "`n[3/3] Đang đóng gói bộ cài đặt bằng Velopack (vpk pack)..." -ForegroundColor Green
$releaseDir = "Releases/$Target"
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

$mainExe = if ($Target -eq "win-x64") { "ShopHoa.exe" } else { "ShopHoa" }

vpk pack -u ShopHoa -v $Version -p $publishDir -e $mainExe -o $releaseDir --packTitle "Phần mềm Quản lý Shop Hoa"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Đóng gói Velopack thất bại!"
    exit 1
}

Write-Host "`n=== ĐÓNG GÓI HOÀN TẤT THÀNH CÔNG ===" -ForegroundColor Green
Write-Host "Thư mục chứa file cài đặt: $(Resolve-Path $releaseDir)" -ForegroundColor Cyan
if ($Target -eq "win-x64") {
    Write-Host "File cài đặt máy khách: ShopHoa-Setup.exe" -ForegroundColor Yellow
}
