#!/bin/bash
# Check Backup Status - Bash Health Check Script
# Returns 0 if OK, 1 if warning, 2 if critical

BACKUP_DIR="${BACKUP_DIR:-/var/backups}"
MAX_AGE_HOURS="${MAX_AGE_HOURS:-24}"

# Find latest backup file
latest_backup=$(find "$BACKUP_DIR" -name "*.bak" -o -name "*.tar.gz" -o -name "*.sql" 2>/dev/null | head -1)

if [ -z "$latest_backup" ]; then
    echo "ERROR: No backup files found in $BACKUP_DIR"
    exit 2
fi

# Check file age
file_age_seconds=$(($(date +%s) - $(stat -c %Y "$latest_backup" 2>/dev/null || stat -f %m "$latest_backup" 2>/dev/null)))
file_age_hours=$((file_age_seconds / 3600))

# Get file size
file_size=$(stat -c %s "$latest_backup" 2>/dev/null || stat -f %z "$latest_backup" 2>/dev/null)
file_size_mb=$((file_size / 1024 / 1024))

# Output info
echo "Latest backup: $(basename "$latest_backup")"
echo "Age: ${file_age_hours}h, Size: ${file_size_mb}MB"

if [ $file_age_hours -gt $((MAX_AGE_HOURS * 2)) ]; then
    echo "CRITICAL: Backup is ${file_age_hours}h old (max: ${MAX_AGE_HOURS}h)"
    exit 2
elif [ $file_age_hours -gt $MAX_AGE_HOURS ]; then
    echo "WARNING: Backup is ${file_age_hours}h old (max: ${MAX_AGE_HOURS}h)"
    exit 1
else
    echo "OK: Backup is recent"
    exit 0
fi

