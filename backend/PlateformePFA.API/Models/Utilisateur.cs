namespace PlateformePFA.API.Models
{
    public class Utilisateur
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MotDePasseHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // "Admin", "Responsable", "Enseignant"
    }
}