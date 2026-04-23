using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Module
    {
        public int     Id          { get; set; }
        public string  Code        { get; set; } = string.Empty;
        public string  Nom         { get; set; } = string.Empty; // was "Intitule"
        public int     FiliereId   { get; set; }
        public string  Niveau      { get; set; } = string.Empty;
        public decimal Coefficient { get; set; } = 1;
        public string  Semestre    { get; set; } = string.Empty; // S1 | S2  (was int)

        [JsonIgnore] public Filiere?              Filiere  { get; set; }
        [JsonIgnore] public ICollection<Note>?    Notes    { get; set; }
        [JsonIgnore] public ICollection<Absence>? Absences { get; set; }
    }
}