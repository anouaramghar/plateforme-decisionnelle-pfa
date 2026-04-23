using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Note
    {
        public int      Id         { get; set; }
        public int      EtudiantId { get; set; }
        public int      ModuleId   { get; set; }
        public decimal? NoteExamen { get; set; }
        public decimal? NoteTD     { get; set; }
        public decimal? NoteTP     { get; set; }
        public decimal? NoteFinal  { get; set; }
        public string   Annee      { get; set; } = string.Empty;
        public string   Semestre   { get; set; } = string.Empty; // S1 | S2
        public DateTime CreeLe     { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public Etudiant Etudiant { get; set; } = null!;
        [JsonIgnore] public Module   Module   { get; set; } = null!;
    }
}