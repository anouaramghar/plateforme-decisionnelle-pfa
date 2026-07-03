# Architecture — Plateforme Décisionnelle ENIAD 2025/2026

Huit services orchestrés par Docker Compose, segmentés en cinq réseaux. Les plans
DB et ML sont `internal: true` : injoignables depuis Internet par construction.
Le Copilot est un sidecar optionnel — jamais un gate de démarrage, la plateforme
BI reste disponible s'il tombe.

```mermaid
flowchart TB
    internet(["Internet / Utilisateur"])
    nim["NVIDIA NIM<br/>LLaMA 3.3 70B (externe)"]
    host[/"Hôte (127.0.0.1)<br/>SSMS :1433 · MLflow UI :5001 · Swagger :5135"/]

    subgraph public["pfa_public"]
        nginx["nginx :80/:443<br/>rate-limit · TLS optionnel"]
        frontend["frontend :8080<br/>React 18 + Vite (nginx-unprivileged)"]
        backend["backend :8080<br/>ASP.NET Core 8 · JWT · audit"]
        copilot["copilot-runtime :4000<br/>Node/Express + CopilotKit"]
    end

    subgraph internal_ml["pfa_internal_ml (internal)"]
        ml["ml-service :8000<br/>FastAPI · XGBoost · SHAP"]
        mlflow["mlflow :5000<br/>lineage des modèles"]
    end

    subgraph internal_db["pfa_internal_db (internal)"]
        db[("SQL Server 2022<br/>PFA_DB (OLTP) + PFA_DW (étoile)")]
        backup["db-backup<br/>sauvegardes + rétention"]
    end

    internet --> nginx
    nginx -->|"/*"| frontend
    nginx -->|"/api/*"| backend
    nginx -->|"/api/copilotkit"| copilot
    copilot -->|"outils : JWT + token interne"| backend
    copilot -->|"API externe"| nim
    backend -->|"X-Internal-Token"| ml
    backend -->|"login pfa_app"| db
    ml -->|"lecture seule pfa_app_readonly (DW)"| db
    ml -.->|"best-effort"| mlflow
    backup --> db
    host -.->|"bridges non-internes<br/>pfa_db_host · pfa_mlflow_host"| db
    host -.-> mlflow
```

## Points clés à défendre

- **Segmentation réseau** : `pfa_internal_db` et `pfa_internal_ml` sont `internal: true`.
  Les bridges `pfa_db_host` / `pfa_mlflow_host` existent uniquement pour publier
  les ports 127.0.0.1 vers l'hôte (un conteneur attaché seulement à des réseaux
  internes ne peut pas publier de port hôte).
- **Moindre privilège SQL** : `sa` (admin uniquement) · `pfa_app` (backend,
  datareader + datawriter) · `pfa_app_readonly` (ML/Copilot, lecture seule sur PFA_DW).
- **Démarrage gaté** : nginx dépend de `/ready` du backend (DB joignable **et**
  marqueur de schéma présent) — le trafic n'est admis qu'après provisionnement.
- **Dégradation gracieuse** : ML indisponible → prédiction enregistrée avec statut
  d'échec, la BI reste up. Copilot down → la plateforme fonctionne sans lui.
- **Flux Copilot** : cookie de session HTTP-only 15 min → runtime vérifie le JWT →
  callbacks d'outils doublement gatés (JWT utilisateur + `AGENT_INTERNAL_TOKEN`).
  `query_dw` = mots-clés vers requêtes paramétrées fixes, jamais de NL→SQL brut.
  ⚠️ Les données étudiants exposées aux outils transitent vers NVIDIA (choix assumé).
- **ETL** : `POST /api/admin/sync-dw` — MERGE idempotents OLTP → DW sous app-lock
  exclusif (409 si exécution concurrente).
