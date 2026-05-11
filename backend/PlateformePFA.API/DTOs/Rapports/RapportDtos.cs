using System.ComponentModel.DataAnnotations;

namespace PlateformePFA.API.DTOs.Rapports
{
    public class RapportDto
    {
        public int    Id          { get; set; }
        public string TemplateId  { get; set; } = string.Empty;
        public string Titre       { get; set; } = string.Empty;
        public string Format      { get; set; } = string.Empty;
        public string FiliereCode { get; set; } = string.Empty;
        public string Periode     { get; set; } = string.Empty;
        public string AuteurNom   { get; set; } = string.Empty;
        public int    Taille      { get; set; }
        public DateTime CreeLe    { get; set; }
    }

    public class CreateRapportDto
    {
        [Required]
        [MaxLength(40)]
        public string TemplateId { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(PDF|XLSX|CSV)$",
            ErrorMessage = "Format must be PDF, XLSX, or CSV.")]
        public string Format { get; set; } = "PDF";

        [MaxLength(20)]
        public string FiliereCode { get; set; } = "TOUS";

        [MaxLength(40)]
        public string Periode { get; set; } = "Semestre 2 · 2025/2026";
    }
}
