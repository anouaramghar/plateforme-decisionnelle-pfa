using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Utilisateur
    {
        public int      Id             { get; set; }
        public string   Nom            { get; set; } = string.Empty;
        public string   Prenom         { get; set; } = string.Empty;
        public string   Email          { get; set; } = string.Empty;

        [JsonIgnore] // Never serialize the hash in API responses
        public string   MotDePasseHash { get; set; } = string.Empty;

        public string   Role           { get; set; } = string.Empty; // Admin | Responsable | Enseignant
        public bool     EstActif       { get; set; } = true;
        public DateTime CreeLe         { get; set; } = DateTime.UtcNow;
    }
}