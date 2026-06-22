using Microsoft.EntityFrameworkCore;

namespace PlateformePFA.API.Data
{
    /// <summary>
    /// Idempotent schema migrations executed on every backend startup.
    ///
    /// Why this exists: <c>database/entrypoint.sh</c> only runs <c>init.sql</c>
    /// when <c>PFA_DB</c> doesn't exist yet, so additive ALTER TABLE / CREATE
    /// TABLE statements added later never fire on volumes seeded with an
    /// older schema. Putting them here guarantees they run regardless of
    /// volume age. Keep every block guarded with an existence check.
    /// </summary>
    public static class RuntimeMigrations
    {
        public static void Apply(AppDbContext context)
        {
            // 0. Utilisateurs.ModuleId: teacher → module scoping column added with the
            //    read-scope work. The EF model maps it and AuthService selects it on
            //    every login, but init.sql never created it — so existing volumes (and
            //    fresh deploys) throw "Invalid column name 'ModuleId'" at login until
            //    this runs. Nullable FK to Modules; null = unrestricted (Admin/Responsable).
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.Utilisateurs', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.columns
                                   WHERE object_id = OBJECT_ID('dbo.Utilisateurs') AND name = 'ModuleId')
                    ALTER TABLE Utilisateurs ADD ModuleId INT NULL REFERENCES Modules(Id);
            ");

            // 1. Predictions: backfill columns added after the first init.sql release.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.Predictions', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.columns
                                   WHERE object_id = OBJECT_ID('dbo.Predictions') AND name = 'Niveau')
                    ALTER TABLE Predictions ADD Niveau NVARCHAR(20) NULL;
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.Predictions', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.columns
                                   WHERE object_id = OBJECT_ID('dbo.Predictions') AND name = 'Status')
                    ALTER TABLE Predictions ADD Status NVARCHAR(30) NOT NULL
                        CONSTRAINT DF_Predictions_Status DEFAULT 'Ok';
            ");

            // 2. Rapports table — created here rather than in init.sql so it
            //    rolls forward on existing volumes without manual ops.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.Rapports', 'U') IS NULL
                CREATE TABLE Rapports (
                    Id          INT IDENTITY(1,1) PRIMARY KEY,
                    TemplateId  NVARCHAR(40)  NOT NULL,
                    Titre       NVARCHAR(200) NOT NULL,
                    Format      NVARCHAR(10)  NOT NULL,
                    FiliereCode NVARCHAR(20)  NOT NULL DEFAULT 'TOUS',
                    Periode     NVARCHAR(40)  NOT NULL DEFAULT '',
                    AuteurId    INT NULL REFERENCES Utilisateurs(Id),
                    AuteurNom   NVARCHAR(200) NOT NULL DEFAULT '',
                    Taille      INT           NOT NULL DEFAULT 0,
                    CreeLe      DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                    Contenu     VARBINARY(MAX) NOT NULL
                );
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.Rapports', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                                   WHERE name = 'IX_Rapports_CreeLe'
                                     AND object_id = OBJECT_ID('dbo.Rapports'))
                    CREATE INDEX IX_Rapports_CreeLe ON Rapports(CreeLe DESC);
            ");

            // 3. AuditEntries — append-only event log feeding the dashboard
            //    activity feed and the platform's audit trail.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AuditEntries', 'U') IS NULL
                CREATE TABLE AuditEntries (
                    Id              INT IDENTITY(1,1) PRIMARY KEY,
                    UtilisateurId   INT NULL REFERENCES Utilisateurs(Id),
                    UtilisateurNom  NVARCHAR(200) NOT NULL,
                    Action          NVARCHAR(40)  NOT NULL,
                    EntityType      NVARCHAR(40)  NOT NULL DEFAULT '',
                    EntityId        INT NULL,
                    Message         NVARCHAR(500) NULL,
                    CreeLe          DATETIME2     NOT NULL DEFAULT GETUTCDATE()
                );
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AuditEntries', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                                   WHERE name = 'IX_AuditEntries_CreeLe'
                                     AND object_id = OBJECT_ID('dbo.AuditEntries'))
                    CREATE INDEX IX_AuditEntries_CreeLe ON AuditEntries(CreeLe DESC);
            ");

            // 4. Copilot — AgentSessions: one conversation between a user and ENIAD Copilot.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AgentSessions', 'U') IS NULL
                CREATE TABLE AgentSessions (
                    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                    UserId          INT           NOT NULL REFERENCES Utilisateurs(Id),
                    StartedAt       DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                    LastActivityAt  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                    IsArchived      BIT           NOT NULL DEFAULT 0
                );
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AgentSessions', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                                   WHERE name = 'IX_AgentSessions_UserId_LastActivityAt'
                                     AND object_id = OBJECT_ID('dbo.AgentSessions'))
                    CREATE INDEX IX_AgentSessions_UserId_LastActivityAt
                        ON AgentSessions(UserId, LastActivityAt DESC);
            ");

            // 5. Copilot — AgentSessionMessages: turns in a session.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AgentSessionMessages', 'U') IS NULL
                CREATE TABLE AgentSessionMessages (
                    Id           BIGINT IDENTITY(1,1) PRIMARY KEY,
                    SessionId    UNIQUEIDENTIFIER NOT NULL REFERENCES AgentSessions(Id),
                    TurnIndex    INT           NOT NULL,
                    Role         NVARCHAR(20)  NOT NULL,
                    ContentJson  NVARCHAR(MAX) NOT NULL,
                    TokensIn     INT NULL,
                    TokensOut    INT NULL,
                    CreatedAt    DATETIME2     NOT NULL DEFAULT GETUTCDATE()
                );
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AgentSessionMessages', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                                   WHERE name = 'IX_AgentSessionMessages_SessionId_TurnIndex'
                                     AND object_id = OBJECT_ID('dbo.AgentSessionMessages'))
                    CREATE UNIQUE INDEX IX_AgentSessionMessages_SessionId_TurnIndex
                        ON AgentSessionMessages(SessionId, TurnIndex);
            ");

            // 6. Copilot — AgentAuditLogs: PII-redacted record of every LLM/tool call.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AgentAuditLogs', 'U') IS NULL
                CREATE TABLE AgentAuditLogs (
                    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
                    CreatedAt   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                    UserId      INT           NOT NULL REFERENCES Utilisateurs(Id),
                    SessionId   UNIQUEIDENTIFIER NULL REFERENCES AgentSessions(Id),
                    Kind        NVARCHAR(20)  NOT NULL,
                    Model       NVARCHAR(80)  NULL,
                    ToolName    NVARCHAR(60)  NULL,
                    TokensIn    INT NULL,
                    TokensOut   INT NULL,
                    LatencyMs   INT NULL,
                    Payload     NVARCHAR(MAX) NULL,
                    Outcome     NVARCHAR(20)  NOT NULL DEFAULT 'success'
                );
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AgentAuditLogs', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                                   WHERE name = 'IX_AgentAuditLogs_UserId_CreatedAt'
                                     AND object_id = OBJECT_ID('dbo.AgentAuditLogs'))
                    CREATE INDEX IX_AgentAuditLogs_UserId_CreatedAt
                        ON AgentAuditLogs(UserId, CreatedAt DESC);
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AgentAuditLogs', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                                   WHERE name = 'IX_AgentAuditLogs_Kind'
                                     AND object_id = OBJECT_ID('dbo.AgentAuditLogs'))
                    CREATE INDEX IX_AgentAuditLogs_Kind ON AgentAuditLogs(Kind);
            ");

            // 7. Copilot — AlertDrafts: alerts prepared by draft_alert; promoted on user confirm.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AlertDrafts', 'U') IS NULL
                CREATE TABLE AlertDrafts (
                    Id         INT IDENTITY(1,1) PRIMARY KEY,
                    StudentId  INT           NOT NULL REFERENCES Etudiants(Id),
                    Severity   NVARCHAR(10)  NOT NULL DEFAULT 'medium',
                    MessageFr  NVARCHAR(1000) NOT NULL,
                    CreatedBy  INT           NOT NULL REFERENCES Utilisateurs(Id),
                    CreatedAt  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                    ExpiresAt  DATETIME2     NOT NULL,
                    Status     NVARCHAR(30)  NOT NULL DEFAULT 'pending_user_confirm',
                    SentAt     DATETIME2     NULL
                );
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AlertDrafts', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                                   WHERE name = 'IX_AlertDrafts_CreatedBy_Status'
                                     AND object_id = OBJECT_ID('dbo.AlertDrafts'))
                    CREATE INDEX IX_AlertDrafts_CreatedBy_Status
                        ON AlertDrafts(CreatedBy, Status);
            ");

            // L4: defense-in-depth CHECK constraints on Copilot enums.
            // If the agent (or any future client) bypasses pydantic validation
            // and tries to insert an invalid Severity/Status/Role, SQL refuses.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AlertDrafts', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                                   WHERE name = 'CK_AlertDrafts_Severity'
                                     AND parent_object_id = OBJECT_ID('dbo.AlertDrafts'))
                    ALTER TABLE AlertDrafts
                        ADD CONSTRAINT CK_AlertDrafts_Severity
                        CHECK (Severity IN ('low', 'medium', 'high'));
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AlertDrafts', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                                   WHERE name = 'CK_AlertDrafts_Status'
                                     AND parent_object_id = OBJECT_ID('dbo.AlertDrafts'))
                    ALTER TABLE AlertDrafts
                        ADD CONSTRAINT CK_AlertDrafts_Status
                        CHECK (Status IN ('pending_user_confirm', 'sent', 'expired', 'cancelled'));
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AgentSessionMessages', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                                   WHERE name = 'CK_AgentSessionMessages_Role'
                                     AND parent_object_id = OBJECT_ID('dbo.AgentSessionMessages'))
                    ALTER TABLE AgentSessionMessages
                        ADD CONSTRAINT CK_AgentSessionMessages_Role
                        CHECK (Role IN ('system', 'user', 'assistant', 'tool'));
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AgentAuditLogs', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                                   WHERE name = 'CK_AgentAuditLogs_Kind'
                                     AND parent_object_id = OBJECT_ID('dbo.AgentAuditLogs'))
                    ALTER TABLE AgentAuditLogs
                        ADD CONSTRAINT CK_AgentAuditLogs_Kind
                        CHECK (Kind IN ('llm_call', 'tool_call', 'confirm', 'reject', 'safety_block'));
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.AgentAuditLogs', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                                   WHERE name = 'CK_AgentAuditLogs_Outcome'
                                     AND parent_object_id = OBJECT_ID('dbo.AgentAuditLogs'))
                    ALTER TABLE AgentAuditLogs
                        ADD CONSTRAINT CK_AgentAuditLogs_Outcome
                        CHECK (Outcome IN ('success', 'error', 'denied'));
            ");

            // 8. Etudiants: Add DesinscritLe column
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.Etudiants', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.columns
                                   WHERE object_id = OBJECT_ID('dbo.Etudiants') AND name = 'DesinscritLe')
                    ALTER TABLE Etudiants ADD DesinscritLe DATETIME2 NULL;
            ");

            // 9. Notes & Absences: Add RowVersion column
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.Notes', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.columns
                                   WHERE object_id = OBJECT_ID('dbo.Notes') AND name = 'RowVersion')
                    ALTER TABLE Notes ADD RowVersion ROWVERSION NOT NULL;
            ");

            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.Absences', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.columns
                                   WHERE object_id = OBJECT_ID('dbo.Absences') AND name = 'RowVersion')
                    ALTER TABLE Absences ADD RowVersion ROWVERSION NOT NULL;
            ");

            // 10. SchemaState — readiness marker table.
            //     Added here so volumes created before this table existed get it
            //     on the next backend startup without a manual SQL run.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.SchemaState', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.SchemaState (
                        Component NVARCHAR(50) NOT NULL PRIMARY KEY,
                        Version   INT          NOT NULL,
                        UpdatedAt DATETIME     NOT NULL DEFAULT GETUTCDATE()
                    );
                END
            ");
            context.Database.ExecuteSqlRaw(@"
                IF EXISTS (SELECT 1 FROM dbo.SchemaState WHERE Component = 'core')
                    UPDATE dbo.SchemaState SET Version = 1, UpdatedAt = GETUTCDATE() WHERE Component = 'core';
                ELSE
                    INSERT INTO dbo.SchemaState (Component, Version) VALUES ('core', 1);
            ");

            // 11. Intervention cases (Release 1) — owned, auditable interventions.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.InterventionCases', 'U') IS NULL
                CREATE TABLE InterventionCases (
                    Id                INT IDENTITY(1,1) PRIMARY KEY,
                    EtudiantId        INT           NOT NULL REFERENCES Etudiants(Id),
                    FiliereId         INT           NOT NULL,
                    Motif             NVARCHAR(200) NOT NULL,
                    Priorite          NVARCHAR(20)  NOT NULL DEFAULT 'Medium',
                    Etat              NVARCHAR(20)  NOT NULL DEFAULT 'Open',
                    OwnerId           INT NULL REFERENCES Utilisateurs(Id),
                    DueDate           DATETIME2     NULL,
                    EscaladeLe        DATETIME2     NULL,
                    Outcome           NVARCHAR(40)  NULL,
                    ResolutionSummary NVARCHAR(1000) NULL,
                    FollowUpDate      DATETIME2     NULL,
                    CreeLe            DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                    CreeParId         INT NULL REFERENCES Utilisateurs(Id),
                    ClotureLe         DATETIME2     NULL
                );
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.InterventionCases', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                                   WHERE name = 'IX_InterventionCases_Etat'
                                     AND object_id = OBJECT_ID('dbo.InterventionCases'))
                    CREATE INDEX IX_InterventionCases_Etat ON InterventionCases(Etat, CreeLe DESC);
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.InterventionCases', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                                   WHERE name = 'CK_InterventionCases_Etat'
                                     AND parent_object_id = OBJECT_ID('dbo.InterventionCases'))
                    ALTER TABLE InterventionCases
                        ADD CONSTRAINT CK_InterventionCases_Etat
                        CHECK (Etat IN ('Open','InProgress','WaitingStudent','Monitoring','Escalated','Resolved','Closed'));
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.InterventionCases', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                                   WHERE name = 'CK_InterventionCases_Priorite'
                                     AND parent_object_id = OBJECT_ID('dbo.InterventionCases'))
                    ALTER TABLE InterventionCases
                        ADD CONSTRAINT CK_InterventionCases_Priorite
                        CHECK (Priorite IN ('Critical','High','Medium','Low'));
            ");

            // 12. CaseTimelineEvents — append-only audit trail per case.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.CaseTimelineEvents', 'U') IS NULL
                CREATE TABLE CaseTimelineEvents (
                    Id             INT IDENTITY(1,1) PRIMARY KEY,
                    CaseId         INT           NOT NULL REFERENCES InterventionCases(Id),
                    Action         NVARCHAR(40)  NOT NULL,
                    Description    NVARCHAR(500) NULL,
                    UtilisateurId  INT NULL REFERENCES Utilisateurs(Id),
                    UtilisateurNom NVARCHAR(200) NOT NULL DEFAULT 'Système',
                    CreeLe         DATETIME2     NOT NULL DEFAULT GETUTCDATE()
                );
            ");
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.CaseTimelineEvents', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                                   WHERE name = 'IX_CaseTimelineEvents_CaseId'
                                     AND object_id = OBJECT_ID('dbo.CaseTimelineEvents'))
                    CREATE INDEX IX_CaseTimelineEvents_CaseId ON CaseTimelineEvents(CaseId, CreeLe DESC);
            ");

            // 13. Alertes.CaseId — links an evidence signal to its intervention case.
            context.Database.ExecuteSqlRaw(@"
                IF OBJECT_ID('dbo.Alertes', 'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.columns
                                   WHERE object_id = OBJECT_ID('dbo.Alertes') AND name = 'CaseId')
                    ALTER TABLE Alertes ADD CaseId INT NULL REFERENCES InterventionCases(Id);
            ");
        }
    }
}
