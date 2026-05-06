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
        }
    }
}
