@echo off
title Vua Hoa Nha Huy - Tao Link Web 4G Cho Dien Thoai
color 0B
cd /d "%~dp0"

echo ====================================================================
echo        VUA HOA NHA HUY - TAO LINK XEM BAO CAO CHO DIEN THOAI
echo ====================================================================
echo.

:: 1. Kiem tra file cloudflared.exe hop le (phai > 10MB)
if exist "cloudflared.exe" (
    for %%F in ("cloudflared.exe") do (
        if %%~zF LSS 10000000 (
            echo File cloudflared.exe cu bi loi hoac chua tai xong, dang xoa de tai lai...
            del /f /q "cloudflared.exe" >nul 2>&1
        )
    )
)

if not exist "cloudflared.exe" (
    echo [1/3] Dang tai cong cu Cloudflare Tunnel ve may...
    echo       (Dung luong khoang 25MB, xin cho 10-20 giay...)
    echo.

    :: Thu tai bang curl co kem User-Agent
    curl.exe -L -A "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" --progress-bar -o "cloudflared.exe" "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"

    :: Kiem tra lai neu chua duoc thi thu bang PowerShell
    if exist "cloudflared.exe" (
        for %%F in ("cloudflared.exe") do (
            if %%~zF LSS 10000000 (
                del /f /q "cloudflared.exe" >nul 2>&1
            )
        )
    )

    if not exist "cloudflared.exe" (
        echo Dang thu tai lai bang PowerShell...
        powershell -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; $ProgressPreference = 'SilentlyContinue'; $wc = New-Object System.Net.WebClient; $wc.Headers.Add('User-Agent', 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'); $wc.DownloadFile('https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe', 'cloudflared.exe')"
    )
)

:: Kiem tra lan cuoi
if exist "cloudflared.exe" (
    for %%F in ("cloudflared.exe") do (
        if %%~zF LSS 10000000 (
            del /f /q "cloudflared.exe" >nul 2>&1
        )
    )
)

if not exist "cloudflared.exe" (
    echo.
    echo ====================================================================
    echo [THONG BAO] Khong the tu dong tai cloudflared.exe do mang chan.
    echo Dang mo trinh duyet (Chrome/Edge) de ban tai truc tiep...
    echo ====================================================================
    start https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe
    echo.
    echo HUONG DAN:
    echo 1. Trinh duyet cua ban dang tai file 'cloudflared-windows-amd64.exe'.
    echo 2. Khi tai xong, hay doi ten file thanh 'cloudflared.exe'.
    echo 3. Chep file 'cloudflared.exe' vao thu muc D:\ShopHoa.
    echo 4. Sau do nhan phim bat ky tai day de tiep tuc!
    echo ====================================================================
    pause
    if not exist "cloudflared.exe" (
        echo Van chua tim thay file cloudflared.exe trong D:\ShopHoa. Vui long lam theo huong dan tren.
        pause
        exit /b
    )
)

echo.
echo [1/3] Cong cu Cloudflare Tunnel: DA SAN SANG!
echo.

:: 2. Khoi dong Web Server ShopHoa.Api neu chua khoi dong
echo [2/3] Dang khoi dong may chu Web ShopHoa (cong 5000)...
start "ShopHoa Web Server" cmd /c "cd /d %~dp0ShopHoa.Api && dotnet run"

echo Cho 6 giay de may chu web khoi dong xong...
ping 127.0.0.1 -n 7 >nul

echo.
echo [3/3] Dang ket noi Cloudflare tao duong link HTTPS xem tren dien thoai...
echo.
echo ====================================================================
echo  HUONG DAN LAY LINK XEM DIEN THOAI:
echo  1. Xem cac dong chu ben duoi, tim dong co chu:
echo     https://xxxx-xxxx-xxxx.trycloudflare.com
echo  2. Copy link do gui qua Zalo / tin nhan mo tren DIEN THOAI (4G hoac Wifi)!
echo  3. GIU CUA SO NAY LUON MO trong luc xem bao cao.
echo ====================================================================
echo.

cloudflared.exe tunnel --url http://localhost:5000

echo.
echo Cua so da ket thuc ket noi.
pause
