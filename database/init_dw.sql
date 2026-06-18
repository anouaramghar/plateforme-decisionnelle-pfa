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

-- NOTE: Copilot read-only DW access (pfa_app_readonly user + db_datareader) is
-- provisioned by database/copilot_readonly.sql, run on every boot by
-- entrypoint.sh so it rolls forward onto pre-existing volumes too.
