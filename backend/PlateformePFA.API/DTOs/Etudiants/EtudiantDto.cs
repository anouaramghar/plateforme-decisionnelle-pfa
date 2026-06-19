using System.ComponentModel.DataAnnotations;
using PlateformePFA.API.DTOs.Common;

namespace PlateformePFA.API.DTOs.Etudiants
{
    public class EtudiantDto
    {
        public int    Id            { get; set; }
        public string Matricule     { get; set; } = string.Empty;
        public string Nom           { get; set; } = string.Empty;
        public string Prenom        { get; set; } = string.Empty;
        public string? Email        { get; set; }
        public int    FiliereId     { get; set; }
        public string FiliereCode   { get; set; } = string.Empty;
        public string FiliereIntitule { get; set; } = string.Empty;
        public string Niveau        { get; set; } = string.Empty;
        public string Annee         { get; set; } = string.Empty;
    }

    /// <summary>
    /// Enriched student row for the Étudiants list view: rolls up notes,
    /// absences, validated modules, and the latest ML risk score.
    /// </summary>
    public class EtudiantWithStatsDto
    {
        public int    Id            { get; set; }
        public string Matricule     { get; set; } = string.Empty;
        public string Nom           { get; set; } = string.Empty;
        public string Prenom        { get; set; } = string.Empty;
        public string NomComplet    { get; set; } = string.Empty;
        public string? Email        { get; set; }
        public int    FiliereId     { get; set; }
        public string FiliereCode   { get; set; } = string.Empty;
        public string FiliereIntitule { get; set; } = string.Empty;
        public string Niveau        { get; set; } = string.Empty;
        public string Annee         { get; set; } = string.Empty;

        public decimal Moyenne        { get; set; }
        public int     Absences       { get; set; }      // total hours
        public int     ModulesValides { get; set; }
        public int     ModulesTotal   { get; set; }
        public decimal ScoreRisque    { get; set; }      // 0.0 – 1.0
        public string  Risque         { get; set; } = "faible"; // faible | modere | eleve
        public string  Statut         { get; set; } = "Régulier"; // Régulier | Suivi | En difficulté
    }

    /// <summary>Per-module note row for the student detail drawer.</summary>
    public class EtudiantNoteDto
    {
        public int     ModuleId  { get; set; }
        public string  Code      { get; set; } = string.Empty;
        public string  Nom       { get; set; } = string.Empty;
        public string  Semestre  { get; set; } = string.Empty;
        public string  Annee     { get; set; } = string.Empty;
        public decimal Coef      { get; set; }
        public decimal? Cc       { get; set; }
        public decimal? Tp       { get; set; }
        public decimal? Exam     { get; set; }
        public decimal? Finale   { get; set; }
        public bool    Valide    { get; set; }
    }

    public class CreateEtudiantDto
    {
        [Required]
        [MaxLength(20)]
        public string Matricule { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Nom { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Prenom { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "FiliereId must be a positive integer.")]
        public int FiliereId { get; set; }

        [Required]
        [MaxLength(10)]
        public string Niveau { get; set; } = string.Empty; // CP1, CP2, CI1, CI2, CI3

        [Required]
        [MaxLength(9)]
        [RegularExpression(Validation.AnneePattern, ErrorMessage = Validation.AnneeError)]
        public string Annee { get; set; } = string.Empty;
    }

    public class UpdateEtudiantDto
    {
        [Required]
        [MaxLength(20)]
        public string Matricule { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Nom { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Prenom { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "FiliereId must be a positive integer.")]
        public int FiliereId { get; set; }

        [Required]
        [MaxLength(10)]
        public string Niveau { get; set; } = string.Empty;

        [Required]
        [MaxLength(9)]
        [RegularExpression(Validation.AnneePattern, ErrorMessage = Validation.AnneeError)]
        public string Annee { get; set; } = string.Empty;
    }
}
