-- ============================================================
-- PFA_DW — Entrepôt de données (schéma en étoile)
-- Plateforme Décisionnelle ENIAD 2025/2026
--
-- Idempotent: safe to run on every container start.
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PFA_DW')
    CREATE DATABASE PFA_DW;
GO

USE PFA_DW;
GO

-- ─── Dimensions ──────────────────────────────────────────────

IF OBJECT_ID('dbo.DimEtudiant', 'U') IS NULL
CREATE TABLE DimEtudiant (
    EtudiantKey INT IDENTITY(1,1) PRIMARY KEY,
    Matricule   NVARCHAR(20)  NOT NULL UNIQUE,
    Nom         NVARCHAR(100) NOT NULL,
    Prenom      NVARCHAR(100) NOT NULL,
    Filiere     NVARCHAR(100) NOT NULL,
    Niveau      NVARCHAR(10)  NOT NULL
);
GO

IF OBJECT_ID('dbo.DimModule', 'U') IS NULL
CREATE TABLE DimModule (
    ModuleKey   INT IDENTITY(1,1) PRIMARY KEY,
    Code        NVARCHAR(20)  NOT NULL,
    Nom         NVARCHAR(200) NOT NULL,
    Filiere     NVARCHAR(100) NOT NULL,
    Coefficient DECIMAL(4,2)  NOT NULL
);
GO

IF OBJECT_ID('dbo.DimTemps', 'U') IS NULL
CREATE TABLE DimTemps (
    TempsKey INT IDENTITY(1,1) PRIMARY KEY,
    Annee    NVARCHAR(9) NOT NULL,
    Semestre NVARCHAR(5) NOT NULL,
    UNIQUE (Annee, Semestre)
);
GO

-- ─── Table de faits : Notes ───────────────────────────────────
IF OBJECT_ID('dbo.FaitNotes', 'U') IS NULL
CREATE TABLE FaitNotes (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    EtudiantKey INT          NOT NULL REFERENCES DimEtudiant(EtudiantKey),
    ModuleKey   INT          NOT NULL REFERENCES DimModule(ModuleKey),
    TempsKey    INT          NOT NULL REFERENCES DimTemps(TempsKey),
    NoteFinale  DECIMAL(5,2),
    NbAbsences  INT          NOT NULL DEFAULT 0,
    ScoreRisque DECIMAL(5,4),
    Cluster     INT
);
GO

-- Unique grain constraint on FaitNotes
-- Idempotent: checks for existing index before creating.
-- Guards against the ETL inserting duplicate rows for the same
-- (student, module, period) grain — which would silently double-count.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.FaitNotes')
      AND name = 'UX_FaitNotes_Grain'
)
BEGIN
    -- Abort if duplicates already exist — they must be repaired first.
    IF EXISTS (
        SELECT 1 FROM dbo.FaitNotes
        GROUP BY EtudiantKey, ModuleKey, TempsKey
        HAVING COUNT(*) > 1
    )
        THROW 51001, 'Duplicate FaitNotes grain must be repaired before migration', 1;

    CREATE UNIQUE INDEX UX_FaitNotes_Grain
        ON dbo.FaitNotes(EtudiantKey, ModuleKey, TempsKey);
END
GO

-- ─── Read access for the application login ───────────────────
USE PFA_DW;
GO

IF EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = 'pfa_app')
   AND NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'pfa_app')
    CREATE USER pfa_app FOR LOGIN pfa_app;
GO

IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'pfa_app')
BEGIN
    ALTER ROLE db_datareader ADD MEMBER pfa_app;
    ALTER ROLE db_datawriter ADD MEMBER pfa_app;
END
GO

-- ─── Read access for the read-only login (Copilot query_dw + ML service) ──
-- pfa_app_readonly login is created by copilot_readonly.sql, which entrypoint.sh
-- runs AFTER init_dw.sql, so on cold first boot the login does not exist yet and
-- this block is a no-op (copilot_readonly.sql grants db_datareader on its own
-- run). On subsequent boots the login exists, so this idempotent block re-asserts
-- the grant — keeping init_dw.sql self-describing for the DW's full reader set.
USE PFA_DW;
GO

IF EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = 'pfa_app_readonly')
   AND NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'pfa_app_readonly')
    CREATE USER pfa_app_readonly FOR LOGIN pfa_app_readonly;
GO

IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'pfa_app_readonly')
   AND IS_ROLEMEMBER('db_datareader', 'pfa_app_readonly') = 0
    ALTER ROLE db_datareader ADD MEMBER pfa_app_readonly;
GO

-- NOTE: The pfa_app_readonly login itself is provisioned by
-- database/copilot_readonly.sql, run on every boot by entrypoint.sh so it rolls
-- forward onto pre-existing volumes too. Both the Copilot query_dw tool and the
-- ML service connect as pfa_app_readonly (read-only on PFA_DW, no PFA_DB access).
