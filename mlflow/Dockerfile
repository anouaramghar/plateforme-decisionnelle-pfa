# MLflow tracking server.
#
# Uses the FULL mlflow package (not mlflow-skinny) because it runs the server
# + UI, which needs Flask/gunicorn bundled only with the full package. This
# container is isolated from the ml-service env, so the heavier deps here
# cannot conflict with the carefully pinned numpy/scikit-learn in ml_service.
FROM python:3.11-slim

RUN pip install --no-cache-dir mlflow==2.14.3 gunicorn==22.0.0

# /mlflow holds the SQLite backend store + artifact root. Mount a named volume
# so runs, params, metrics, and artifacts survive restarts and re-deploys.
RUN mkdir -p /mlflow
VOLUME ["/mlflow"]

EXPOSE 5000

# backend-store-uri SQLite (file in the volume) keeps run metadata; the
# default-artifact-root on the same volume persists model .joblib artifacts.
CMD ["mlflow", "server", \
     "--host", "0.0.0.0", "--port", "5000", \
     "--backend-store-uri", "sqlite:////mlflow/mlflow.db", \
     "--default-artifact-root", "/mlflow/artifacts", \
     "--serve-artifacts"]
