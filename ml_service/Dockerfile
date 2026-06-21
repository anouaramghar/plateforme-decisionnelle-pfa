FROM python:3.11-slim

WORKDIR /app

# Install Microsoft ODBC 18 driver for SQL Server connectivity to PFA_DW.
# Required by pyodbc to query the Data Warehouse for real training data.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
        curl gnupg2 apt-transport-https unixodbc-dev \
 && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
 && echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-prod.gpg] https://packages.microsoft.com/debian/12/prod bookworm main" \
        > /etc/apt/sources.list.d/mssql-release.list \
 && apt-get update \
 && ACCEPT_EULA=Y apt-get install -y --no-install-recommends msodbcsql18 libgssapi-krb5-2 \
 && apt-get purge -y curl gnupg2 apt-transport-https \
 && apt-get autoremove -y \
 && rm -rf /var/lib/apt/lists/*

# Install deps first for layer caching.
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code.
COPY . .

# Create a non-root user and hand over /app.
# saved_models/ is excluded from the build context (.dockerignore) so we must
# mkdir it here — otherwise the Docker named-volume mounts root-owned and
# appuser gets PermissionError when saving .joblib files after training.
RUN adduser --system --no-create-home --group appuser \
 && mkdir -p /app/saved_models \
 && chown -R appuser:appuser /app
USER appuser

EXPOSE 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
