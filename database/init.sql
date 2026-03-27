-- ============================================================
-- PFA_DB — Base de données OLTP
-- Plateforme Décisionnelle ENIAD 2025/2026
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PFA_DB')
    CREATE DATABASE PFA_DB;
GO

USE PFA_DB;
GO

-- ─── Utilisateurs (authentification) ────────────────────────
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

-- ─── Etudiants ───────────────────────────────────────────────
CREATE TABLE Etudiants (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    Matricule NVARCHAR(20)  NOT NULL UNIQUE,
    Nom       NVARCHAR(100) NOT NULL,
    Prenom    NVARCHAR(100) NOT NULL,
    Email     NVARCHAR(200),
    Filiere   NVARCHAR(100) NOT NULL,
    Niveau    NVARCHAR(10)  NOT NULL,  -- L1 L2 L3 M1 M2
    Annee     NVARCHAR(9)   NOT NULL,  -- ex: 2025/2026
    CreeLe    DATETIME2     NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ─── Modules ─────────────────────────────────────────────────
CREATE TABLE Modules (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Code        NVARCHAR(20)  NOT NULL UNIQUE,
    Nom         NVARCHAR(200) NOT NULL,
    Filiere     NVARCHAR(100) NOT NULL,
    Niveau      NVARCHAR(10)  NOT NULL,
    Coefficient DECIMAL(4,2)  NOT NULL DEFAULT 1,
    Semestre    NVARCHAR(5)   NOT NULL  -- S1 S2
);
GO

-- ─── Notes ───────────────────────────────────────────────────
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
    UNIQUE (EtudiantId, ModuleId, Annee, Semestre)
);
GO

-- ─── Absences ────────────────────────────────────────────────
CREATE TABLE Absences (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    EtudiantId   INT       NOT NULL REFERENCES Etudiants(Id),
    ModuleId     INT       NOT NULL REFERENCES Modules(Id),
    NombreHeures INT       NOT NULL DEFAULT 1,
    Justifiee    BIT       NOT NULL DEFAULT 0,
    DateAbsence  DATE      NOT NULL,
    CreeLe       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ─── Alertes ─────────────────────────────────────────────────
CREATE TABLE Alertes (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    EtudiantId INT          NOT NULL REFERENCES Etudiants(Id),
    Type       NVARCHAR(50) NOT NULL
               CHECK (Type IN ('RisqueEchec', 'AbsenceExcessive', 'NoteFaible', 'Abandon')),
    Niveau     NVARCHAR(20) NOT NULL
               CHECK (Niveau IN ('Faible', 'Moyen', 'Eleve', 'Critique')),
    Message    NVARCHAR(500),
    Resolue    BIT          NOT NULL DEFAULT 0,
    CreeLe     DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    ResolueeLe DATETIME2
);
GO

-- ─── Predictions (résultats ML) ──────────────────────────────
CREATE TABLE Predictions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    EtudiantId  INT          NOT NULL REFERENCES Etudiants(Id),
    TypeModele  NVARCHAR(50) NOT NULL
                CHECK (TypeModele IN ('RisqueEchec', 'Regression', 'Clustering')),
    ScoreRisque DECIMAL(5,4),   -- probabilité 0.0–1.0
    Cluster     INT,            -- segment de clustering
    NotePredite DECIMAL(5,2),   -- note prédite (régression)
    Confiance   DECIMAL(5,4),
    Annee       NVARCHAR(9)  NOT NULL,
    CreeLe      DATETIME2    NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ─── Données initiales ───────────────────────────────────────
INSERT INTO Utilisateurs (Nom, Prenom, Email, MotDePasseHash, Role)
VALUES ('Admin', 'System', 'admin@eniad.dz',
        '$2a$12$placeholder_hash_to_be_replaced', 'Admin');
GO
