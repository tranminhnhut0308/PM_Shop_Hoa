
#!/usr/bin/env bash
# ==============================================================================
# SHOPHOA - BUILD & PACK SCRIPT
# Usage:
#   ./pack.sh -Target linux-x64 -Version 1.1.1
#   ./pack.sh -Target win-x64 -Version 1.1.1
#   ./pack.sh -Target all -Version 1.1.1
# ==============================================================================

set -Eeuo pipefail

TARGET="linux-x64"
VERSION="1.0.0"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -Target|--target|-target|-t)
            [[ $# -ge 2 ]] || { echo "Thiếu Target"; exit 1; }
            TARGET="$2"
            shift 2
            ;;
        -Version|--version|-version|-v)
            [[ $# -ge 2 ]] || { echo "Thiếu Version"; exit 1; }
            VERSION="$2"
            shift 2
            ;;
        *)
            echo "Tham số không hợp lệ: $1"
            exit 1
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

# Validate target
if [[ "$TARGET" == "all" ]]; then
    TARGETS=("win-x64" "linux-x64")
elif [[ "$TARGET" == "win-x64" || "$TARGET" == "linux-x64" ]]; then
    TARGETS=("$TARGET")
else
    echo -e "${RED}Target không hợp lệ: $TARGET${NC}"
    exit 1
fi

# Check project
[[ -f "ShopHoa.csproj" ]] || {
    echo -e "${RED}Không tìm thấy ShopHoa.csproj. Hãy chạy tại thư mục gốc dự án.${NC}"
    exit 1
}

# Check Velopack CLI
echo -e "\n${GREEN}[1/3] Kiểm tra Velopack CLI...${NC}"
export PATH="$PATH:$HOME/.dotnet/tools"

if ! command -v vpk >/dev/null 2>&1; then
    echo -e "${YELLOW}Đang cài đặt Velopack CLI...${NC}"
    dotnet tool install -g vpk
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

command -v vpk >/dev/null 2>&1 || {
    echo -e "${RED}Không tìm thấy vpk!${NC}"
    exit 1
}

for T in "${TARGETS[@]}"; do
    echo -e "\n${CYAN}======================================================${NC}"
    echo -e "${CYAN}ĐÓNG GÓI $T - VERSION $VERSION${NC}"
    echo -e "${CYAN}======================================================${NC}"

    PUBLISH_DIR="publish/$T"
    RELEASE_DIR="Releases/$T"
    PROFILE="Properties/PublishProfiles/$T.pubxml"

    [[ -f "$PROFILE" ]] || {
        echo -e "${RED}Không tìm thấy Publish Profile: $PROFILE${NC}"
        exit 1
    }

    # Publish
    echo -e "\n${GREEN}[2/3] Publish ứng dụng...${NC}"

    rm -rf "$PUBLISH_DIR"

    dotnet publish ShopHoa.csproj \
        -c Release \
        -p:PublishProfile="$PROFILE" \
        -p:Version="$VERSION" \
        -o "$PUBLISH_DIR"

    [[ -f "$PUBLISH_DIR/ShopHoa.dll" ]] || {
        echo -e "${RED}Publish thất bại: Không tìm thấy ShopHoa.dll${NC}"
        exit 1
    }

    MAIN_EXE="ShopHoa"
    if [[ "$T" == "win-x64" ]]; then
        MAIN_EXE="ShopHoa.exe"
    fi

    [[ -f "$PUBLISH_DIR/$MAIN_EXE" ]] || {
        echo -e "${RED}Không tìm thấy file chạy: $PUBLISH_DIR/$MAIN_EXE${NC}"
        exit 1
    }

    # Icon
    ICON_ARGS=()
    if [[ -f "Assets/logo.ico" ]]; then
        ICON_ARGS=("-i" "Assets/logo.ico")
    elif [[ -f "Assets/LOGO_APP.png" ]]; then
        ICON_ARGS=("-i" "Assets/LOGO_APP.png")
    elif [[ -f "Assets/logo.png" ]]; then
        ICON_ARGS=("-i" "Assets/logo.png")
    fi

    mkdir -p "$RELEASE_DIR"

    # Velopack
    echo -e "\n${GREEN}[3/3] Đóng gói Velopack...${NC}"

    if [[ "$T" == "win-x64" ]]; then
        VPK_OS="[win]"
    else
        VPK_OS="[linux]"
    fi

    vpk "$VPK_OS" pack \
        -u ShopHoa \
        -v "$VERSION" \
        -p "$PUBLISH_DIR" \
        -e "$MAIN_EXE" \
        -o "$RELEASE_DIR" \
        --packTitle "ShopHoa" \
        --runtime "$T" \
        "${ICON_ARGS[@]}"

    # Extra Linux installer archive
    if [[ "$T" == "linux-x64" ]]; then
        echo -e "${YELLOW}Tạo gói Linux tar.gz...${NC}"
        tar -czf "$RELEASE_DIR/ShopHoa-linux.tar.gz" -C "$PUBLISH_DIR" .

        if [[ -f "install.sh" ]]; then
            cp -f install.sh "$RELEASE_DIR/install.sh"
        fi
    fi

    # Verify package
    if [[ "$T" == "win-x64" ]]; then
        FULL_PACKAGE="$RELEASE_DIR/ShopHoa-$VERSION-full.nupkg"
    else
        FULL_PACKAGE="$RELEASE_DIR/ShopHoa-$VERSION-linux-full.nupkg"
    fi

    if [[ ! -f "$FULL_PACKAGE" ]]; then
        echo -e "${RED}Không tìm thấy gói đầy đủ: $FULL_PACKAGE${NC}"
        echo -e "${YELLOW}Hãy kiểm tra đầu ra Velopack và cấu trúc thư mục Releases.${NC}"
        exit 1
    fi

    echo -e "${GREEN}ĐÃ TẠO GÓI: $FULL_PACKAGE${NC}"
done

echo -e "\n${GREEN}======================================================${NC}"
echo -e "${GREEN}ĐÓNG GÓI HOÀN TẤT VÀ ĐÃ KIỂM TRA FILE!${NC}"
echo -e "${GREEN}======================================================${NC}"

for T in "${TARGETS[@]}"; do
    echo -e "${CYAN}- Releases/$T${NC}"
done
