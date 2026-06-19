# Mirrors ml_service/Dockerfile so ops feel familiar.
FROM python:3.11-slim AS runtime

WORKDIR /app

# Install Microsoft ODBC 18 driver for SQL Server — required by pyodbc for
# the query_dw tool. Copied from ml_service/Dockerfile.
# NOTE: arch=amd64 hardcoded; will fail on ARM/Apple Silicon.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
        curl gnupg2 apt-transport-https unixodbc-dev \
 && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
 && echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/debian/12/prod bookworm main" \
        > /etc/apt/sources.list.d/mssql-release.list \
 && apt-get update \
 && ACCEPT_EULA=Y apt-get install -y --no-install-recommends msodbcsql18 \
 && apt-get purge -y gnupg2 apt-transport-https \
 && apt-get autoremove -y \
 && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Same internal port pattern as ml_service (8000); we use 8001 for agent-service.
EXPOSE 8001

# Single-worker is fine: the workload is I/O-bound on LLM calls, and concurrency
# comes from async (FastAPI + httpx).
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8001"]
