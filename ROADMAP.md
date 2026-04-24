# Roadmap — Plateforme Décisionnelle ENIAD 2025/2026
**Équipe** : AIT ALI Marouane · AMGHAR Anouar · ENGAR  
**Encadrant** : Prof. LHIADI  
**Stack** : ASP.NET Core 8 · SQL Server · FastAPI · React 19 · Docker · Nginx

---

## Phase 1 — Infrastructure & Base de données ✅

- [x] Docker Compose (db, backend, ml-service, frontend, nginx)
- [x] Réseaux Docker isolés (`pfa_internal_db`, `pfa_internal_ml`, `pfa_public`)
- [x] SQL Server containerisé avec `init.sql` et `init_dw.sql`
- [x] Base OLTP `PFA_DB` — tables : Utilisateurs, Filieres, Modules, Etudiants, Notes, Absences, Alertes, Predictions
- [x] Base DW `PFA_DW` — tables : DimEtudiant, DimModule, DimTemps, FaitNotes
- [x] Script d'entrée sécurisé (`entrypoint.sh`) avec guard "already initialized"
- [x] Variables d'environnement via `.env`

---

## Phase 2 — Backend ASP.NET Core 8 ✅

### Authentification & Sécurité
- [x] JWT Bearer authentication
- [x] Register / Login (`AuthController`, `UtilisateurController`)
- [x] Hash BCrypt des mots de passe
- [x] Rôles : Admin, Responsable, Enseignant
- [x] Refresh token — DB-backed via `RefreshTokens` table, single-use rotation
- [x] Middleware de validation des rôles sur toutes les routes sensibles (audit)

### Contrôleurs CRUD
- [x] `EtudiantsController` — CRUD complet
- [x] `FilieresController` — CRUD complet
- [x] `ModulesController` — CRUD complet
- [x] `NotesController` — CRUD complet + alerte auto sur note < 10
- [x] `AbsencesController` — CRUD + filtres + alerte auto sur absences > 20h
- [x] `AlertesController` — CRUD + résoudre (PATCH)
- [x] `PredictionsController` — CRUD + appel ML service + alerte auto risque élevé
- [x] `UtilisateurController` — CRUD + Register

### Logique métier
- [x] **Génération automatique des alertes** (`AlerteService`) :
  - Étudiant avec NoteFinal < 10 sur un module → alerte `NoteFaible`
  - Étudiant avec absences non justifiées > 20h → alerte `AbsenceExcessive`
  - Déclenché automatiquement lors de la création/modification d'une note ou absence
- [x] **Intégration ML → Backend** — `PredictionsController.PredictForEtudiant` :
  - `POST /api/Predictions/predict/{etudiantId}` → appelle `http://ml-service:8000/predict`
  - Sauvegarde le résultat en base + crée une alerte si risque élevé
- [x] **ETL OLTP → DW** — `AdminController.SyncDataWarehouse` :
  - `POST /api/admin/sync-dw` exécute le script MERGE (etl.sql)
  - Alimente DimEtudiant, DimModule, DimTemps, FaitNotes

### Qualité
- [x] `AuthService` + `IAuthService` — refactorisé : DB-backed refresh tokens (plus dead code)
- [x] `HasPrecision` ajouté sur Coefficient, Notes (5,2), ScoreRisque/Confiance (5,4)
- [x] Swagger documenté (description API, contact, security scheme)

---

## Phase 3 — Service ML (FastAPI) ✅

- [x] Structure FastAPI avec health endpoint
- [x] Endpoint de prédiction de risque (`/predict`)
- [x] Endpoint de clustering étudiant (`/cluster`)
- [x] Endpoint de prévision (`/forecast`)
- [x] Auto-entraînement au démarrage (DW réel → fallback synthétique)
- [x] **Entraînement sur données réelles** — `data/db_loader.py` lit `PFA_DW.FaitNotes` via pyodbc
- [x] **Validation du modèle** — classification_report, ROC-AUC, R², MAE loggés à chaque entraînement
- [x] **Endpoint `/predict/batch`** — prédit pour une liste d'étudiants en un seul appel (limite : 500)
- [x] **Endpoint `/predict/retrain`** — ré-entraîne les 3 modèles à la demande depuis le DW
- [x] Sécurisé avec `X-Internal-Token` sur tous les endpoints (verify_token Depends)
- [x] Features documentées dans les Pydantic schemas (`Field` avec `ge`, `le`, `description`)


---

## Phase 4 — Frontend React 19 ⏳

> **À commencer après validation des Phases 2 & 3**

### Auth (fait)
- [x] Page Login
- [x] Page Register
- [x] AuthContext + persistance JWT
- [x] Routes protégées / publiques

### Dashboard principal
- [ ] KPIs en haut : nb étudiants, taux de réussite, nb alertes actives, nb à risque
- [ ] Graphique : distribution des notes par filière (BarChart)
- [ ] Graphique : taux d'absence par module (LineChart)
- [ ] Graphique : répartition des niveaux de risque (PieChart)
- [ ] Filtre global par filière / semestre / année

### Page Étudiants
- [ ] Tableau paginé avec filtres (filière, niveau, statut)
- [ ] Fiche étudiant : notes, absences, score de risque ML
- [ ] Badge de risque coloré (vert/orange/rouge)

### Page Alertes
- [ ] Liste des alertes actives avec sévérité
- [ ] Filtres par type (absence / note / risque)
- [ ] Marquer comme traitée

### Page Prédictions ML
- [ ] Lancer une prédiction batch sur une filière
- [ ] Tableau des étudiants à risque avec score
- [ ] Graphique d'évolution du risque

### Page Rapports / Exports
- [ ] Export CSV des notes par filière
- [ ] Export PDF du rapport de performance
- [ ] Statistiques agrégées par semestre

---

## Phase 5 — Intégration & Tests ⏳

- [ ] Test end-to-end du flux complet : login → dashboard → prédiction ML → alerte
- [ ] Test Docker full stack (`docker compose up`) sans erreurs
- [ ] Vérifier que le DW est bien alimenté après ETL
- [ ] Test de charge léger (300 étudiants, 1200 notes)
- [ ] Vérifier CORS en production (nginx → backend)

---

## Phase 6 — Finalisation & Soutenance ⏳

- [ ] Nettoyer le code (supprimer commentaires inutiles, dead code)
- [ ] README complet avec instructions de déploiement
- [ ] Schéma d'architecture (diagramme)
- [ ] Préparer la démo avec données réalistes
- [ ] Slides de présentation

---

## Résumé de l'état actuel

| Composant | Avancement | Bloquant |
|-----------|-----------|----------|
| Infrastructure Docker | ✅ 100% | — |
| Base de données OLTP + DW | ✅ 100% | — |
| Backend Auth | ✅ 100% | — |
| Backend CRUD + Logique métier | ✅ 100% | — |
| ML Service | ✅ 100% | — |
| Frontend Auth | ✅ 100% | — |
| Frontend Dashboard | ⏳ 5% | Prêt à commencer |
| Frontend Pages | ⏳ 0% | Dépend dashboard |
| Tests & Intégration | ⏳ 0% | Dépend frontend |

---

*Dernière mise à jour : 2026-04-25*
