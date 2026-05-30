# Roadmap — Plateforme Décisionnelle ENIAD 2025/2026
**Équipe** : AIT ALI Marouane · AMGHAR Anouar · ENGAR  
**Encadrant** : Prof. LHIADI  
**Stack** : ASP.NET Core 8 · SQL Server · FastAPI · React 19 · Docker · Nginx  
**Dernière mise à jour** : 2026-05-30

---

## Phase 1 — Infrastructure & Base de données ✅

- [x] Docker Compose (db, backend, ml-service, frontend, nginx, agent-service)
- [x] Réseaux Docker isolés (`pfa_internal_db`, `pfa_internal_ml`, `pfa_public`)
- [x] SQL Server containerisé avec `init.sql` et `init_dw.sql`
- [x] Base OLTP `PFA_DB` — tables : Utilisateurs, Filieres, Modules, Etudiants, Notes, Absences, Alertes, Predictions, Rapports, AuditEntries, AgentSessions, AgentSessionMessages, AgentAuditLogs, AlertDrafts
- [x] Base DW `PFA_DW` — tables : DimEtudiant, DimModule, DimTemps, FaitNotes
- [x] Script d'entrée sécurisé (`entrypoint.sh`) avec guard "already initialized"
- [x] Variables d'environnement via `.env`
- [x] Login SQL read-only `pfa_app_readonly` pour le Copilot

---

## Phase 2 — Backend ASP.NET Core 8 ✅

### Authentification & Sécurité
- [x] JWT Bearer authentication
- [x] Register / Login (`AuthController`, `UtilisateurController`)
- [x] Hash BCrypt des mots de passe
- [x] Rôles : Admin, Responsable, Enseignant
- [x] Refresh token — DB-backed via `RefreshTokens` table, single-use rotation
- [x] Middleware de validation des rôles

### Contrôleurs CRUD
- [x] `EtudiantsController` — CRUD + `/with-stats` + `/notes` par étudiant
- [x] `FilieresController` — CRUD
- [x] `ModulesController` — CRUD
- [x] `NotesController` — CRUD + alerte auto sur note < 10
- [x] `AbsencesController` — CRUD + filtres + alerte auto sur absences > 20h
- [x] `AlertesController` — CRUD + résoudre (PATCH)
- [x] `PredictionsController` — CRUD + appel ML service + alerte auto risque élevé + batch + retrain + metrics proxy + summary
- [x] `UtilisateurController` — CRUD + Register
- [x] `DashboardController` — summary (KPIs + deltas + sparkline + charts) + activity feed
- [x] `RapportsController` — generate (PDF/XLSX/CSV) + download + list
- [x] `AdminController` — sync-dw (ETL)

### Logique métier
- [x] **Génération automatique des alertes** (`AlerteService`)
- [x] **Intégration ML → Backend** — `PredictionsController.PredictForEtudiant`
- [x] **ETL OLTP → DW** — `AdminController.SyncDataWarehouse`
- [x] **Journal d'audit** — `AuditService` + `AuditEntries` table

### Copilot backend
- [x] `CopilotController` — `POST /api/copilot/chat` (SSE proxy vers agent-service)
- [x] `CopilotToolController` — `POST /api/copilot/tool/{name}` (callbacks d'outils, double gate JWT + internal token)
- [x] `AgentServiceClient` (typed HttpClient + SSE passthrough)
- [x] Entités DB : `AgentSession`, `AgentSessionMessage`, `AgentAuditLog`, `AlertDraft`

### Tests
- [x] xUnit avec in-memory EF — Auth, Etudiants, Dashboard, Predictions, Rapports, CopilotController, CopilotToolController

---

## Phase 3 — Service ML (FastAPI) ✅

- [x] Structure FastAPI avec health endpoint
- [x] Endpoint de prédiction de risque (`/predict`)
- [x] Endpoint de clustering étudiant (`/cluster`)
- [x] Endpoint de prévision (`/forecast`)
- [x] Endpoint batch (`/predict/batch` — jusqu'à 500 étudiants)
- [x] Endpoint retrain (`/predict/retrain`)
- [x] Endpoint métriques (`/metrics` — AUC, F1, précision, rappel + eval set persisté)
- [x] Auto-entraînement au démarrage (DW réel → fallback synthétique)
- [x] Sécurisé avec `X-Internal-Token`

---

## Phase 4 — Frontend React 19 ✅

### Auth
- [x] Page Login
- [x] AuthContext + persistance JWT (in-memory + refresh token automatique)
- [x] Routes protégées / publiques
- [x] Axios avec intercepteur 401 → refresh token (single-flight)

### Layout
- [x] `AppShell` (Sidebar + Topbar + CommandPalette)
- [x] Design system : Pill, RiskBar, KpiCard, Avatar, Icon, SectionHeader, Select, FilterChip, Empty, Field

### Charts (ApexCharts)
- [x] `ChartBars` — moyennes par filière
- [x] `ChartHistogram` — distribution des moyennes
- [x] `ChartArea` — tendance absences
- [x] `ChartRadial` — score de risque
- [x] `ChartScatter` — cartographie du risque

### Dashboard
- [x] KPIs en haut avec deltas vs période précédente
- [x] Hero card : étudiants suivis + sparkline 14 semaines
- [x] Répartition du risque ML (barre segmentée)
- [x] Graphiques : notes par filière, distribution, absences
- [x] Tableau : étudiants à surveiller (top risque)
- [x] Feed d'activité récente (audit log, polling 30s)
- [x] Carte modèle ML : AUC/F1/Précision/Rappel en temps réel

### Page Étudiants
- [x] Tableau paginé avec 4 filtres + recherche
- [x] Fiche étudiant (drawer) : notes par module, analyse ML
- [x] Badge de risque coloré (vert/orange/rouge)
- [x] Accessibilité : trap focus, ESC, ARIA

### Page Alertes
- [x] Résumé 3 catégories (NoteFaible, AbsenceExcessive, RisqueEleve)
- [x] Tabs : actives / résolues / toutes
- [x] Résoudre individuellement + "tout marquer lu"
- [x] Export XLSX
- [x] Polling 30s

### Page Prédictions ML
- [x] Lancer une prédiction batch (filière + niveau)
- [x] Scatter plot : moy vs absences, colorié par risque
- [x] Top à risque
- [x] Historique des lots
- [x] Bouton ré-entraîner

### Page Rapports / Exports
- [x] 6 modèles de rapports
- [x] Panneau paramètres (période, filière, format)
- [x] Aperçu document (page de couverture)
- [x] Téléchargement auth-aware (blob via axios)
- [x] Table des rapports récents

---

## Phase 5 — ENIAD Copilot (agent-service) 🔄

Agent IA conversationnel pour le personnel pédagogique, utilisant NVIDIA NIM (LLaMA 3.3 70B).

### P1.A — Fondations ✅
- [x] Microservice `agent-service` (FastAPI, port 8001)
- [x] LLMProvider abstraction + NIMProvider (OpenAI-compatible, streaming)
- [x] Schémas SSE : `token`, `tool_call`, `tool_result`, `done`, `error`
- [x] `/agent/run` SSE endpoint + auth `X-Internal-Token`
- [x] Sessions persistées en DB

### P1.B — Agent loop + premier outil ✅
- [x] Boucle multi-itérations avec cap (L6)
- [x] Registre d'outils filtré par rôle
- [x] Outil `get_student` (lecture profil par matricule)
- [x] Validation des args Pydantic `extra=forbid` (L3)
- [x] `ToolExecutor` — callback vers backend avec JWT + internal token (L2)
- [x] 25 tests pytest (+ 1 skipped pour NIM réel)

### P2 — Deuxième outil read ✅
- [x] Outil `list_at_risk` (threshold + filière + niveau, cap 50 résultats)
- [x] Backend handler + tests
- [x] 32 tests pytest (+ 1 skipped)

### P3 — Outils avancés ⏳
- [ ] `query_dw` — NL → SQL sur `PFA_DW` (L4 safety layer)
- [ ] `explain_risk` — explication des facteurs de risque
- [ ] `draft_alert` — préparer une alerte (Tier-2, avec confirmation L5)

### Frontend Copilot Panel ❌
- [ ] Composant `CopilotPanel` (chat flottant ou page dédiée)
- [ ] Rendu des SSE events (tokens en streaming, chips tool_call/tool_result)
- [ ] Bouton de confirmation pour `draft_alert`
- [ ] Intégration dans `AppShell` (bouton dans Topbar ou Sidebar)

---

## Phase 6 — Intégration & Tests ⏳

- [ ] Test end-to-end du flux complet : login → dashboard → prédiction ML → alerte
- [ ] Test Docker full stack (`docker compose up`) sans erreurs sur volume vierge
- [ ] Test de charge léger (300 étudiants, 1200 notes)

---

## Phase 7 — Finalisation & Soutenance ⏳

- [ ] Nettoyer le code (supprimer commentaires inutiles, dead code)
- [ ] README complet avec instructions de déploiement ✅ (fait)
- [ ] Schéma d'architecture (diagramme)
- [ ] Préparer la démo avec données réalistes
- [ ] Slides de présentation

---

## Résumé de l'état actuel

| Composant | Avancement | Notes |
|-----------|-----------|-------|
| Infrastructure Docker | ✅ 100% | 6 services |
| Base de données OLTP + DW | ✅ 100% | — |
| Backend Auth + CRUD + Logique | ✅ 100% | — |
| Backend Copilot | ✅ 100% | SSE proxy + tool callbacks |
| Service ML | ✅ 100% | predict/batch/retrain/metrics |
| Frontend — 5 pages | ✅ 100% | Données réelles, pas de mocks |
| Copilot agent-service | 🔄 70% | P1.A+P1.B+P2 ok, P3 + UI à faire |
| Tests & Intégration | ⏳ 30% | Tests unitaires ok, E2E manquant |
| Finalisation / Soutenance | ⏳ 0% | — |
