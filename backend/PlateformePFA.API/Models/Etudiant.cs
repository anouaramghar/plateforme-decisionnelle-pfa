using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Etudiant
    {
        public int      Id        { get; set; }
        public string   Matricule { get; set; } = string.Empty;
        public string   Nom       { get; set; } = string.Empty;
        public string   Prenom    { get; set; } = string.Empty;
        public string?  Email     { get; set; }
        public int      FiliereId { get; set; }
        public string   Niveau    { get; set; } = string.Empty; // L1 L2 L3 M1 M2
        public string   Annee     { get; set; } = string.Empty; // ex: 2025/2026
        public DateTime CreeLe    { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public Filiere?                  Filiere       { get; set; }
        [JsonIgnore] public ICollection<Note>?         Notes         { get; set; }
        [JsonIgnore] public ICollection<Absence>?      Absences      { get; set; }
        [JsonIgnore] public ICollection<Alerte>?       Alertes       { get; set; }
        [JsonIgnore] public ICollection<PredictionML>? PredictionsML { get; set; }
    }
}