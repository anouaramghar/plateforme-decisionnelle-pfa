using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Services
{
    public class AlerteService : IAlerteService
    {
        private const decimal SeuilNoteFaible  = 10m;   // Moyenne < 10 → alerte
        private const int     SeuilAbsenceH    = 20;    // > 20 h non justifiées → alerte

        private readonly AppDbContext _context;
        private readonly ILogger<AlerteService> _logger;

        public AlerteService(AppDbContext context, ILogger<AlerteService> logger)
        {
            _context = context;
            _logger  = logger;
        }

        // Levels are ordered Faible < Moyen < Eleve < Critique. Escalations move
        // up; a "lower" niveau on a re-check never downgrades an existing alert.
        private static readonly Dictionary<string, int> NiveauRank = new()
        {
            ["Faible"]   = 0,
            ["Moyen"]    = 1,
            ["Eleve"]    = 2,
            ["Critique"] = 3,
        };

        // ── Note-based alert ──────────────────────────────────────
        public async Task CheckNoteAlertAsync(int etudiantId, int moduleId)
        {
            var note = await _context.Notes
                .Where(n => n.EtudiantId == etudiantId && n.ModuleId == moduleId)
                .OrderByDescending(n => n.CreeLe)
                .FirstOrDefaultAsync();

            if (note?.NoteFinal == null) return;

            // If grade is now above threshold, auto-resolve any open NoteFaible alert.
            if (note.NoteFinal >= SeuilNoteFaible)
            {
                var toResolve = await _context.Alertes
                    .FirstOrDefaultAsync(a =>
                        a.EtudiantId == etudiantId
                        && a.ModuleId == moduleId
                        && a.Type    == "NoteFaible"
                        && !a.Resolue);
                if (toResolve != null)
                {
                    toResolve.Resolue = true;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        "Alerte NoteFaible résolue automatiquement : EtudiantId={E}, ModuleId={M}",
                        etudiantId, moduleId);
                }
                return;
            }

            var niveau = note.NoteFinal < 5m ? "Critique"
                       : note.NoteFinal < 8m ? "Eleve"
                       : "Moyen";

            // Structured dedupe on (EtudiantId, ModuleId, Type) — replaces the
            // previous fragile Message.Contains("ModuleId=N") match.
            var existing = await _context.Alertes
                .FirstOrDefaultAsync(a =>
                    a.EtudiantId == etudiantId
                    && a.ModuleId   == moduleId
                    && a.Type       == "NoteFaible"
                    && !a.Resolue);

            var message = $"Note finale {note.NoteFinal:F2}/20 (seuil : {SeuilNoteFaible})";

            if (existing != null)
            {
                // Escalate if the new niveau is higher than the existing one.
                if (NiveauRank[niveau] > NiveauRank[existing.Niveau])
                {
                    existing.Niveau  = niveau;
                    existing.Message = message;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        "Alerte NoteFaible escaladée : EtudiantId={E}, ModuleId={M}, {Old}→{New}",
                        etudiantId, moduleId, existing.Niveau, niveau);
                }
                return;
            }

            _context.Alertes.Add(new Alerte
            {
                EtudiantId = etudiantId,
                ModuleId   = moduleId,
                Type       = "NoteFaible",
                Niveau     = niveau,
                Message    = message,
                Resolue    = false,
                CreeLe     = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Alerte créée : NoteFaible — EtudiantId={E}, ModuleId={M}, Note={N}",
                etudiantId, moduleId, note.NoteFinal);
        }

        // ── Absence-based alert ───────────────────────────────────
        public async Task CheckAbsenceAlertAsync(int etudiantId)
        {
            int totalHeures = await _context.Absences
                .Where(a => a.EtudiantId == etudiantId && !a.Justifiee)
                .SumAsync(a => a.NombreHeures);

            if (totalHeures <= SeuilAbsenceH) return;

            var niveau = totalHeures > 40 ? "Critique"
                       : totalHeures > 30 ? "Eleve"
                       : "Moyen";

            var existing = await _context.Alertes
                .FirstOrDefaultAsync(a =>
                    a.EtudiantId == etudiantId
                    && a.Type    == "AbsenceExcessive"
                    && !a.Resolue);

            var message = $"Total absences non justifiées : {totalHeures}h (seuil : {SeuilAbsenceH}h)";

            if (existing != null)
            {
                if (NiveauRank[niveau] > NiveauRank[existing.Niveau])
                {
                    var old = existing.Niveau;
                    existing.Niveau  = niveau;
                    existing.Message = message;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        "Alerte AbsenceExcessive escaladée : EtudiantId={E}, {Old}→{New}",
                        etudiantId, old, niveau);
                }
                return;
            }

            _context.Alertes.Add(new Alerte
            {
                EtudiantId = etudiantId,
                ModuleId   = null,
                Type       = "AbsenceExcessive",
                Niveau     = niveau,
                Message    = message,
                Resolue    = false,
                CreeLe     = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Alerte créée : AbsenceExcessive — EtudiantId={E}, Heures={H}",
                etudiantId, totalHeures);
        }
    }
}
