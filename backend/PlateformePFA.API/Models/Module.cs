using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Module
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Intitule { get; set; } = string.Empty;
        public float Coefficient { get; set; }
        public int Semestre { get; set; }

        // --- CLÉS ÉTRANGÈRES ---
        public int FiliereId { get; set; }
        public int EnseignantId { get; set; }

        // --- PROPRIÉTÉS DE NAVIGATION (Masquées pour Swagger) ---
        
        [JsonIgnore]
        public Filiere? Filiere { get; set; }

        [JsonIgnore]
        public Utilisateur? Enseignant { get; set; }

        [JsonIgnore]
        public ICollection<Note>? Notes { get; set; }

        [JsonIgnore]
        public ICollection<Presence>? Presences { get; set; }
    }
}