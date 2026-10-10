# TÀI LIỆU HƯỚNG DẪN TRIỂN KHAI, ĐÓNG GÓI VÀ BẢO MẬT HỆ THỐNG SHOPHOA

Dự án **ShopHoa** là hệ thống quản lý bán hàng đa nền tảng (Windows 10/11 & Linux) được xây dựng trên nền tảng **C# / .NET 8**, **Avalonia UI (MVVM)**, **ASP.NET Core Web API**, và **MySQL / MariaDB**.

---

## MỤC LỤC
1. [Kiến trúc hệ thống](#1-kiến-trúc-hệ-thống)
2. [Lệnh Build và Đóng gói Desktop](#2-lệnh-build-và-đóng-gói-desktop)
3. [Hướng dẫn triển khai Backend Web API](#3-hướng-dẫn-triển-khai-backend-web-api)
4. [Cấu hình Bảo mật Máy chủ & Cơ sở dữ liệu](#4-cấu-hình-bảo-mật-máy-chủ--cơ-sở-dữ-liệu)
5. [Cấu hình Máy chủ Cập nhật từ xa (Velopack Update Server)](#5-cấu-hình-máy-chủ-cập-nhật-từ-xa-velopack-update-server)
6. [Quy trình Sao lưu (Backup) và Khôi phục (Restore)](#6-quy-trình-sao-lưu-backup-và-khôi-phục-restore)
7. [Quy trình Phát hành Phiên bản Mới (Release Workflow)](#7-quy-trình-phát-hành-phiên-bản-mới-release-workflow)

---

## 1. KIẾN TRÚC HỆ THỐNG

```
[Máy khách Windows 10/11 / Linux]
       │
       ▼ (HTTPS / REST API - Port 443)
[Máy chủ Web API (ShopHoa.Api)]
       │
       ▼ (Kết nối nội bộ localhost:3306)
[MariaDB / MySQL Server]
```

- **Mặt trước (Client):** Ứng dụng Desktop Avalonia tự động cập nhật qua Velopack.
- **Mặt sau (Server API):** ASP.NET Core 8 Web API xác thực bằng JSON Web Token (JWT) và mã hóa mật khẩu BCrypt.
- **Cơ sở dữ liệu:** MariaDB / MySQL. Cổng 3306 được đóng hoàn toàn ra ngoài Internet, chỉ chấp nhận kết nối nội bộ từ Web API.
- **An toàn dữ liệu cục bộ:** File cấu hình và phiên đăng nhập lưu tại thư mục AppData người dùng:
  - Windows: `%LocalAppData%\ShopHoa\`
  - Linux: `~/.local/share/ShopHoa/`
  *(Không bao giờ bị xóa khi người dùng nâng cấp phần mềm)*.

---

## 2. LỆNH BUILD VÀ ĐÓNG GÓI DESKTOP

### 2.1. Biên dịch kiểm tra dự án
```powershell
# Biên dịch toàn bộ Solution (bao gồm Client và API)
dotnet build ShopHoa.sln -c Release
```

### 2.2. Đóng gói bộ cài đặt tự động bằng PowerShell (`pack.ps1`)
Project đã được tích hợp sẵn script tự động kiểm tra công cụ Velopack CLI (`vpk`), xuất bản `self-contained` (máy khách không cần cài .NET Runtime), và đóng gói bộ cài:

#### Đóng gói cho Windows 64-bit (`win-x64`):
```powershell
.\pack.ps1 -Target win-x64 -Version 1.0.0
```
- **Thư mục đầu ra:** `d:\ShopHoa\Releases\win-x64\`
- **File cài đặt:** `ShopHoa-Setup.exe` (Người dùng chỉ cần click đúp là tự cài đặt, tự tạo shortcut Desktop & Start Menu).

#### Đóng gói cho Linux 64-bit (`linux-x64`):
```powershell
.\pack.ps1 -Target linux-x64 -Version 1.0.0
```
- **Thư mục đầu ra:** `d:\ShopHoa\Releases\linux-x64\`

---

## 3. HƯỚNG DẪN TRIỂN KHAI BACKEND WEB API

### 3.1. Xuất bản Web API (Publish)
```powershell
dotnet publish ShopHoa.Api\ShopHoa.Api.csproj -c Release -o ./publish/api
```

### 3.2. Cấu hình chuỗi kết nối trên máy chủ (`appsettings.json`)
Chỉnh sửa file `appsettings.json` trong thư mục xuất bản:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=3306;Database=hoa_shop;User ID=shophoa_api_user;Password=MatKhauBaoMatCucManh_2026;SslMode=None;Allow User Variables=True;"
  },
  "Jwt": {
    "Key": "Chuoi_Khoa_Bi_Mat_JWT_Sieu_Dai_Toi_Thieu_64_Ky_Tu_Bao_Mat_Tuyet_Doi_2026!",
    "Issuer": "ShopHoaApi",
    "Audience": "ShopHoaApp",
    "ExpireHours": 48
  }
}
```

### 3.3. Cấu hình chạy dịch vụ Systemd trên Linux (Ubuntu / Debian)
Tạo file dịch vụ: `/etc/systemd/system/shophoa-api.service`:
```ini
[Unit]
Description=ShopHoa ASP.NET Core Web API Service
After=network.target mysql.service

[Service]
WorkingDirectory=/var/www/shophoa-api
ExecStart=/usr/bin/dotnet /var/www/shophoa-api/ShopHoa.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=shophoa-api
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```
Khởi động dịch vụ:
```bash
sudo systemctl daemon-reload
sudo systemctl enable shophoa-api
sudo systemctl start shophoa-api
sudo systemctl status shophoa-api
```

### 3.4. Cấu hình Nginx làm Reverse Proxy + SSL (HTTPS)
Cấu hình Nginx tại `/etc/nginx/sites-available/shophoa-api`:
```nginx
server {
    listen 80;
    server_name api.shophoa.vn;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.shophoa.vn;

    ssl_certificate /etc/letsencrypt/live/api.shophoa.vn/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.shophoa.vn/privkey.pem;

    client_max_body_size 50M;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

---

## 4. CẤU HÌNH BẢO MẬT MÁY CHỦ & CƠ SỞ DỮ LIỆU

### 4.1. Đóng cổng MySQL 3306 ra ngoài Internet
Trên máy chủ Linux chạy lệnh firewall:
```bash
# Chỉ cho phép SSH (22), HTTP (80), HTTPS (443)
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# Chặn cổng MySQL 3306
sudo ufw deny 3306/tcp
sudo ufw enable
```

### 4.2. Cấu hình MySQL chỉ lắng nghe nội bộ (Localhost)
Mở file cấu hình MySQL `/etc/mysql/mariadb.conf.d/50-server.cnf` (hoặc `my.cnf`):
```ini
[mysqld]
bind-address = 127.0.0.1
```
Khởi động lại MySQL:
```bash
sudo systemctl restart mariadb
```

---

## 5. CẤU HÌNH MÁY CHỦ CẬP NHẬT TỪ XA (VELOPACK UPDATE SERVER)

Velopack cần một máy chủ Web tĩnh hỗ trợ **HTTP Range Requests** để cung cấp các bản tải delta:

### 5.1. Cấu hình Nginx phục vụ thư mục cập nhật
```nginx
server {
    listen 443 ssl http2;
    server_name update.shophoa.vn;

    ssl_certificate /etc/letsencrypt/live/update.shophoa.vn/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/update.shophoa.vn/privkey.pem;

    root /var/www/shophoa-updates/releases;

    location / {
        autoindex on;
        sendfile on;
        sendfile_max_chunk 1m;
        tcp_nopush on;
        
        # Bắt buộc cho Velopack:
        add_header Accept-Ranges bytes;
    }
}
```

---

## 6. QUY TRÌNH SAO LƯU (BACKUP) VÀ KHÔI PHỤC (RESTORE)

### 6.1. Lệnh Sao lưu Database
```bash
# Tạo file backup nén gz có chứa timestamp
mysqldump -u root -p --single-transaction --routines --triggers hoa_shop | gzip > /var/backups/shophoa_backup_$(date +\%Y\%m\%d_\%H\%M\%S).sql.gz
```

### 6.2. Thiết lập Cronjob tự động sao lưu mỗi đêm (01:00 AM)
Chạy `crontab -e` và thêm dòng sau:
```cron
0 1 * * * mysqldump -u root -p'MatKhauRoot' --single-transaction --routines --triggers hoa_shop | gzip > /var/backups/shophoa_$(date +\%Y\%m\%d).sql.gz && find /var/backups -type f -mtime +30 -name '*.sql.gz' -delete
```
*(Lệnh trên tự động xóa các file backup cũ hơn 30 ngày để tiết kiệm dung lượng đĩa)*.

### 6.3. Khôi phục dữ liệu khi gặp sự cố (Restore)
```bash
# Giải nén và nạp lại vào database
gunzip < /var/backups/shophoa_backup_YYYYMMDD_HHMMSS.sql.gz | mysql -u root -p hoa_shop
```

---

## 7. QUY TRÌNH PHÁT HÀNH PHIÊN BẢN MỚI (RELEASE WORKFLOW)

Khi cần phát hành một bản cập nhật mới (ví dụ từ `1.0.0` lên `1.0.1`):

1. **Bước 1: Chạy lệnh đóng gói phiên bản mới:**
   ```powershell
   .\pack.ps1 -Target win-x64 -Version 1.0.1
   ```
2. **Bước 2: Kiểm tra thư mục đầu ra `Releases/win-x64/`:**
   - File manifest: `releases.win.json` (tự cập nhật checksum SHA256)
   - Gói delta siêu nhẹ: `ShopHoa-1.0.1-delta.nupkg` (thường chỉ vài trăm KB đến vài MB)
   - Gói đầy đủ: `ShopHoa-1.0.1-full.nupkg`
   - File cài đặt máy mới: `ShopHoa-Setup.exe`
3. **Bước 3: Tải các file mới lên máy chủ:**
   - Copy toàn bộ file trong thư mục `Releases/win-x64/` lên thư mục `/var/www/shophoa-updates/releases/` trên server.
4. **Bước 4: Người dùng tự động nhận bản cập nhật:**
   - Khách hàng khi mở phần mềm sẽ nhận được thông báo phiên bản mới sau 3 giây.
   - Hoặc khách hàng bấm vào menu tài khoản góc trái dưới -> Chọn **"🔄 Cập nhật phần mềm"** -> Bấm **"Tải và Cập nhật"** -> Bấm **"Khởi động lại ngay"**.
   - Cập nhật diễn ra trong vài giây, toàn bộ dữ liệu cấu hình và nghiệp vụ bán hàng giữ nguyên vẹn 100%.
