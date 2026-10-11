@echo off
title Vua Hoa Nha Huy - Web Bao Cao Dien Thoai
color 0A
echo ==========================================================
echo        VUA HOA NHA HUY - WEB BAO CAO CHO DIEN THOAI
echo ==========================================================
echo.
echo Dang khoi dong Web server tai cong 5000...
echo.
echo - Tren may tinh nay:    http://localhost:5000
echo - Tren dien thoai:      http://[IP_MAY_TINH]:5000
echo   (Vi du: http://192.168.1.15:5000)
echo.
echo Luu y: Giu cua so nay mo de dien thoai co the truy cap.
echo ==========================================================
echo.
cd /d "%~dp0ShopHoa.Api"
dotnet run
pause
