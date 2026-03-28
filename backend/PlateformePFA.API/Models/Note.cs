namespace PlateformePFA.API.Models
{
    public class Note
    {
        public int Id { get; set; }
        public float Valeur { get; set; }
        public string Session { get; set; } = string.Empty;
        public string AnneeAcad { get; set; } = string.Empty;

        // Clés étrangères
        public int EtudiantId { get; set; }
        public Etudiant Etudiant { get; set; } = null!;

        public int ModuleId { get; set; }
        public Module Module { get; set; } = null!;
    }
}