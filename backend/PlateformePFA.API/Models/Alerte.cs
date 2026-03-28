namespace PlateformePFA.API.Models
{
    public class Alerte
    {
        public int Id { get; set; }
        public string TypeAlerte { get; set; } = string.Empty;
        public float ScoreRisque { get; set; }
        public DateTime DateGeneration { get; set; }
        public string Statut { get; set; } = string.Empty;

        // Clé étrangère vers Etudiant
        public int EtudiantId { get; set; }
        public Etudiant Etudiant { get; set; } = null!;
    }
}