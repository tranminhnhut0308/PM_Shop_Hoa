@echo off
title Vua Hoa Nha Huy - Ket Noi Web Cho Dien Thoai 4G
color 0B
cd /d "%~dp0"

echo ====================================================================
echo             VUA HOA NHA HUY - TAO LINK WEB ONLINE CHO DIEN THOAI
echo             (Mien phi 100%% - Khong can the ngan hang - Co HTTPS)
echo ====================================================================
echo.

:: 1. Kiem tra va tai cong cu Cloudflare Tunnel neu chua co
if not exist "cloudflared.exe" (
    echo [1/3] Dang tai cong cu Cloudflare Tunnel an toan ve may (khoang 20MB)...
    echo Xin vui long cho vai giay...
    powershell -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; (New-Object System.Net.WebClient).DownloadFile('https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe', 'cloudflared.exe')"
    if not exist "cloudflared.exe" (
        curl.exe -L -o cloudflared.exe https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe
    )
)

if not exist "cloudflared.exe" (
    echo [LOI] Khong the tai cloudflared.exe. Vui long kiem tra ket noi Internet cua ban.
    pause
    exit /b
)

echo [1/3] Cong cu Cloudflare: DA CO SAN.
echo.

:: 2. Khoi dong Web Server o chay ngam
echo [2/3] Dang khoi dong may chu Web ShopHoa...
start "ShopHoa Web API" /min cmd /c "cd /d %~dp0ShopHoa.Api && dotnet run"

:: Cho 4 giay de web khoi dong xong
timeout /t 4 /nobreak >nul

echo [3/3] Dang tao duong link Online truc tiep ra Internet...
echo.
echo ====================================================================
echo  HUONG DAN:
echo  1. Hay tim dong co chu: https://...trycloudflare.com ben duoi
echo  2. Copy duong link do gui qua Zalo hoac mo tren Safari/Chrome dien thoai
echo  3. Chu tiem dung 4G hay o bat ky dau deu xem duoc bao cao!
echo.
echo  (Giu cua so nay mo trong luc xem web nhe)
echo ====================================================================
echo.

cloudflared.exe tunnel --url http://localhost:5000
pause
