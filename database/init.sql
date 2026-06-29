-- ============================================================
-- PFA_DB — Base de données OLTP
-- Plateforme Décisionnelle ENIAD 2025/2026
--
-- Idempotent: safe to run on every container start.
-- Expects sqlcmd variable :APP_PASSWORD (passed by entrypoint.sh).
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PFA_DB')
    CREATE DATABASE PFA_DB;
GO

USE PFA_DB;
GO

-- ─── Utilisateurs (authentification) ────────────────────────
IF OBJECT_ID('dbo.Utilisateurs', 'U') IS NULL
CREATE TABLE Utilisateurs (
    Id             INT IDENTITY(1,1) PRIMARY KEY,
    Nom            NVARCHAR(100)  NOT NULL,
    Prenom         NVARCHAR(100)  NOT NULL,
    Email          NVARCHAR(200)  NOT NULL UNIQUE,
    MotDePasseHash NVARCHAR(500)  NOT NULL,
    Role           NVARCHAR(50)   NOT NULL
                   CHECK (Role IN ('Admin', 'Enseignant', 'Responsable')),
    EstActif       BIT            NOT NULL DEFAULT 1,
    CreeLe         DATETIME2      NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ─── Filieres ────────────────────────────────────────────────
IF OBJECT_ID('dbo.Filieres', 'U') IS NULL
CREATE TABLE Filieres (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    Code          NVARCHAR(20)  NOT NULL UNIQUE,
    Intitule      NVARCHAR(200) NOT NULL,
    ResponsableId INT           REFERENCES Utilisateurs(Id)
);
GO

-- ─── Etudiants ───────────────────────────────────────────────
IF OBJECT_ID('dbo.Etudiants', 'U') IS NULL
CREATE TABLE Etudiants (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    Matricule NVARCHAR(20)  NOT NULL UNIQUE,
    Nom       NVARCHAR(100) NOT NULL,
    Prenom    NVARCHAR(100) NOT NULL,
    Email     NVARCHAR(200),
    FiliereId INT           NOT NULL REFERENCES Filieres(Id),
    Niveau    NVARCHAR(10)  NOT NULL,  -- CP1 CP2 CI1 CI2 CI3
    Annee     NVARCHAR(9)   NOT NULL,  -- ex: 2025/2026
    CreeLe    DATETIME2     NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ─── Modules ─────────────────────────────────────────────────
IF OBJECT_ID('dbo.Modules', 'U') IS NULL
CREATE TABLE Modules (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Code        NVARCHAR(20)  NOT NULL UNIQUE,
    Nom         NVARCHAR(200) NOT NULL,
    FiliereId   INT           NOT NULL REFERENCES Filieres(Id),
    Niveau      NVARCHAR(10)  NOT NULL,
    Coefficient DECIMAL(4,2)  NOT NULL DEFAULT 1,
    Semestre    NVARCHAR(5)   NOT NULL  -- S1 S2
);
GO

-- ─── Notes ───────────────────────────────────────────────────
IF OBJECT_ID('dbo.Notes', 'U') IS NULL
CREATE TABLE Notes (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    EtudiantId INT          NOT NULL REFERENCES Etudiants(Id),
    ModuleId   INT          NOT NULL REFERENCES Modules(Id),
    NoteExamen DECIMAL(5,2),
    NoteTD     DECIMAL(5,2),
    NoteTP     DECIMAL(5,2),
    NoteFinal  DECIMAL(5,2),
    Annee      NVARCHAR(9)  NOT NULL,
    Semestre   NVARCHAR(5)  NOT NULL,
    CreeLe     DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    RowVersion ROWVERSION   NOT NULL,
    UNIQUE (EtudiantId, ModuleId, Annee, Semestre)
);
GO

-- ─── Absences ────────────────────────────────────────────────
IF OBJECT_ID('dbo.Absences', 'U') IS NULL
CREATE TABLE Absences (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    EtudiantId   INT       NOT NULL REFERENCES Etudiants(Id),
    ModuleId     INT       NOT NULL REFERENCES Modules(Id),
    NombreHeures INT       NOT NULL DEFAULT 1,
    Justifiee    BIT       NOT NULL DEFAULT 0,
    DateAbsence  DATE      NOT NULL,
    CreeLe       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    RowVersion   ROWVERSION NOT NULL
);
GO

-- ─── Alertes ─────────────────────────────────────────────────
-- ModuleId is nullable: AbsenceExcessive aggregates all modules, while
-- NoteFaible is per-module. Storing it structured (instead of stuffing
-- "ModuleId=N" into Message) lets AlerteService dedupe reliably.
IF OBJECT_ID('dbo.Alertes', 'U') IS NULL
CREATE TABLE Alertes (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    EtudiantId    INT          NOT NULL REFERENCES Etudiants(Id),
    ModuleId      INT          NULL     REFERENCES Modules(Id),
    Type          NVARCHAR(50) NOT NULL
                  CHECK (Type IN ('RisqueEchec', 'AbsenceExcessive', 'NoteFaible', 'Abandon')),
    Niveau        NVARCHAR(20) NOT NULL
                  CHECK (Niveau IN ('Faible', 'Moyen', 'Eleve', 'Critique')),
    Message       NVARCHAR(500),
    Resolue       BIT          NOT NULL DEFAULT 0,
    CreeLe        DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    ResolueeLe    DATETIME2,
    ResolueeParId INT          NULL     REFERENCES Utilisateurs(Id)
);
GO

-- Idempotent migration for existing databases (adds new columns if missing).
IF OBJECT_ID('dbo.Alertes', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.Alertes') AND name = 'ModuleId')
    ALTER TABLE Alertes ADD ModuleId INT NULL REFERENCES Modules(Id);
GO

IF OBJECT_ID('dbo.Alertes', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.Alertes') AND name = 'ResolueeParId')
    ALTER TABLE Alertes ADD ResolueeParId INT NULL REFERENCES Utilisateurs(Id);
GO

-- Idempotent migration: link Enseignant to their Module
IF OBJECT_ID('dbo.Utilisateurs', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.Utilisateurs') AND name = 'ModuleId')
    ALTER TABLE Utilisateurs ADD ModuleId INT NULL REFERENCES Modules(Id);
GO

-- ─── Refresh Tokens ──────────────────────────────────────────
IF OBJECT_ID('dbo.RefreshTokens', 'U') IS NULL
CREATE TABLE RefreshTokens (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    UtilisateurId INT         NOT NULL REFERENCES Utilisateurs(Id),
    Token        NVARCHAR(500) NOT NULL UNIQUE,
    ExpiresAt    DATETIME2    NOT NULL,
    RevokedAt    DATETIME2    NULL,        -- NULL = still valid
    CreeLe       DATETIME2    NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ─── Predictions (résultats ML) ──────────────────────────────
IF OBJECT_ID('dbo.Predictions', 'U') IS NULL
CREATE TABLE Predictions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    EtudiantId  INT          NOT NULL REFERENCES Etudiants(Id),
    TypeModele  NVARCHAR(50) NOT NULL
                CHECK (TypeModele IN ('RisqueEchec', 'Regression', 'Clustering')),
    ScoreRisque DECIMAL(5,4),
    Niveau      NVARCHAR(20) NULL,    -- mirrors Alertes.Niveau labels
    Cluster     INT,
    NotePredite DECIMAL(5,2),
    Confiance   DECIMAL(5,4),
    Annee       NVARCHAR(9)  NOT NULL,
    Status      NVARCHAR(30) NOT NULL DEFAULT 'Ok',  -- Ok | MlUnavailable | InsufficientData
    CreeLe      DATETIME2    NOT NULL DEFAULT GETUTCDATE()
);
GO

-- Idempotent migration for existing DBs
IF OBJECT_ID('dbo.Predictions', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.Predictions') AND name = 'Niveau')
    ALTER TABLE Predictions ADD Niveau NVARCHAR(20) NULL;
GO

IF OBJECT_ID('dbo.Predictions', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID('dbo.Predictions') AND name = 'Status')
    ALTER TABLE Predictions ADD Status NVARCHAR(30) NOT NULL DEFAULT 'Ok';
GO

-- ─── Performance indexes ─────────────────────────────────────
-- Idempotent: each index is wrapped in an existence check.

-- Speeds up ETL DimTemps sync and FaitNotes join on (Annee, Semestre)
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Notes_Annee_Semestre'
                 AND object_id = OBJECT_ID('dbo.Notes'))
    CREATE INDEX IX_Notes_Annee_Semestre ON Notes(Annee, Semestre);
GO

-- Speeds up the correlated absence-hours subquery in FaitNotes ETL
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Absences_Etudiant_Module_Date'
                 AND object_id = OBJECT_ID('dbo.Absences'))
    CREATE INDEX IX_Absences_Etudiant_Module_Date
        ON Absences(EtudiantId, ModuleId, DateAbsence);
GO

-- Speeds up LatestPredictions CTE (partition + order) in FaitNotes ETL
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Predictions_Etudiant_Annee_Type'
                 AND object_id = OBJECT_ID('dbo.Predictions'))
    CREATE INDEX IX_Predictions_Etudiant_Annee_Type
        ON Predictions(EtudiantId, Annee, TypeModele);
GO

-- Speeds up token lookup by user during auth / logout / revocation
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_RefreshTokens_UtilisateurId'
                 AND object_id = OBJECT_ID('dbo.RefreshTokens'))
    CREATE INDEX IX_RefreshTokens_UtilisateurId
        ON RefreshTokens(UtilisateurId);
GO

-- ─── Least-privilege application login ───────────────────────
-- The backend connects as pfa_app (db_datareader + db_datawriter),
-- NOT as sa. Password is passed via sqlcmd variable :APP_PASSWORD.
USE master;
GO

IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = 'pfa_app')
BEGIN
    -- CHECK_POLICY = ON enforces Windows complexity / lockout rules on the
    -- app credential; CHECK_EXPIRATION = OFF prevents an automatic password
    -- expiry from silently bricking the backend when the app account is
    -- non-interactive (rotation must be done deliberately, via redeploy).
    -- sqlcmd substitutes $(APP_PASSWORD) verbatim into the batch text; the
    -- REPLACE only doubles embedded single quotes so the @pw literal parses.
    -- QUOTENAME then escapes the value correctly for the dynamic statement
    -- (parameterized instead of nested-quote string concatenation).
    DECLARE @pw NVARCHAR(128) = REPLACE('$(APP_PASSWORD)', '''', '''''');
    DECLARE @sql NVARCHAR(MAX) =
        N'CREATE LOGIN pfa_app WITH PASSWORD = ' + QUOTENAME(@pw, '''') +
        N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
    EXEC sp_executesql @sql;
END
GO

-- NOTE: the Copilot read-only login (pfa_app_readonly) is provisioned by
-- database/copilot_readonly.sql, which entrypoint.sh runs on EVERY boot — not
-- here. init.sql only runs on cold first boot, so putting it here would never
-- fire on volumes seeded with an older schema. See entrypoint.sh.

USE PFA_DB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'pfa_app')
    CREATE USER pfa_app FOR LOGIN pfa_app;
GO

ALTER ROLE db_datareader ADD MEMBER pfa_app;
ALTER ROLE db_datawriter ADD MEMBER pfa_app;
-- db_ddladmin is REQUIRED for RuntimeMigrations.cs, the canonical schema-
-- evolution path (EF migrations are NOT used — there is no Migrations/ folder).
-- RuntimeMigrations runs idempotent CREATE TABLE / ALTER TABLE / CREATE INDEX
-- blocks on every backend startup to roll forward additive schema changes
-- (Rapports, AuditEntries, Predictions.Niveau backfill, new indexes) onto
-- existing volumes without needing a separate sa connection.
--
-- To REMOVE db_ddladmin (tighter least-privilege): retire RuntimeMigrations.cs
-- in favor of explicit, reviewable migration scripts run by an operator with sa
-- during deploys, then grant pfa_app only db_datareader + db_datawriter. New
-- tables/columns would then need specific per-object GRANTs (or the app would
-- lose access to objects it can no longer create). Do NOT drop ddladmin while
-- RuntimeMigrations.cs is still in the startup path — the backend will fail to
-- apply schema changes and report /ready as unhealthy on stale volumes.
ALTER ROLE db_ddladmin ADD MEMBER pfa_app;
GO

-- NOTE: No admin user is seeded in SQL. The first Admin account is created at
-- backend startup by DataSeeder (backend/PlateformePFA.API/Services/DataSeeder),
-- driven by the ADMIN_SEED_EMAIL / ADMIN_SEED_PASSWORD env vars (see .env.example).
-- There is NO /api/auth/register endpoint — DataSeeder is the only first-admin
-- path. After first boot, change the password via the API.

-- ─── Schema readiness marker ──────────────────────────────────
-- Written at the very end of init.sql so it is set only after
-- every table has been created. The DB healthcheck queries this
-- table to distinguish "SQL Server reachable" from "schema ready".
IF OBJECT_ID('dbo.SchemaState', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaState (
        Component NVARCHAR(50) NOT NULL PRIMARY KEY,
        Version   INT          NOT NULL,
        UpdatedAt DATETIME     NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

-- Mark core schema as complete (insert or update)
IF EXISTS (SELECT 1 FROM dbo.SchemaState WHERE Component = 'core')
    UPDATE dbo.SchemaState SET Version = 1, UpdatedAt = GETUTCDATE() WHERE Component = 'core';
ELSE
    INSERT INTO dbo.SchemaState (Component, Version) VALUES ('core', 1);
GO
