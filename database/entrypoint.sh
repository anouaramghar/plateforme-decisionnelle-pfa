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
# "script upgrade mode" and rejects logins for several minutes. 450×2s (15min)
# so we ride out the worst case instead of exiting and letting Docker
# restart-loop us forever.
echo "Waiting for SQL Server to be ready..."
READY=0
for i in $(seq 1 450); do
    if SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -Q "SELECT 1" -C > /dev/null 2>&1; then
        READY=1
        break
    fi
    echo "  attempt $i/450..."
    sleep 2
done

if [ "$READY" -ne 1 ]; then
    echo "ERROR: SQL Server did not become ready in time." >&2
    exit 1
fi

echo "Waiting for user databases to be ONLINE..."
for i in $(seq 1 150); do
    STATUS=$(SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -C \
        -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE state_desc <> 'ONLINE' AND name IN ('PFA_DB', 'PFA_DW')" \
        -h -1 2>/dev/null | tr -d ' \r\n')
    if [ "$STATUS" = "0" ]; then
        break
    fi
    echo "  waiting for databases to online (remaining: $STATUS), attempt $i/150..."
    sleep 2
done

# init.sql and init_dw.sql are fully idempotent — every CREATE is guarded by
# IF OBJECT_ID / IF NOT EXISTS and the SchemaState marker is upserted. Run them
# on EVERY boot (not just cold first boot) so pre-existing volumes roll forward:
# a volume seeded before SchemaState (or any later additive change) existed would
# otherwise never receive the schema marker and stay permanently unhealthy. -b
# aborts the boot on any SQL error.
echo "Running init.sql (idempotent)..."
SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -C -b \
    -v APP_PASSWORD="$APP_DB_PASSWORD" \
    -i /scripts/init.sql

echo "Running init_dw.sql (idempotent)..."
SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -C -b \
    -i /scripts/init_dw.sql

echo "Database initialization complete."

# Always run the Copilot read-only provisioning — idempotent, and crucially it
# must roll forward onto volumes seeded BEFORE Copilot existed (the init.sql /
# init_dw.sql branch above only fires on cold first boot).
if [ -n "${READONLY_DB_PASSWORD:-}" ]; then
    echo "Ensuring Copilot read-only login (idempotent)..."
    SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -C -b \
        -v READONLY_PASSWORD="$READONLY_DB_PASSWORD" \
        -i /scripts/copilot_readonly.sql
else
    echo "READONLY_DB_PASSWORD not set — skipping Copilot read-only login configuration."
fi

# Clear the trap — from here we want sqlservr to own the foreground.
trap - EXIT
wait $MSSQL_PID
