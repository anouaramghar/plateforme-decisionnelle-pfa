-- ============================================================
-- ETL : PFA_DB (OLTP) → PFA_DW (Data Warehouse)
-- Plateforme Décisionnelle ENIAD 2025/2026
--
-- Idempotent UPSERT: safe to run repeatedly.
-- Call via: POST /api/admin/sync-dw  (AdminController)
--
-- All four MERGEs run inside a single transaction protected by
-- an exclusive application lock so concurrent ETL runs are
-- serialised rather than racing each other.  If a run is
-- already in progress the lock attempt returns immediately
-- (timeout = 0) and error 51002 is thrown — AdminController
-- converts this to HTTP 409 Conflict.
-- ============================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lock_result INT;
EXEC @lock_result = sys.sp_getapplock
    @Resource    = 'PFA_DW_ETL',
    @LockMode    = 'Exclusive',
    @LockOwner   = 'Transaction',
    @LockTimeout = 0;
IF @lock_result < 0
    THROW 51002, 'ETL already running', 1;

USE PFA_DW;

-- ─── 1. Sync DimEtudiant ─────────────────────────────────────
MERGE DimEtudiant AS target
USING (
    SELECT e.Matricule, e.Nom, e.Prenom, f.Intitule AS Filiere, e.Niveau
    FROM PFA_DB.dbo.Etudiants e
    JOIN PFA_DB.dbo.Filieres  f ON f.Id = e.FiliereId
) AS src ON target.Matricule = src.Matricule
WHEN MATCHED THEN
    UPDATE SET Nom = src.Nom, Prenom = src.Prenom,
               Filiere = src.Filiere, Niveau = src.Niveau
WHEN NOT MATCHED THEN
    INSERT (Matricule, Nom, Prenom, Filiere, Niveau)
    VALUES (src.Matricule, src.Nom, src.Prenom, src.Filiere, src.Niveau);

-- ─── 2. Sync DimModule ───────────────────────────────────────
MERGE DimModule AS target
USING (
    SELECT m.Code, m.Nom, f.Intitule AS Filiere, m.Coefficient
    FROM PFA_DB.dbo.Modules  m
    JOIN PFA_DB.dbo.Filieres f ON f.Id = m.FiliereId
) AS src ON target.Code = src.Code
WHEN MATCHED THEN
    UPDATE SET Nom = src.Nom, Filiere = src.Filiere,
               Coefficient = src.Coefficient
WHEN NOT MATCHED THEN
    INSERT (Code, Nom, Filiere, Coefficient)
    VALUES (src.Code, src.Nom, src.Filiere, src.Coefficient);

-- ─── 3. Sync DimTemps ────────────────────────────────────────
MERGE DimTemps AS target
USING (
    SELECT DISTINCT Annee, Semestre FROM PFA_DB.dbo.Notes
) AS src ON target.Annee = src.Annee AND target.Semestre = src.Semestre
WHEN NOT MATCHED THEN
    INSERT (Annee, Semestre) VALUES (src.Annee, src.Semestre);

-- ─── 4. Sync FaitNotes (fact table) ──────────────────────────
-- LatestPredictions CTE deduplicates Predictions so the MERGE source
-- never yields two rows for the same (EtudiantKey, ModuleKey, TempsKey),
-- which would trigger SQL Server error 8672 (duplicate target rows).
;WITH LatestPredictions AS (
    SELECT EtudiantId, Annee, ScoreRisque, Cluster,
           ROW_NUMBER() OVER (
               PARTITION BY EtudiantId, Annee
               ORDER BY CreeLe DESC
           ) AS rn
    FROM PFA_DB.dbo.Predictions
    WHERE TypeModele = 'RisqueEchec'
)
MERGE FaitNotes AS target
USING (
    SELECT
        de.EtudiantKey,
        dm.ModuleKey,
        dt.TempsKey,
        n.NoteFinal                                        AS NoteFinale,
        ISNULL((
            SELECT SUM(a.NombreHeures)
            FROM   PFA_DB.dbo.Absences a
            WHERE  a.EtudiantId = n.EtudiantId
              AND  a.ModuleId   = n.ModuleId
              AND  a.Justifiee  = 0                        -- unjustified only (matches batch predict feature)
              -- Fix: for S2 the academic year starts in the *second* calendar
              -- year of the pair (e.g. 2026 for "2025/2026 S2"), not the first.
              AND  a.DateAbsence BETWEEN
                     DATEFROMPARTS(
                         IIF(n.Semestre = 'S1', LEFT(n.Annee, 4), RIGHT(n.Annee, 4)),
                         IIF(n.Semestre = 'S1', 9, 2),
                         1)
                 AND DATEFROMPARTS(
                         RIGHT(n.Annee, 4),
                         IIF(n.Semestre = 'S1', 1, 7),
                         31)
        ), 0)                                              AS NbAbsences,
        p.ScoreRisque,
        p.Cluster
    FROM      PFA_DB.dbo.Notes          n
    JOIN      PFA_DB.dbo.Etudiants      e  ON e.Id         = n.EtudiantId
    JOIN      DimEtudiant               de ON de.Matricule  = e.Matricule
    JOIN      PFA_DB.dbo.Modules        m  ON m.Id          = n.ModuleId
    JOIN      DimModule                 dm ON dm.Code       = m.Code
    JOIN      DimTemps                  dt ON dt.Annee      = n.Annee
                                         AND dt.Semestre   = n.Semestre
    LEFT JOIN LatestPredictions         p  ON p.EtudiantId = n.EtudiantId
                                         AND p.Annee      = n.Annee
                                         AND p.rn         = 1
) AS src ON  target.EtudiantKey = src.EtudiantKey
         AND target.ModuleKey   = src.ModuleKey
         AND target.TempsKey    = src.TempsKey
WHEN MATCHED THEN
    UPDATE SET NoteFinale  = src.NoteFinale,
               NbAbsences  = src.NbAbsences,
               ScoreRisque = src.ScoreRisque,
               Cluster     = src.Cluster
WHEN NOT MATCHED THEN
    INSERT (EtudiantKey, ModuleKey, TempsKey, NoteFinale, NbAbsences, ScoreRisque, Cluster)
    VALUES (src.EtudiantKey, src.ModuleKey, src.TempsKey,
            src.NoteFinale, src.NbAbsences, src.ScoreRisque, src.Cluster)
-- Physically-deleted OLTP notes must also disappear from the warehouse,
-- otherwise BI reports and future ML training keep counting stale grades.
-- The source already covers every current note (dimensions are synced first
-- in this same transaction), so any fact with no matching source row is an
-- orphan from a deleted note.
WHEN NOT MATCHED BY SOURCE THEN
    DELETE;

COMMIT TRANSACTION;
