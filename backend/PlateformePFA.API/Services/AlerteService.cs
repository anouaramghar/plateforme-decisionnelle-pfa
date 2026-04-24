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

        // ── Note-based alert ──────────────────────────────────────
        public async Task CheckNoteAlertAsync(int etudiantId, int moduleId)
        {
            // Get the latest note for this student × module
            var note = await _context.Notes
                .Where(n => n.EtudiantId == etudiantId && n.ModuleId == moduleId)
                .OrderByDescending(n => n.CreeLe)
                .FirstOrDefaultAsync();

            if (note?.NoteFinal == null || note.NoteFinal >= SeuilNoteFaible)
                return; // No alert needed

            // Avoid duplicate: check if an unresolved "NoteFaible" alert already exists
            // for the same student + module combination.
            bool alreadyExists = await _context.Alertes.AnyAsync(a =>
                a.EtudiantId == etudiantId
                && a.Type == "NoteFaible"
                && !a.Resolue
                && a.Message != null && a.Message.Contains($"ModuleId={moduleId}"));

            if (alreadyExists)
                return;

            var niveau = note.NoteFinal < 5m ? "Critique"
                       : note.NoteFinal < 8m ? "Eleve"
                       : "Moyen";

            var alerte = new Alerte
            {
                EtudiantId = etudiantId,
                Type       = "NoteFaible",
                Niveau     = niveau,
                Message    = $"Note finale {note.NoteFinal:F2}/20 sur ModuleId={moduleId} (seuil : {SeuilNoteFaible})",
                Resolue    = false,
                CreeLe     = DateTime.UtcNow
            };

            _context.Alertes.Add(alerte);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Alerte auto-générée : NoteFaible — EtudiantId={EtudiantId}, ModuleId={ModuleId}, Note={Note}",
                etudiantId, moduleId, note.NoteFinal);
        }

        // ── Absence-based alert ───────────────────────────────────
        public async Task CheckAbsenceAlertAsync(int etudiantId)
        {
            // Sum only unjustified hours
            int totalHeures = await _context.Absences
                .Where(a => a.EtudiantId == etudiantId && !a.Justifiee)
                .SumAsync(a => a.NombreHeures);

            if (totalHeures <= SeuilAbsenceH)
                return; // Below threshold

            // Avoid duplicate: check if an unresolved "AbsenceExcessive" alert already exists
            bool alreadyExists = await _context.Alertes.AnyAsync(a =>
                a.EtudiantId == etudiantId
                && a.Type == "AbsenceExcessive"
                && !a.Resolue);

            if (alreadyExists)
                return;

            var niveau = totalHeures > 40 ? "Critique"
                       : totalHeures > 30 ? "Eleve"
                       : "Moyen";

            var alerte = new Alerte
            {
                EtudiantId = etudiantId,
                Type       = "AbsenceExcessive",
                Niveau     = niveau,
                Message    = $"Total absences non justifiées : {totalHeures}h (seuil : {SeuilAbsenceH}h)",
                Resolue    = false,
                CreeLe     = DateTime.UtcNow
            };

            _context.Alertes.Add(alerte);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Alerte auto-générée : AbsenceExcessive — EtudiantId={EtudiantId}, Heures={Heures}",
                etudiantId, totalHeures);
        }
    }
}
