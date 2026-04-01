using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Alerte
    {
        public int       Id         { get; set; }
        public int       EtudiantId { get; set; }
        public string    Type       { get; set; } = string.Empty; // RisqueEchec | AbsenceExcessive | NoteFaible | Abandon
        public string    Niveau     { get; set; } = string.Empty; // Faible | Moyen | Eleve | Critique
        public string?   Message    { get; set; }
        public bool      Resolue    { get; set; }
        public DateTime  CreeLe     { get; set; } = DateTime.UtcNow;
        public DateTime? ResolueeLe { get; set; }

        [JsonIgnore] public Etudiant Etudiant { get; set; } = null!;
    }
}