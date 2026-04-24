using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Module
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Nom { get; set; } = string.Empty; // was "Intitule"

        public int FiliereId { get; set; }

        [Required]
        [MaxLength(10)]
        public string Niveau { get; set; } = string.Empty;

        [Column(TypeName = "decimal(4,2)")]
        public decimal Coefficient { get; set; } = 1;

        [Required]
        [MaxLength(5)]
        public string Semestre { get; set; } = string.Empty; // S1 | S2

        [JsonIgnore] public Filiere?              Filiere  { get; set; }
        [JsonIgnore] public ICollection<Note>?    Notes    { get; set; }
        [JsonIgnore] public ICollection<Absence>? Absences { get; set; }
    }
}