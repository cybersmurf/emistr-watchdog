#!/bin/bash
# check-mariadb.sh - MariaDB/MySQL Health Check
# Usage: ./check-mariadb.sh
# Environment variables: DB_HOST, DB_PORT, DB_USER, DB_PASS
# Exit codes: 0=OK, 1=WARNING, 2=CRITICAL

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-3306}"
DB_USER="${DB_USER:-watchdog}"
DB_PASS="${DB_PASS:-}"

# Check if mysql client exists
if ! command -v mysql &> /dev/null; then
    echo "CRITICAL: mysql client not found"
    exit 2
fi

# Test connection with simple query
RESULT=$(mysql -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER" -p"$DB_PASS" -e "SELECT 1 AS health;" 2>&1)
EXIT_CODE=$?

if [ $EXIT_CODE -ne 0 ]; then
    echo "CRITICAL: Cannot connect to MariaDB at $DB_HOST:$DB_PORT - $RESULT"
    exit 2
fi

if echo "$RESULT" | grep -q "health"; then
    # Check replication status (if applicable)
    SLAVE_STATUS=$(mysql -h "$DB_HOST" -P "$DB_PORT" -u "$DB_USER" -p"$DB_PASS" -e "SHOW SLAVE STATUS\G" 2>&1)
    
    if echo "$SLAVE_STATUS" | grep -q "Slave_IO_Running"; then
        IO_RUNNING=$(echo "$SLAVE_STATUS" | grep "Slave_IO_Running" | awk '{print $2}')
        SQL_RUNNING=$(echo "$SLAVE_STATUS" | grep "Slave_SQL_Running" | awk '{print $2}')
        SECONDS_BEHIND=$(echo "$SLAVE_STATUS" | grep "Seconds_Behind_Master" | awk '{print $2}')
        
        if [ "$IO_RUNNING" = "Yes" ] && [ "$SQL_RUNNING" = "Yes" ]; then
            if [ -n "$SECONDS_BEHIND" ] && [ "$SECONDS_BEHIND" != "NULL" ] && [ "$SECONDS_BEHIND" -gt 60 ]; then
                echo "WARNING: MariaDB replication lag: ${SECONDS_BEHIND}s"
                exit 1
            fi
            echo "OK: MariaDB healthy, replication running (lag: ${SECONDS_BEHIND:-0}s)"
            exit 0
        else
            echo "CRITICAL: MariaDB replication broken (IO: $IO_RUNNING, SQL: $SQL_RUNNING)"
            exit 2
        fi
    else
        # Not a replica, standalone server
        echo "OK: MariaDB healthy (standalone)"
        exit 0
    fi
else
    echo "CRITICAL: Unexpected response from MariaDB"
    exit 2
fi

