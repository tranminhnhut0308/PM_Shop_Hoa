#!/usr/bin/env bash
# ==============================================================================
# Script đóng gói phần mềm ShopHoa trên Linux
# Cách dùng:
#   ./pack.sh -Target linux-x64 -Version 1.0.3
#   hoặc: ./pack.sh linux-x64 1.0.3
# ==============================================================================

set -e

TARGET="linux-x64"
VERSION="1.0.0"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -Target|--target)
            TARGET="$2"
            shift 2
            ;;
        -Version|--version)
            VERSION="$2"
            shift 2
            ;;
        *)
            if [[ -z "$POS_ARG_1" ]]; then
                POS_ARG_1="$1"
                TARGET="$1"
            elif [[ -z "$POS_ARG_2" ]]; then
                POS_ARG_2="$1"
                VERSION="$1"
            fi
            shift
            ;;
    esac
done

RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${CYAN}=== BẮT ĐẦU ĐÓNG GÓI SHOPHOA ===${NC}"
echo -e "${YELLOW}Mục tiêu: $TARGET${NC}"
echo -e "${YELLOW}Phiên bản: $VERSION${NC}"

PUBLISH_DIR="publish/$TARGET"
RELEASE_DIR="Releases/$TARGET"

# 1. Kiểm tra Velopack CLI (vpk)
echo -e "\n${GREEN}[1/3] Kiểm tra công cụ Velopack CLI (vpk)...${NC}"
if ! command -v vpk >/dev/null 2>&1; then
    echo -e "${YELLOW}Đang tự động cài đặt công cụ vpk (Velopack CLI)...${NC}"
    dotnet tool install -g vpk || true
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# 2. Xuất bản ứng dụng (Self-contained)
echo -e "\n${GREEN}[2/3] Đang xuất bản dự án ShopHoa (Release, $TARGET, self-contained)...${NC}"
rm -rf "$PUBLISH_DIR"

dotnet publish ShopHoa.csproj -c Release /p:PublishProfile="Properties/PublishProfiles/$TARGET.pubxml" /p:Version="$VERSION"

# 3. Đóng gói bộ cài đặt
echo -e "\n${GREEN}[3/3] Đang đóng gói bộ cài đặt...${NC}"
mkdir -p "$RELEASE_DIR"

MAIN_EXE="ShopHoa"
if [[ "$TARGET" == "win-x64" ]]; then
    MAIN_EXE="ShopHoa.exe"
fi

ICON_ARGS=()
if [[ -f "Assets/logo.ico" ]]; then
    ICON_ARGS=("-i" "Assets/logo.ico")
elif [[ -f "Assets/LOGO_APP.png" ]]; then
    ICON_ARGS=("-i" "Assets/LOGO_APP.png")
fi

if command -v vpk >/dev/null 2>&1; then
    vpk pack -u ShopHoa -v "$VERSION" -p "$PUBLISH_DIR" -e "$MAIN_EXE" -o "$RELEASE_DIR" --packTitle "ShopHoa" --runtime "$TARGET" "${ICON_ARGS[@]}" || true
fi

if [[ "$TARGET" == "linux-x64" ]]; then
    echo -e "${YELLOW}Đang tạo gói ShopHoa-linux.tar.gz phục vụ cài đặt 1 dòng...${NC}"
    tar -czf "$RELEASE_DIR/ShopHoa-linux.tar.gz" -C "$PUBLISH_DIR" .
    cp -f "install.sh" "$RELEASE_DIR/install.sh" 2>/dev/null || true
    echo -e "${YELLOW}File cài đặt Linux: $RELEASE_DIR/ShopHoa-linux.tar.gz và install.sh${NC}"
elif [[ "$TARGET" == "win-x64" ]]; then
    echo -e "${YELLOW}File cài đặt Windows: $RELEASE_DIR/ShopHoa-win-Setup.exe${NC}"
fi

echo -e "\n${GREEN}=== ĐÓNG GÓI HOÀN TẤT THÀNH CÔNG ===${NC}"
echo -e "${CYAN}Thư mục chứa file cài đặt: $RELEASE_DIR${NC}\n"
