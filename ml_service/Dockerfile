FROM python:3.11-slim
WORKDIR /app

# Installation des dépendances (Nécessite un fichier requirements.txt avec fastapi, uvicorn, scikit-learn, xgboost)
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copie du code FastAPI
COPY . .

EXPOSE 8000
# Lancement de l'API ML
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]