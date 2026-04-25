using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Etudiant
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Matricule { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Nom { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Prenom { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Email { get; set; }

        public int FiliereId { get; set; }

        [Required]
        [MaxLength(10)]
        public string Niveau { get; set; } = string.Empty; // CP1, CP2, CI1, CI2, CI3

        [Required]
        [MaxLength(9)]
        public string Annee { get; set; } = string.Empty; // ex: 2025/2026 — see Validation.AnneePattern

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public Filiere?                  Filiere       { get; set; }
        [JsonIgnore] public ICollection<Note>?         Notes         { get; set; }
        [JsonIgnore] public ICollection<Absence>?      Absences      { get; set; }
        [JsonIgnore] public ICollection<Alerte>?       Alertes       { get; set; }
        [JsonIgnore] public ICollection<PredictionML>? PredictionsML { get; set; }
    }
}