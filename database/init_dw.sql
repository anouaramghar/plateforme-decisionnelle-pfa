-- ============================================================
-- PFA_DW — Entrepôt de données (schéma en étoile)
-- Plateforme Décisionnelle ENIAD 2025/2026
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PFA_DW')
    CREATE DATABASE PFA_DW;
GO

USE PFA_DW;
GO

-- ─── Dimensions ──────────────────────────────────────────────

CREATE TABLE DimEtudiant (
    EtudiantKey INT IDENTITY(1,1) PRIMARY KEY,
    Matricule   NVARCHAR(20)  NOT NULL,
    Nom         NVARCHAR(100) NOT NULL,
    Prenom      NVARCHAR(100) NOT NULL,
    Filiere     NVARCHAR(100) NOT NULL,
    Niveau      NVARCHAR(10)  NOT NULL
);
GO

CREATE TABLE DimModule (
    ModuleKey   INT IDENTITY(1,1) PRIMARY KEY,
    Code        NVARCHAR(20)  NOT NULL,
    Nom         NVARCHAR(200) NOT NULL,
    Filiere     NVARCHAR(100) NOT NULL,
    Coefficient DECIMAL(4,2)  NOT NULL
);
GO

CREATE TABLE DimTemps (
    TempsKey INT IDENTITY(1,1) PRIMARY KEY,
    Annee    NVARCHAR(9) NOT NULL,
    Semestre NVARCHAR(5) NOT NULL,
    UNIQUE (Annee, Semestre)
);
GO

-- ─── Table de faits : Notes ───────────────────────────────────
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
