# ============================================================
# PFA - Full Endpoint Test Script
# ============================================================
$BASE = "http://localhost:5135"
$pass = 0; $fail = 0; $results = @()

function Test-Endpoint {
    param($Method, $Uri, $Body, $Token, $Desc, $Expected)
    $headers = @{ "Content-Type" = "application/json" }
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    try {
        $params = @{ Uri = "$BASE$Uri"; Method = $Method; Headers = $headers; UseBasicParsing = $true }
        if ($Body) { $params["Body"] = ($Body | ConvertTo-Json -Depth 5) }
        $r = Invoke-WebRequest @params -ErrorAction Stop
        $status = $r.StatusCode
        $content = $r.Content | ConvertFrom-Json -ErrorAction SilentlyContinue
    } catch {
        $status = [int]$_.Exception.Response.StatusCode
        try { $content = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction SilentlyContinue } catch { $content = $null }
    }
    $ok = if ($Expected) { $Expected -contains $status } else { $status -ge 200 -and $status -lt 300 }
    if ($ok) { $script:pass++ } else { $script:fail++ }
    $icon = if ($ok) { "PASS" } else { "FAIL" }
    $script:results += [PSCustomObject]@{ Status=$icon; Code=$status; Method=$Method; Endpoint=$Uri; Desc=$Desc }
    Write-Host "[$icon] $status $Method $Uri - $Desc"
    return $content
}

Write-Host "`n====== BACKEND HEALTH ======" -ForegroundColor Cyan
Test-Endpoint -Method GET -Uri "/health" -Desc "Health check"

# --- AUTH ---
Write-Host "`n====== AUTH ======" -ForegroundColor Cyan
Test-Endpoint -Method POST -Uri "/api/Auth/login" -Body @{email="bad@x.com";motDePasse="wrong1"} -Desc "Login - invalid creds" -Expected @(401)

$login = Test-Endpoint -Method POST -Uri "/api/Auth/login" -Body @{email="admin@eniad.ma";motDePasse="Admin@ENIAD2026!"} -Desc "Login - admin seed"
if (-not $login.token) {
    # Try registering an admin first
    Write-Host "  -> No seed admin found, trying to register..." -ForegroundColor Yellow
    # Check if there's a register endpoint we can use without auth
    $regBody = @{nom="Admin";prenom="ENIAD";email="admin@eniad.ma";motDePasse="Admin@ENIAD2026!";role="Admin"}
    Test-Endpoint -Method POST -Uri "/api/Auth/register" -Body $regBody -Desc "Register admin (fallback)" -Expected @(200,201,404,405)
    $login = Test-Endpoint -Method POST -Uri "/api/Auth/login" -Body @{email="admin@eniad.ma";motDePasse="Admin@ENIAD2026!"} -Desc "Login - retry"
}
$TOKEN = $login.token
if (-not $TOKEN) { Write-Host "FATAL: Cannot get JWT token. Aborting." -ForegroundColor Red; exit 1 }
Write-Host "  -> JWT obtained OK" -ForegroundColor Green

# Refresh token
if ($login.refreshToken) {
    Test-Endpoint -Method POST -Uri "/api/Auth/refresh" -Body @{refreshToken=$login.refreshToken} -Desc "Refresh token"
}

# --- UTILISATEURS ---
Write-Host "`n====== UTILISATEURS (Admin only) ======" -ForegroundColor Cyan
$users = Test-Endpoint -Method GET -Uri "/api/Utilisateurs" -Token $TOKEN -Desc "List users"
Test-Endpoint -Method GET -Uri "/api/Utilisateurs?page=1&pageSize=5" -Token $TOKEN -Desc "List users (paginated)"

# --- FILIERES ---
Write-Host "`n====== FILIERES ======" -ForegroundColor Cyan
Test-Endpoint -Method GET -Uri "/api/Filieres" -Token $TOKEN -Desc "List filieres"

$newFiliere = Test-Endpoint -Method POST -Uri "/api/Filieres" -Token $TOKEN -Desc "Create filiere" -Body @{code="TEST01";intitule="Filiere Test";responsableId=$null}
$filiereId = $newFiliere.id
if ($filiereId) {
    Test-Endpoint -Method GET -Uri "/api/Filieres/$filiereId" -Token $TOKEN -Desc "Get filiere by ID"
    Test-Endpoint -Method PUT -Uri "/api/Filieres/$filiereId" -Token $TOKEN -Desc "Update filiere" -Body @{code="TEST01";intitule="Filiere Test Updated";responsableId=$null}
}

# --- MODULES ---
Write-Host "`n====== MODULES ======" -ForegroundColor Cyan
Test-Endpoint -Method GET -Uri "/api/Modules" -Token $TOKEN -Desc "List modules"

if ($filiereId) {
    $newModule = Test-Endpoint -Method POST -Uri "/api/Modules" -Token $TOKEN -Desc "Create module" -Body @{code="MOD01";nom="Module Test";filiereId=$filiereId;niveau="L1";coefficient=2;semestre="S1"}
    $moduleId = $newModule.id
    if ($moduleId) {
        Test-Endpoint -Method GET -Uri "/api/Modules/$moduleId" -Token $TOKEN -Desc "Get module by ID"
        Test-Endpoint -Method PUT -Uri "/api/Modules/$moduleId" -Token $TOKEN -Desc "Update module" -Body @{code="MOD01";nom="Module Test Updated";filiereId=$filiereId;niveau="L1";coefficient=3;semestre="S1"}
    }
}

# --- ETUDIANTS ---
Write-Host "`n====== ETUDIANTS ======" -ForegroundColor Cyan
Test-Endpoint -Method GET -Uri "/api/Etudiants" -Token $TOKEN -Desc "List etudiants"
Test-Endpoint -Method GET -Uri "/api/Etudiants?page=1&pageSize=5" -Token $TOKEN -Desc "List etudiants (paginated)"

if ($filiereId) {
    $newEtud = Test-Endpoint -Method POST -Uri "/api/Etudiants" -Token $TOKEN -Desc "Create etudiant" -Body @{matricule="TST001";nom="Doe";prenom="John";email="john@test.com";filiereId=$filiereId;niveau="L1";annee="2025/2026"}
    $etudiantId = $newEtud.id
    if ($etudiantId) {
        Test-Endpoint -Method GET -Uri "/api/Etudiants/$etudiantId" -Token $TOKEN -Desc "Get etudiant by ID"
        Test-Endpoint -Method PUT -Uri "/api/Etudiants/$etudiantId" -Token $TOKEN -Desc "Update etudiant" -Body @{matricule="TST001";nom="Doe";prenom="Jane";email="jane@test.com";filiereId=$filiereId;niveau="L2";annee="2025/2026"}
    }
}

# --- NOTES ---
Write-Host "`n====== NOTES ======" -ForegroundColor Cyan
Test-Endpoint -Method GET -Uri "/api/Notes" -Token $TOKEN -Desc "List notes"

if ($etudiantId -and $moduleId) {
    $newNote = Test-Endpoint -Method POST -Uri "/api/Notes" -Token $TOKEN -Desc "Create note (triggers alert if <10)" -Body @{etudiantId=$etudiantId;moduleId=$moduleId;noteExamen=8;noteTD=7;noteTP=6;noteFinal=7;annee="2025/2026";semestre="S1"}
    $noteId = $newNote.id
    if ($noteId) {
        Test-Endpoint -Method GET -Uri "/api/Notes/$noteId" -Token $TOKEN -Desc "Get note by ID"
        Test-Endpoint -Method PUT -Uri "/api/Notes/$noteId" -Token $TOKEN -Desc "Update note" -Body @{noteExamen=12;noteTD=11;noteTP=10;noteFinal=11;annee="2025/2026";semestre="S1"}
    }
    Test-Endpoint -Method GET -Uri "/api/Notes/etudiant/$etudiantId" -Token $TOKEN -Desc "Notes by etudiant"
    Test-Endpoint -Method GET -Uri "/api/Notes/module/$moduleId" -Token $TOKEN -Desc "Notes by module"
}

# --- ABSENCES ---
Write-Host "`n====== ABSENCES ======" -ForegroundColor Cyan
Test-Endpoint -Method GET -Uri "/api/absences" -Token $TOKEN -Desc "List absences"

if ($etudiantId -and $moduleId) {
    $newAbs = Test-Endpoint -Method POST -Uri "/api/absences" -Token $TOKEN -Desc "Create absence" -Body @{etudiantId=$etudiantId;moduleId=$moduleId;nombreHeures=4;justifiee=$false;dateAbsence="2026-03-15T08:00:00"}
    $absenceId = $newAbs.id
    if ($absenceId) {
        Test-Endpoint -Method GET -Uri "/api/absences/$absenceId" -Token $TOKEN -Desc "Get absence by ID"
        Test-Endpoint -Method PUT -Uri "/api/absences/$absenceId" -Token $TOKEN -Desc "Update absence" -Body @{nombreHeures=6;justifiee=$true;dateAbsence="2026-03-15T08:00:00"}
    }
    Test-Endpoint -Method GET -Uri "/api/absences/etudiant/$etudiantId" -Token $TOKEN -Desc "Absences by etudiant"
    Test-Endpoint -Method GET -Uri "/api/absences/module/$moduleId" -Token $TOKEN -Desc "Absences by module"
}

# --- ALERTES ---
Write-Host "`n====== ALERTES ======" -ForegroundColor Cyan
Test-Endpoint -Method GET -Uri "/api/Alertes" -Token $TOKEN -Desc "List alertes"
Test-Endpoint -Method GET -Uri "/api/Alertes?resolue=false" -Token $TOKEN -Desc "List unresolved alertes"

if ($etudiantId) {
    $newAlerte = Test-Endpoint -Method POST -Uri "/api/Alertes" -Token $TOKEN -Desc "Create alerte" -Body @{etudiantId=$etudiantId;type="NoteFaible";niveau="Moyen";message="Test alerte"}
    $alerteId = $newAlerte.id
    if ($alerteId) {
        Test-Endpoint -Method GET -Uri "/api/Alertes/$alerteId" -Token $TOKEN -Desc "Get alerte by ID"
        Test-Endpoint -Method PATCH -Uri "/api/Alertes/$alerteId/resoudre" -Token $TOKEN -Desc "Resolve alerte"
    }
    Test-Endpoint -Method GET -Uri "/api/Alertes/etudiant/$etudiantId" -Token $TOKEN -Desc "Alertes by etudiant"
}

# --- PREDICTIONS ---
Write-Host "`n====== PREDICTIONS ======" -ForegroundColor Cyan
Test-Endpoint -Method GET -Uri "/api/Predictions" -Token $TOKEN -Desc "List predictions"

if ($etudiantId) {
    $pred = Test-Endpoint -Method POST -Uri "/api/Predictions/predict/$etudiantId" -Token $TOKEN -Desc "Predict for etudiant (calls ML)"
    if ($pred.id) {
        Test-Endpoint -Method GET -Uri "/api/Predictions/$($pred.id)" -Token $TOKEN -Desc "Get prediction by ID"
    }
    Test-Endpoint -Method GET -Uri "/api/Predictions/etudiant/$etudiantId" -Token $TOKEN -Desc "Predictions by etudiant"
}

# --- ADMIN ---
Write-Host "`n====== ADMIN ======" -ForegroundColor Cyan
Test-Endpoint -Method POST -Uri "/api/admin/sync-dw" -Token $TOKEN -Desc "Sync DW (ETL)"

# --- 401 TESTS (no token) ---
Write-Host "`n====== 401 - UNAUTHORIZED (no token) ======" -ForegroundColor Cyan
Test-Endpoint -Method GET -Uri "/api/Etudiants" -Desc "Etudiants without token" -Expected @(401)
Test-Endpoint -Method GET -Uri "/api/Filieres" -Desc "Filieres without token" -Expected @(401)
Test-Endpoint -Method GET -Uri "/api/Modules" -Desc "Modules without token" -Expected @(401)
Test-Endpoint -Method GET -Uri "/api/Notes" -Desc "Notes without token" -Expected @(401)
Test-Endpoint -Method GET -Uri "/api/absences" -Desc "Absences without token" -Expected @(401)
Test-Endpoint -Method GET -Uri "/api/Alertes" -Desc "Alertes without token" -Expected @(401)
Test-Endpoint -Method GET -Uri "/api/Predictions" -Desc "Predictions without token" -Expected @(401)

# --- CLEANUP ---
Write-Host "`n====== CLEANUP ======" -ForegroundColor Cyan
if ($alerteId)  { Test-Endpoint -Method DELETE -Uri "/api/Alertes/$alerteId" -Token $TOKEN -Desc "Delete alerte" -Expected @(204,404) }
if ($noteId)    { Test-Endpoint -Method DELETE -Uri "/api/Notes/$noteId" -Token $TOKEN -Desc "Delete note" -Expected @(204,404) }
if ($absenceId) { Test-Endpoint -Method DELETE -Uri "/api/absences/$absenceId" -Token $TOKEN -Desc "Delete absence" -Expected @(204,404) }
if ($etudiantId){ Test-Endpoint -Method DELETE -Uri "/api/Etudiants/$etudiantId" -Token $TOKEN -Desc "Delete etudiant" -Expected @(204,404) }
if ($moduleId)  { Test-Endpoint -Method DELETE -Uri "/api/Modules/$moduleId" -Token $TOKEN -Desc "Delete module" -Expected @(204,404) }
if ($filiereId) { Test-Endpoint -Method DELETE -Uri "/api/Filieres/$filiereId" -Token $TOKEN -Desc "Delete filiere" -Expected @(204,404) }

# --- SUMMARY ---
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "RESULTS: $pass PASSED / $fail FAILED / $($pass+$fail) TOTAL" -ForegroundColor $(if($fail -eq 0){"Green"}else{"Red"})
Write-Host "============================================================`n" -ForegroundColor Cyan
$results | Format-Table -AutoSize
