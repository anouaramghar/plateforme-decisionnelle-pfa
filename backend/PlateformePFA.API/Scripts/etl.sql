-- ============================================================
-- ETL : PFA_DB (OLTP) → PFA_DW (Data Warehouse)
-- Plateforme Décisionnelle ENIAD 2025/2026
--
-- Idempotent UPSERT: safe to run repeatedly.
-- Call via: POST /api/admin/sync-dw  (AdminController)
-- ============================================================

USE PFA_DW;
GO

-- ─── 1. Sync DimEtudiant ─────────────────────────────────────
MERGE DimEtudiant AS target
USING (
    SELECT Matricule, Nom, Prenom, Filiere, Niveau
    FROM PFA_DB.dbo.Etudiants
) AS src ON target.Matricule = src.Matricule
WHEN MATCHED THEN
    UPDATE SET Nom = src.Nom, Prenom = src.Prenom,
               Filiere = src.Filiere, Niveau = src.Niveau
WHEN NOT MATCHED THEN
    INSERT (Matricule, Nom, Prenom, Filiere, Niveau)
    VALUES (src.Matricule, src.Nom, src.Prenom, src.Filiere, src.Niveau);
GO

-- ─── 2. Sync DimModule ───────────────────────────────────────
MERGE DimModule AS target
USING (
    SELECT Code, Nom, Filiere, Coefficient
    FROM PFA_DB.dbo.Modules
) AS src ON target.Code = src.Code
WHEN MATCHED THEN
    UPDATE SET Nom = src.Nom, Filiere = src.Filiere,
               Coefficient = src.Coefficient
WHEN NOT MATCHED THEN
    INSERT (Code, Nom, Filiere, Coefficient)
    VALUES (src.Code, src.Nom, src.Filiere, src.Coefficient);
GO

-- ─── 3. Sync DimTemps ────────────────────────────────────────
MERGE DimTemps AS target
USING (
    SELECT DISTINCT Annee, Semestre FROM PFA_DB.dbo.Notes
) AS src ON target.Annee = src.Annee AND target.Semestre = src.Semestre
WHEN NOT MATCHED THEN
    INSERT (Annee, Semestre) VALUES (src.Annee, src.Semestre);
GO

-- ─── 4. Sync FaitNotes (fact table) ──────────────────────────
-- Full incremental upsert keyed on (EtudiantKey, ModuleKey, TempsKey).
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
              AND  a.DateAbsence BETWEEN
                     DATEFROMPARTS(LEFT(n.Annee, 4), IIF(n.Semestre = 'S1', 9, 2), 1)
                 AND DATEFROMPARTS(RIGHT(n.Annee, 4), IIF(n.Semestre = 'S1', 1, 7), 31)
        ), 0)                                              AS NbAbsences,
        p.ScoreRisque,
        p.Cluster
    FROM      PFA_DB.dbo.Notes          n
    JOIN      PFA_DB.dbo.Etudiants      e  ON e.Id          = n.EtudiantId
    JOIN      DimEtudiant               de ON de.Matricule   = e.Matricule
    JOIN      PFA_DB.dbo.Modules        m  ON m.Id           = n.ModuleId
    JOIN      DimModule                 dm ON dm.Code        = m.Code
    JOIN      DimTemps                  dt ON dt.Annee       = n.Annee
                                          AND dt.Semestre    = n.Semestre
    LEFT JOIN PFA_DB.dbo.Predictions    p  ON p.EtudiantId  = n.EtudiantId
                                          AND p.Annee       = n.Annee
                                          AND p.TypeModele  = 'RisqueEchec'
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
            src.NoteFinale, src.NbAbsences, src.ScoreRisque, src.Cluster);
GO
