# Roadmap — Plateforme Décisionnelle ENIAD 2025/2026
**Équipe** : AIT ALI Marouane · AMGHAR Anouar · ENGAR Aymen
**Encadrant** : Prof. LHIADI
**Stack** : ASP.NET Core 8 · SQL Server · FastAPI · React 18 · Node/Express (CopilotKit) · Docker · Nginx
**Dernière mise à jour** : 2026-07-03

---

## Phase 1 — Infrastructure & Base de données ✅

- [x] Docker Compose — 8 services : db, db-backup, backend, ml-service, mlflow, copilot-runtime, frontend, nginx
- [x] Réseaux Docker isolés (`pfa_public`, `pfa_internal_db`, `pfa_internal_ml` + bridges hôte `pfa_db_host`, `pfa_mlflow_host`)
- [x] SQL Server containerisé avec `init.sql`, `init_dw.sql`, `copilot_readonly.sql` (idempotents, re-appliqués à chaque boot)
- [x] Base OLTP `PFA_DB` (normalisée) + Base DW `PFA_DW` (schéma en étoile : FaitNotes + DimEtudiant, DimModule, DimTemps)
- [x] Logins SQL moindre-privilège : `pfa_app` (backend), `pfa_app_readonly` (Copilot DW), `sa` réservé à l'admin
- [x] Healthchecks : `/ready` (DB + marqueur de schéma) gate le démarrage de nginx ; `/health` = liveness seul
- [x] Sidecar `db-backup` — sauvegardes complètes périodiques PFA_DB + PFA_DW avec rétention
- [x] MLflow tracking server (UI démo sur 127.0.0.1:5001) — lineage des entraînements
- [x] TLS optionnel dans nginx (`ENABLE_TLS`) + rate-limit 20 r/s + headers de sécurité
- [x] Variables d'environnement via `.env` (validées au démarrage, placeholders refusés)

---

## Phase 2 — Backend ASP.NET Core 8 ✅

### Authentification & Sécurité
- [x] JWT Bearer (24h) + refresh tokens en DB (rotation single-use, transactionnelle)
- [x] Hash BCrypt, rôles Admin / Responsable / Enseignant, `[Authorize]` sur tous les contrôleurs
- [x] Premier admin seedé par env vars (`ADMIN_SEED_*`) — aucun credential en dur
- [x] Journal d'audit (`AuditService` + `AuditEntries`)

### Contrôleurs (19)
- [x] CRUD : Etudiants, Filieres, Modules, Notes, Absences, Alertes, Utilisateurs
- [x] `DashboardController` — KPIs + deltas + graphiques + feed d'activité
- [x] `PredictionsController` — proxy ML, batch, retrain, metrics, SHAP explain, forecast
- [x] `RapportsController` — 6 modèles, PDF/XLSX/CSV
- [x] `AdminController` — ETL `sync-dw` (app-lock exclusif, 409 si concurrent)
- [x] `EnseignantController` — espace enseignant scopé module + filière + niveau
- [x] Interventions : `InterventionCasesController`, `CaseOutreachController`, `OutreachDraftController`
- [x] Copilot : `CopilotSessionController`, `CopilotToolController`, `CopilotDraftController`, `EmailTemplatesController`

### Logique métier
- [x] Alertes automatiques (note < 10, absences > 20h, risque ML élevé)
- [x] ETL OLTP → DW (MERGE idempotents, source de vérité `database/etl.sql`)
- [x] Workflow d'intervention v2 — flux d'événements unique, escalade dérivée (`CaseWorkflow`, `CaseEscalationWorker`)
- [x] Outreach étudiant : brouillon IA → revue → envoi → entretien → résolution
- [x] Schéma évolutif via `RuntimeMigrations.cs` (SQL idempotent au démarrage — pas d'EF migrations)

---

## Phase 3 — Service ML (FastAPI) ✅

- [x] 4 routeurs : `/predict` (+ batch, retrain), `/cluster`, `/forecast`, `/metrics`
- [x] Auto-entraînement au démarrage : DW réel → fallback UCI → fallback synthétique (clairement étiqueté)
- [x] Provenance du modèle exposée : `data_source`, `split_strategy`, `trained_at` — le frontend avertit si non-`dw`
- [x] Labels risque = échec de la période *suivante* ; split par période (ou GroupShuffleSplit par étudiant)
- [x] Eval sets persistés (parquet) pour métriques honnêtes ; logging MLflow best-effort
- [x] Modèles chargés une fois au démarrage (lifespan), sécurisé par `X-Internal-Token`

---

## Phase 4 — Frontend React 18 ✅

- [x] Auth : JWT en mémoire (pas de localStorage), intercepteur 401 → refresh single-flight, routes protégées par rôle
- [x] AppShell (Sidebar + Topbar + CommandPalette) + design system (Pill, RiskBar, KpiCard, …)
- [x] 16 pages : Dashboard, Étudiants, Fiche étudiant, Alertes (hub triage + liste), Cases + détail, Prédictions, Rapports, Admin, Enseignant, Responsable, Settings, Login
- [x] Charts ApexCharts : barres, histogramme, aire, radial, scatter
- [x] Exports PDF (jsPDF) / XLSX / CSV, téléchargement auth-aware
- [x] Alertes en polling 30s (TanStack Query), résolution en masse
- [x] Tableau de suivi enseignant (À voir / En suivi / Traité)
- [x] Accessibilité : focus trap, ESC, ARIA sur les drawers et boards

---

## Phase 5 — ENIAD Copilot ✅

Assistant IA basé sur **copilot-runtime** (Node 20 / Express + CopilotKit Runtime v2) et NVIDIA NIM (LLaMA 3.3 70B). Sidecar optionnel : jamais un gate de démarrage, la plateforme BI reste disponible s'il tombe.

- [x] Runtime CopilotKit authentifié en streaming à `/api/copilotkit`
- [x] Session gated par cookie HTTP-only 15 min (`pfa_copilot_session`, SameSite=Strict) minté après login
- [x] 5 outils : `get_student`, `list_at_risk`, `query_dw`, `draft_alert`, `explain_risk`
- [x] `query_dw` : mots-clés → requêtes paramétrées fixes sur login DW read-only — jamais de NL→SQL brut
- [x] Double gate sur les callbacks d'outils : JWT utilisateur + `AGENT_INTERNAL_TOKEN`
- [x] Panneau frontend CopilotKit monté après login
- [x] ⚠️ Décision assumée : les données étudiants exposées aux outils transitent vers NVIDIA (LLM externe)

---

## Phase 6 — Intégration & Tests ✅ (couverture E2E partielle)

- [x] 226 tests automatisés : 134 xUnit (backend) · 56 vitest (frontend) · 26 pytest (ML) · 10 vitest (copilot-runtime)
- [x] CI GitHub Actions sur chaque PR/push : build + tests des 3 stacks
- [x] E2E Playwright contre la stack Docker complète (workflow nightly + manuel) : login → dashboard → prédictions → alertes, boucle outreach complète
- [ ] Couverture E2E étendue (parcours enseignant, exports) — connu, non bloquant

---

## Phase 7 — Finalisation & Soutenance 🔄

- [x] README complet (architecture, quick start, opérations)
- [x] Schéma d'architecture — `docs/architecture.md`
- [x] Données de démo réalistes (DataSeeder : 5 filières, 188 modules, étudiants/notes/absences Bogus en Development ou `SEED_SAMPLE_DATA=true`)
- [ ] Slides de présentation
- [ ] Répétition de la démo sur volume vierge (`docker compose down -v && up --build`)

---

## Résumé de l'état actuel

| Composant | Avancement | Notes |
|-----------|-----------|-------|
| Infrastructure Docker | ✅ 100% | 8 services, 5 réseaux, backups, MLflow, TLS optionnel |
| Base de données OLTP + DW | ✅ 100% | ETL idempotent, moindre privilège |
| Backend API | ✅ 100% | 19 contrôleurs, interventions v2, audit |
| Service ML | ✅ 100% | provenance honnête, fallbacks étiquetés |
| Frontend | ✅ 100% | 16 pages, exports, suivi enseignant |
| Copilot (runtime + panel) | ✅ 100% | 5 outils, session cookie, double gate |
| Tests & CI | ✅ 95% | 226 tests verts ; E2E = smoke nightly |
| Soutenance | 🔄 | slides + répétition démo restants |
