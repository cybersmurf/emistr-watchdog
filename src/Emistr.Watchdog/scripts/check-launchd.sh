#!/bin/bash
# check-launchd.sh - macOS launchd Service Health Check
# Usage: ./check-launchd.sh com.example.service
# Exit codes: 0=OK, 1=WARNING, 2=CRITICAL

SERVICE_NAME="$1"

if [ -z "$SERVICE_NAME" ]; then
    echo "CRITICAL: Service name not provided"
    echo "Usage: $0 <service-label>"
    exit 2
fi

# Check if launchctl exists
if ! command -v launchctl &> /dev/null; then
    echo "CRITICAL: launchctl not found (not macOS?)"
    exit 2
fi

# Get service info from launchctl list
SERVICE_INFO=$(launchctl list 2>/dev/null | grep "$SERVICE_NAME")

if [ -z "$SERVICE_INFO" ]; then
    echo "CRITICAL: Service '$SERVICE_NAME' not found"
    exit 2
fi

# Parse PID and status
PID=$(echo "$SERVICE_INFO" | awk '{print $1}')
STATUS=$(echo "$SERVICE_INFO" | awk '{print $2}')
LABEL=$(echo "$SERVICE_INFO" | awk '{print $3}')

if [ "$PID" = "-" ]; then
    # Service is loaded but not running
    if [ "$STATUS" = "0" ]; then
        echo "CRITICAL: Service '$SERVICE_NAME' is stopped (exit code: 0)"
        exit 2
    else
        echo "CRITICAL: Service '$SERVICE_NAME' failed with exit code: $STATUS"
        exit 2
    fi
elif [[ "$PID" =~ ^[0-9]+$ ]]; then
    # Service is running with a PID
    echo "OK: Service '$SERVICE_NAME' is running (PID: $PID)"
    exit 0
else
    echo "WARNING: Service '$SERVICE_NAME' status unknown"
    exit 1
fi

