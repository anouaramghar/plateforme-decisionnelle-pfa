using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PlateformePFA.API.Models
{
    public class Note
    {
        public int Id { get; set; }
        public int EtudiantId { get; set; }
        public int ModuleId   { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? NoteExamen { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? NoteTD { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? NoteTP { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? NoteFinal { get; set; }

        [Required]
        [MaxLength(9)]
        public string Annee { get; set; } = string.Empty;

        [Required]
        [MaxLength(5)]
        public string Semestre { get; set; } = string.Empty; // S1 | S2

        public DateTime CreeLe { get; set; } = DateTime.UtcNow;

        [JsonIgnore] public Etudiant Etudiant { get; set; } = null!;
        [JsonIgnore] public Module   Module   { get; set; } = null!;
    }
}