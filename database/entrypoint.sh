#!/bin/bash
set -e

# Start SQL Server in background
/opt/mssql/bin/sqlservr &
MSSQL_PID=$!

echo "Waiting for SQL Server to be ready..."
for i in $(seq 1 30); do
    SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -Q "SELECT 1" -C > /dev/null 2>&1 && break
    echo "  attempt $i/30..."
    sleep 2
done

echo "Running init.sql..."
SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -C -i /scripts/init.sql

echo "Running init_dw.sql..."
SQLCMDPASSWORD=$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -C -i /scripts/init_dw.sql

echo "Database initialization complete."

wait $MSSQL_PID
