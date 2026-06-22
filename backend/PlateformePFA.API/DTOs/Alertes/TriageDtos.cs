using System.ComponentModel.DataAnnotations;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.DTOs.Alertes
{
    public class DismissSignalDto
    {
        // Mandatory: a signal is never dismissed silently.
        [Required, MaxLength(500)]
        public string Raison { get; set; } = string.Empty;
    }

    // One row of the triage queue: all un-triaged signals for a single student,
    // grouped so multiple alerts become one triage decision (not duplicate cases).
    public class TriageGroupDto
    {
        public int EtudiantId { get; set; }
        public string Matricule { get; set; } = string.Empty;
        public string EtudiantNom { get; set; } = string.Empty;
        public int FiliereId { get; set; }

        public int SignalCount { get; set; }
        public string MaxNiveau { get; set; } = string.Empty;     // highest severity in the group
        public string SuggestedPriorite { get; set; } = "Medium"; // mapped from MaxNiveau

        // If the student already has an open case, the UI links the signals to it
        // instead of offering "open new case".
        public int? OpenCaseId { get; set; }

        public List<Alerte> Signals { get; set; } = new();
    }
}
