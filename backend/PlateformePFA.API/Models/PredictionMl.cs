namespace PlateformePFA.API.Models
{
    public class PredictionML
    {
        public int Id { get; set; }
        public string Modele { get; set; } = string.Empty;
        public float Probabilite { get; set; }
        public DateTime DatePrediction { get; set; }
        public string FeaturesJSON { get; set; } = string.Empty;

        // Clé étrangère vers Etudiant
        public int EtudiantId { get; set; }
        public Etudiant Etudiant { get; set; } = null!;
    }
}