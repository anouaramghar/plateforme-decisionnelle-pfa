using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.DTOs.Notes
{
    public class NoteDto
    {
        public int      Id          { get; set; }
        public int      EtudiantId  { get; set; }
        public string   Matricule   { get; set; } = string.Empty;
        public string   NomEtudiant { get; set; } = string.Empty;
        public int      ModuleId    { get; set; }
        public string   NomModule   { get; set; } = string.Empty;
        public decimal? NoteExamen  { get; set; }
        public decimal? NoteTD      { get; set; }
        public decimal? NoteTP      { get; set; }
        public decimal? NoteFinal   { get; set; }
        public string   Annee       { get; set; } = string.Empty;
        public string   Semestre    { get; set; } = string.Empty;
    }

    public class CreateNoteDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "EtudiantId must be a positive integer.")]
        public int EtudiantId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "ModuleId must be a positive integer.")]
        public int ModuleId { get; set; }

        [Range(0, 20, ErrorMessage = "NoteExamen must be between 0 and 20.")]
        public decimal? NoteExamen { get; set; }

        [Range(0, 20, ErrorMessage = "NoteTD must be between 0 and 20.")]
        public decimal? NoteTD { get; set; }

        [Range(0, 20, ErrorMessage = "NoteTP must be between 0 and 20.")]
        public decimal? NoteTP { get; set; }

        [Range(0, 20, ErrorMessage = "NoteFinal must be between 0 and 20.")]
        public decimal? NoteFinal { get; set; }

        [Required]
        [MaxLength(20)]
        public string Annee { get; set; } = string.Empty;

        [Required]
        [MaxLength(5)]
        public string Semestre { get; set; } = string.Empty; // S1 | S2
    }

    public class UpdateNoteDto
    {
        [Range(0, 20, ErrorMessage = "NoteExamen must be between 0 and 20.")]
        public decimal? NoteExamen { get; set; }

        [Range(0, 20, ErrorMessage = "NoteTD must be between 0 and 20.")]
        public decimal? NoteTD { get; set; }

        [Range(0, 20, ErrorMessage = "NoteTP must be between 0 and 20.")]
        public decimal? NoteTP { get; set; }

        [Range(0, 20, ErrorMessage = "NoteFinal must be between 0 and 20.")]
        public decimal? NoteFinal { get; set; }

        [Required]
        [MaxLength(20)]
        public string Annee { get; set; } = string.Empty;

        [Required]
        [MaxLength(5)]
        public string Semestre { get; set; } = string.Empty; // S1 | S2
    }
}
