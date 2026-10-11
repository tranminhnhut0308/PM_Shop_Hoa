@echo off
title Vua Hoa Nha Huy - Don Dep Thu Muc Du An
color 0A
cd /d "%~dp0"

echo ====================================================================
echo        VUA HOA NHA HUY - DỌN DẸP SOURCE CODE CHO GỌN & NHẸ
echo ====================================================================
echo.
echo Dang tien hanh quet va don dep cac file rac, file cache bien dich...
echo (Luu y: KHONG anh huong den ma nguon va co so du lieu)
echo.

:: 1. Xoa thu muc cache .vs cua Visual Studio (thuong chiem 200MB - 1GB)
if exist ".vs" (
    echo [*] Dang xoa thu muc cache .vs cua Visual Studio...
    rd /s /q ".vs" >nul 2>&1
)

:: 2. Xoa cac thu muc bin (chua file build .dll, .exe, .pdb cu)
if exist "bin" (
    echo [*] Dang xoa thu muc bin (ShopHoa Desktop)...
    rd /s /q "bin" >nul 2>&1
)
if exist "ShopHoa.Api\bin" (
    echo [*] Dang xoa thu muc bin (ShopHoa.Api)...
    rd /s /q "ShopHoa.Api\bin" >nul 2>&1
)

:: 3. Xoa cac thu muc obj (cache trung gian bien dich)
if exist "obj" (
    echo [*] Dang xoa thu muc obj (ShopHoa Desktop)...
    rd /s /q "obj" >nul 2>&1
)
if exist "ShopHoa.Api\obj" (
    echo [*] Dang xoa thu muc obj (ShopHoa.Api)...
    rd /s /q "ShopHoa.Api\obj" >nul 2>&1
)

:: 4. Xoa cac thu muc publish tam thoi
if exist "publish" (
    echo [*] Dang xoa thu muc publish tam thoi...
    rd /s /q "publish" >nul 2>&1
)
if exist "ShopHoa.Api\publish" (
    echo [*] Dang xoa thu muc publish (ShopHoa.Api)...
    rd /s /q "ShopHoa.Api\publish" >nul 2>&1
)

:: 5. Xoa file Dockerfile thua (khong can nua vi da dung Cloudflare Tunnel)
if exist "Dockerfile" (
    echo [*] Dang xoa Dockerfile o thu muc goc...
    del /f /q "Dockerfile" >nul 2>&1
)
if exist "ShopHoa.Api\Dockerfile" (
    echo [*] Dang xoa Dockerfile trong ShopHoa.Api...
    del /f /q "ShopHoa.Api\Dockerfile" >nul 2>&1
)

:: 6. Xoa cac file log, temp rac
del /s /q *.log >nul 2>&1
del /s /q *.tmp >nul 2>&1

echo.
echo ====================================================================
echo [HOAN TAT] DA DỌN DEP THANH CONG!
echo.
echo  - Da xoa sach cac file bien dich cu (bin, obj, cache .vs, publish).
echo  - Thu muc source gio day rat gon gang, nhe hon hang tram MB / GB.
echo  - Khi nao mo lai du an hoac chay 'mo_link_online.bat', he thong se
echo    tu dong bien dich moi hoan toan ma khong bi ton rac cu.
echo ====================================================================
echo.
pause
