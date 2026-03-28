using System.Text.Json.Serialization; 
namespace PlateformePFA.API.Models
{
    public class Filiere
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Intitule { get; set; } = string.Empty;
        public int NbSemestres { get; set; }
        public int NbModules { get; set; }
        public int NbEtudiants { get; set; }

        // La clé étrangère (Celle qu'on envoie dans le JSON)
        public int ResponsableId { get; set; }

        // On ajoute [JsonIgnore] et un "?" après Utilisateur
        [JsonIgnore]
        public Utilisateur? Responsable { get; set; }

        // On fait pareil pour les listes pour alléger Swagger !
        [JsonIgnore]
        public ICollection<Etudiant>? Etudiants { get; set; }

        [JsonIgnore]
        public ICollection<Module>? Modules { get; set; }
    }
}