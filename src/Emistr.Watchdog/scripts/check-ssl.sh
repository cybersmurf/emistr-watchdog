#!/bin/bash
# check-ssl.sh - SSL Certificate Expiry Check
# Usage: ./check-ssl.sh hostname [port] [warn_days] [crit_days]
# Exit codes: 0=OK, 1=WARNING, 2=CRITICAL

HOST="${1:-localhost}"
PORT="${2:-443}"
WARN_DAYS="${3:-30}"
CRIT_DAYS="${4:-7}"

# Check if openssl exists
if ! command -v openssl &> /dev/null; then
    echo "CRITICAL: openssl not found"
    exit 2
fi

# Get certificate expiry date
EXPIRY=$(echo | openssl s_client -servername "$HOST" -connect "$HOST:$PORT" 2>/dev/null | openssl x509 -noout -enddate 2>/dev/null | cut -d= -f2)

if [ -z "$EXPIRY" ]; then
    echo "CRITICAL: Cannot get SSL certificate from $HOST:$PORT"
    exit 2
fi

# Calculate days until expiry
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    EXPIRY_EPOCH=$(date -j -f "%b %d %H:%M:%S %Y %Z" "$EXPIRY" +%s 2>/dev/null)
else
    # Linux
    EXPIRY_EPOCH=$(date -d "$EXPIRY" +%s 2>/dev/null)
fi

NOW_EPOCH=$(date +%s)
DAYS_LEFT=$(( (EXPIRY_EPOCH - NOW_EPOCH) / 86400 ))

if [ $DAYS_LEFT -lt 0 ]; then
    echo "CRITICAL: SSL certificate EXPIRED $((-DAYS_LEFT)) days ago"
    exit 2
elif [ $DAYS_LEFT -lt $CRIT_DAYS ]; then
    echo "CRITICAL: SSL certificate expires in $DAYS_LEFT days (< $CRIT_DAYS)"
    exit 2
elif [ $DAYS_LEFT -lt $WARN_DAYS ]; then
    echo "WARNING: SSL certificate expires in $DAYS_LEFT days (< $WARN_DAYS)"
    exit 1
else
    echo "OK: SSL certificate valid for $DAYS_LEFT days (expires: $EXPIRY)"
    exit 0
fi

