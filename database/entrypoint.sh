#!/bin/bash
set -euo pipefail

: "${SA_PASSWORD:?SA_PASSWORD is required}"
: "${APP_DB_PASSWORD:?APP_DB_PASSWORD is required}"

# Start SQL Server in background
/opt/mssql/bin/sqlservr &
MSSQL_PID=$!

cleanup() {
    if kill -0 "$MSSQL_PID" 2>/dev/null; then
        kill "$MSSQL_PID"
    fi
}
trap cleanup EXIT

# Cold first boot — and any boot where SQL Server applies internal script
# upgrades to the existing volume after an image bump — keeps the server in
# "script upgrade mode" and rejects logins for several minutes. 30×2s (60s)
# isn't enough; bump to 150×2s (5min) so we ride out the worst case instead
# of exiting and letting Docker restart-loop us forever.
echo "Waiting for SQL Server to be ready..."
READY=0
for i in $(seq 1 150); do
    if SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -Q "SELECT 1" -C > /dev/null 2>&1; then
        READY=1
        break
    fi
    echo "  attempt $i/150..."
    sleep 2
done

if [ "$READY" -ne 1 ]; then
    echo "ERROR: SQL Server did not become ready in time." >&2
    exit 1
fi

echo "Checking if database already initialized..."
RESULT=$(SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -C \
    -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='PFA_DB'" \
    -h -1 2>/dev/null | tr -d ' \r\n')

if [ "$RESULT" = "0" ]; then
    echo "Running init.sql..."
    SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -C -b \
        -v APP_PASSWORD="$APP_DB_PASSWORD" \
        -i /scripts/init.sql

    echo "Running init_dw.sql..."
    SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -C -b \
        -i /scripts/init_dw.sql

    echo "Database initialization complete."
else
    echo "Database PFA_DB already exists — skipping initialization."
fi

# Clear the trap — from here we want sqlservr to own the foreground.
trap - EXIT
wait $MSSQL_PID
