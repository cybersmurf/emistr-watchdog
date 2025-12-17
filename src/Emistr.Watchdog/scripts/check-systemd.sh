#!/bin/bash
# check-systemd.sh - Linux systemd Service Health Check
# Usage: ./check-systemd.sh service-name
# Exit codes: 0=OK, 1=WARNING, 2=CRITICAL

SERVICE_NAME="$1"

if [ -z "$SERVICE_NAME" ]; then
    echo "CRITICAL: Service name not provided"
    echo "Usage: $0 <service-name>"
    exit 2
fi

# Check if systemctl exists
if ! command -v systemctl &> /dev/null; then
    echo "CRITICAL: systemctl not found (not a systemd system?)"
    exit 2
fi

# Check if service exists
if ! systemctl list-unit-files | grep -q "^$SERVICE_NAME"; then
    echo "CRITICAL: Service '$SERVICE_NAME' not found"
    exit 2
fi

# Get service status
STATUS=$(systemctl is-active "$SERVICE_NAME" 2>/dev/null)

case "$STATUS" in
    "active")
        # Get additional info
        UPTIME=$(systemctl show "$SERVICE_NAME" --property=ActiveEnterTimestamp --value 2>/dev/null)
        MEMORY=$(systemctl show "$SERVICE_NAME" --property=MemoryCurrent --value 2>/dev/null)
        
        if [ -n "$UPTIME" ] && [ "$UPTIME" != "" ]; then
            echo "OK: Service '$SERVICE_NAME' is running (since $UPTIME)"
        else
            echo "OK: Service '$SERVICE_NAME' is running"
        fi
        exit 0
        ;;
    "inactive")
        echo "CRITICAL: Service '$SERVICE_NAME' is stopped"
        exit 2
        ;;
    "failed")
        # Get failure reason
        REASON=$(systemctl show "$SERVICE_NAME" --property=Result --value 2>/dev/null)
        echo "CRITICAL: Service '$SERVICE_NAME' has failed (reason: $REASON)"
        exit 2
        ;;
    "activating")
        echo "WARNING: Service '$SERVICE_NAME' is starting"
        exit 1
        ;;
    "deactivating")
        echo "WARNING: Service '$SERVICE_NAME' is stopping"
        exit 1
        ;;
    "reloading")
        echo "WARNING: Service '$SERVICE_NAME' is reloading"
        exit 1
        ;;
    *)
        echo "WARNING: Service '$SERVICE_NAME' status: $STATUS"
        exit 1
        ;;
esac

