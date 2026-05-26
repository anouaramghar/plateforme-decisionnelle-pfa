# Mirrors ml_service/Dockerfile so ops feel familiar.
FROM python:3.11-slim AS runtime

WORKDIR /app

# System deps kept minimal — no native ML libs needed.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Same internal port pattern as ml_service (8000); we use 8001 for agent-service.
EXPOSE 8001

# Single-worker is fine: the workload is I/O-bound on LLM calls, and concurrency
# comes from async (FastAPI + httpx).
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8001"]
