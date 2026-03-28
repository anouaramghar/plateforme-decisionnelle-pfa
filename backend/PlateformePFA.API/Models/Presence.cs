namespace PlateformePFA.API.Models
{
    public class Presence
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Statut { get; set; } = string.Empty;

        // Clés étrangères
        public int EtudiantId { get; set; }
        public Etudiant Etudiant { get; set; } = null!;

        public int ModuleId { get; set; }
        public Module Module { get; set; } = null!;
    }
}