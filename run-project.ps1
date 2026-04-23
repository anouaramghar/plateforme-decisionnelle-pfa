# Script d'automatisation pour la Plateforme Décisionnelle PFA

Write-Host "🚀 Démarrage de l'automatisation du projet..." -ForegroundColor Cyan

# 1. Vérification de Docker
if (!(Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Error "❌ Docker n'est pas installé ou n'est pas dans le PATH. Veuillez l'installer pour continuer."
    exit
}

# 2. Installation des dépendances Frontend si nécessaire
if (!(Test-Path "frontend/node_modules")) {
    Write-Host "📦 Installation des dépendances frontend (npm install)..." -ForegroundColor Yellow
    Set-Location frontend
    npm install
    Set-Location ..
}

# 3. Lancement des conteneurs via Docker Compose
Write-Host "🐳 Lancement des services (Docker Compose)..." -ForegroundColor Green
docker-compose up -d --build

Write-Host "`n✅ Le projet est en cours de démarrage !" -ForegroundColor Green
Write-Host "🌐 Frontend : http://localhost:80" -ForegroundColor Cyan
Write-Host "🔗 API Swagger : http://localhost:8080/swagger" -ForegroundColor Cyan

# 4. Attente de la santé du backend (optionnel)
Write-Host "`n⏳ Patientez quelques instants pendant que la base de données s'initialise..." -ForegroundColor Gray
