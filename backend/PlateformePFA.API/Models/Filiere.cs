using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Filiere
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Intitule { get; set; } = string.Empty;

        public int? ResponsableId { get; set; }

        [JsonIgnore] public Utilisateur?           Responsable { get; set; }
        [JsonIgnore] public ICollection<Etudiant>? Etudiants   { get; set; }
        [JsonIgnore] public ICollection<Module>?   Modules     { get; set; }
    }
}