using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    /// <summary>
    /// Mapped to SQL table "Absences".
    /// Renamed from Presence → Absence to match the school domain and the SQL schema.
    /// </summary>
    public class Absence
    {
        public int Id { get; set; }
        public int EtudiantId { get; set; }
        public int ModuleId { get; set; }
        public int NombreHeures { get; set; } = 1;
        public bool Justifiee { get; set; }

        [Column(TypeName = "date")]
        public DateTime DateAbsence { get; set; }

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public Etudiant Etudiant { get; set; } = null!;
        [JsonIgnore] public Module   Module   { get; set; } = null!;
    }
}