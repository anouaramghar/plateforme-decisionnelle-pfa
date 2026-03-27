from fastapi import FastAPI

app = FastAPI(
    title="PFA ML Service",
    description="Microservice ML — Plateforme Décisionnelle ENIAD",
    version="1.0.0"
)


@app.get("/health")
def health():
    return {"status": "ok", "service": "ml-service"}


@app.get("/")
def root():
    return {"message": "ML Service is running"}
