using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Filiere
    {
        public int    Id            { get; set; }
        public string Code          { get; set; } = string.Empty;
        public string Intitule      { get; set; } = string.Empty;
        public int?   ResponsableId { get; set; }

        [JsonIgnore] public Utilisateur?          Responsable { get; set; }
        [JsonIgnore] public ICollection<Etudiant>? Etudiants  { get; set; }
        [JsonIgnore] public ICollection<Module>?   Modules    { get; set; }
    }
}