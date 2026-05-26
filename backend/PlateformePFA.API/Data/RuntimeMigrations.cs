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
        }
    }
}
