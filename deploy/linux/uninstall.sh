#!/bin/bash
# Emistr Services - Linux Uninstall Script
# Run as root or with sudo

set -e

INSTALL_PATH="${INSTALL_PATH:-/opt/emistr}"
SERVICE_USER="${SERVICE_USER:-emistr}"
LOG_PATH="${LOG_PATH:-/var/log/emistr}"
REMOVE_FILES="${REMOVE_FILES:-false}"
REMOVE_USER="${REMOVE_USER:-false}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --remove-files)
            REMOVE_FILES=true
            shift
            ;;
        --remove-user)
            REMOVE_USER=true
            shift
            ;;
        --remove-all)
            REMOVE_FILES=true
            REMOVE_USER=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--remove-files] [--remove-user] [--remove-all]"
            exit 1
            ;;
    esac
done

echo -e "${CYAN}========================================"
echo -e "  Emistr Services Uninstall Script     "
echo -e "========================================${NC}"
echo ""

# Check root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}Error: This script must be run as root${NC}"
    exit 1
fi

# Stop and disable services
echo -e "${YELLOW}[1/4] Stopping and disabling services...${NC}"

SERVICES=("emistr-license-service" "emistr-watchdog")

for service in "${SERVICES[@]}"; do
    if systemctl is-active --quiet "$service" 2>/dev/null; then
        echo -e "  Stopping: $service"
        systemctl stop "$service"
    fi
    
    if systemctl is-enabled --quiet "$service" 2>/dev/null; then
        echo -e "  Disabling: $service"
        systemctl disable "$service"
    fi
    
    if [ -f "/etc/systemd/system/$service.service" ]; then
        rm "/etc/systemd/system/$service.service"
        echo -e "  ${GREEN}Removed: /etc/systemd/system/$service.service${NC}"
    fi
done

# Reload systemd
echo ""
echo -e "${YELLOW}[2/4] Reloading systemd...${NC}"
systemctl daemon-reload
echo -e "  ${GREEN}Systemd reloaded${NC}"

# Remove files
echo ""
echo -e "${YELLOW}[3/4] Removing files...${NC}"
if [ "$REMOVE_FILES" = true ]; then
    if [ -d "$INSTALL_PATH" ]; then
        rm -rf "$INSTALL_PATH"
        echo -e "  ${GREEN}Removed: $INSTALL_PATH${NC}"
    fi
    
    if [ -d "$LOG_PATH" ]; then
        rm -rf "$LOG_PATH"
        echo -e "  ${GREEN}Removed: $LOG_PATH${NC}"
    fi
else
    echo -e "  ${YELLOW}Keeping files. Use --remove-files to delete.${NC}"
    echo "  Installation path: $INSTALL_PATH"
    echo "  Log path: $LOG_PATH"
fi

# Remove user
echo ""
echo -e "${YELLOW}[4/4] Removing service user...${NC}"
if [ "$REMOVE_USER" = true ]; then
    if id "$SERVICE_USER" &>/dev/null; then
        userdel "$SERVICE_USER"
        echo -e "  ${GREEN}Removed user: $SERVICE_USER${NC}"
    else
        echo -e "  User not found: $SERVICE_USER"
    fi
else
    echo -e "  ${YELLOW}Keeping user. Use --remove-user to delete.${NC}"
fi

# Summary
echo ""
echo -e "${CYAN}========================================"
echo -e "Uninstallation Complete!"
echo -e "========================================${NC}"
echo ""

# Check remaining
remaining=$(systemctl list-units --type=service --all | grep -c "emistr" || true)
if [ "$remaining" -gt 0 ]; then
    echo -e "${YELLOW}Remaining Emistr services:${NC}"
    systemctl list-units --type=service --all | grep "emistr" || true
else
    echo -e "${GREEN}All Emistr services have been removed.${NC}"
fi
