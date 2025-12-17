#!/bin/bash
# check-disk.sh - Disk Space Check
# Usage: ./check-disk.sh [path] [warn_percent] [crit_percent]
# Exit codes: 0=OK, 1=WARNING, 2=CRITICAL

PATH_TO_CHECK="${1:-/}"
WARN_PERCENT="${2:-80}"
CRIT_PERCENT="${3:-90}"

# Get disk usage
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS - different df output format
    DISK_INFO=$(df -h "$PATH_TO_CHECK" 2>/dev/null | tail -1)
    USAGE=$(echo "$DISK_INFO" | awk '{print $5}' | tr -d '%')
    AVAIL=$(echo "$DISK_INFO" | awk '{print $4}')
    TOTAL=$(echo "$DISK_INFO" | awk '{print $2}')
else
    # Linux
    DISK_INFO=$(df -h "$PATH_TO_CHECK" 2>/dev/null | tail -1)
    USAGE=$(echo "$DISK_INFO" | awk '{print $5}' | tr -d '%')
    AVAIL=$(echo "$DISK_INFO" | awk '{print $4}')
    TOTAL=$(echo "$DISK_INFO" | awk '{print $2}')
fi

if [ -z "$USAGE" ]; then
    echo "CRITICAL: Cannot get disk info for $PATH_TO_CHECK"
    exit 2
fi

if [ "$USAGE" -ge "$CRIT_PERCENT" ]; then
    echo "CRITICAL: Disk usage ${USAGE}% on $PATH_TO_CHECK (available: $AVAIL of $TOTAL)"
    exit 2
elif [ "$USAGE" -ge "$WARN_PERCENT" ]; then
    echo "WARNING: Disk usage ${USAGE}% on $PATH_TO_CHECK (available: $AVAIL of $TOTAL)"
    exit 1
else
    echo "OK: Disk usage ${USAGE}% on $PATH_TO_CHECK (available: $AVAIL of $TOTAL)"
    exit 0
fi

