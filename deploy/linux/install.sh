#!/bin/bash
# Emistr Services - Linux Installation Script
# Run as root or with sudo

set -e

INSTALL_PATH="${INSTALL_PATH:-/opt/emistr}"
SERVICE_USER="${SERVICE_USER:-emistr}"
LOG_PATH="${LOG_PATH:-/var/log/emistr}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}========================================"
echo -e "  Emistr Services Installation Script  "
echo -e "========================================${NC}"
echo ""

# Check root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}Error: This script must be run as root${NC}"
    exit 1
fi

# Check if .NET runtime is installed
echo -e "${YELLOW}[1/6] Checking .NET Runtime...${NC}"
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --list-runtimes | grep -i "aspnetcore" | head -1)
    echo -e "  ${GREEN}Found: $DOTNET_VERSION${NC}"
else
    echo -e "  ${RED}.NET Runtime not found!${NC}"
    echo -e "  ${YELLOW}Install with:${NC}"
    echo "    Ubuntu/Debian: sudo apt install aspnetcore-runtime-9.0"
    echo "    RHEL/CentOS:   sudo dnf install aspnetcore-runtime-9.0"
    exit 1
fi

# Create service user
echo ""
echo -e "${YELLOW}[2/6] Creating service user...${NC}"
if id "$SERVICE_USER" &>/dev/null; then
    echo -e "  ${GREEN}User '$SERVICE_USER' already exists${NC}"
else
    useradd -r -s /bin/false "$SERVICE_USER"
    echo -e "  ${GREEN}Created user: $SERVICE_USER${NC}"
fi

# Create directories
echo ""
echo -e "${YELLOW}[3/6] Creating directories...${NC}"
DIRECTORIES=(
    "$INSTALL_PATH/license-service"
    "$INSTALL_PATH/license-service/logs"
    "$INSTALL_PATH/watchdog"
    "$INSTALL_PATH/watchdog/logs"
    "$LOG_PATH"
)

for dir in "${DIRECTORIES[@]}"; do
    if [ ! -d "$dir" ]; then
        mkdir -p "$dir"
        echo -e "  ${GREEN}Created: $dir${NC}"
    else
        echo -e "  Already exists: $dir"
    fi
done

chown -R "$SERVICE_USER:$SERVICE_USER" "$INSTALL_PATH"
chown -R "$SERVICE_USER:$SERVICE_USER" "$LOG_PATH"
echo -e "  ${GREEN}Ownership set to $SERVICE_USER${NC}"

# Copy systemd unit files
echo ""
echo -e "${YELLOW}[4/6] Installing systemd unit files...${NC}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -f "$SCRIPT_DIR/emistr-license-service.service" ]; then
    cp "$SCRIPT_DIR/emistr-license-service.service" /etc/systemd/system/
    echo -e "  ${GREEN}Installed: emistr-license-service.service${NC}"
else
    echo -e "  ${YELLOW}Warning: emistr-license-service.service not found in $SCRIPT_DIR${NC}"
fi

if [ -f "$SCRIPT_DIR/emistr-watchdog.service" ]; then
    cp "$SCRIPT_DIR/emistr-watchdog.service" /etc/systemd/system/
    echo -e "  ${GREEN}Installed: emistr-watchdog.service${NC}"
else
    echo -e "  ${YELLOW}Warning: emistr-watchdog.service not found in $SCRIPT_DIR${NC}"
fi

# Reload systemd
echo ""
echo -e "${YELLOW}[5/6] Reloading systemd...${NC}"
systemctl daemon-reload
echo -e "  ${GREEN}Systemd reloaded${NC}"

# Enable services (but don't start yet)
echo ""
echo -e "${YELLOW}[6/6] Enabling services...${NC}"
if [ -f /etc/systemd/system/emistr-license-service.service ]; then
    systemctl enable emistr-license-service
    echo -e "  ${GREEN}Enabled: emistr-license-service${NC}"
fi

if [ -f /etc/systemd/system/emistr-watchdog.service ]; then
    systemctl enable emistr-watchdog
    echo -e "  ${GREEN}Enabled: emistr-watchdog${NC}"
fi

# Summary
echo ""
echo -e "${CYAN}========================================"
echo -e "Installation Complete!"
echo -e "========================================${NC}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "  1. Copy published files:"
echo "     - License Service to: $INSTALL_PATH/license-service/"
echo "     - Watchdog to: $INSTALL_PATH/watchdog/"
echo ""
echo "  2. Configure appsettings.json for each service"
echo ""
echo "  3. Set executable permissions:"
echo "     chmod +x $INSTALL_PATH/license-service/Emistr.LicenseService"
echo "     chmod +x $INSTALL_PATH/watchdog/Emistr.Watchdog"
echo ""
echo "  4. Start services:"
echo "     sudo systemctl start emistr-license-service"
echo "     sudo systemctl start emistr-watchdog"
echo ""
echo -e "${CYAN}Useful commands:${NC}"
echo "  sudo systemctl status emistr-license-service"
echo "  sudo systemctl status emistr-watchdog"
echo "  sudo journalctl -u emistr-license-service -f"
echo "  sudo journalctl -u emistr-watchdog -f"
echo ""
