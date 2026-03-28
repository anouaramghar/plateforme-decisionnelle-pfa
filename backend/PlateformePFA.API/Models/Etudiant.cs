using System.Text.Json.Serialization; 

namespace PlateformePFA.API.Models
{
    public class Etudiant
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string CNE { get; set; } = string.Empty;
        public string Ville { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Promotion { get; set; } = string.Empty;
        public string Statut { get; set; } = "Actif"; // Actif, Décrocheur, Diplômé

        // La clé étrangère
        public int FiliereId { get; set; }

        // --- LES RELATIONS MASQUÉES POUR SWAGGER ---

        [JsonIgnore]
        public Filiere? Filiere { get; set; }

        [JsonIgnore]
        public ICollection<Note>? Notes { get; set; }

        [JsonIgnore]
        public ICollection<Presence>? Presences { get; set; }

        [JsonIgnore]
        public ICollection<Alerte>? Alertes { get; set; }

        [JsonIgnore]
        public ICollection<PredictionML>? PredictionsML { get; set; }
    }
}