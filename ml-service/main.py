from fastapi import FastAPI
from routers import predict, cluster, forecast

app = FastAPI(title="PFA ML Service", version="1.0.0")

app.include_router(predict.router, prefix="/predict", tags=["Prediction"])
app.include_router(cluster.router, prefix="/cluster", tags=["Clustering"])
app.include_router(forecast.router, prefix="/forecast", tags=["Forecast"])


@app.get("/health")
def health():
    return {"status": "ok"}
