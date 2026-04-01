namespace PlateformePFA.API.DTOs.Etudiants
{
    public class EtudiantDto
    {
        public int    Id        { get; set; }
        public string Matricule { get; set; } = string.Empty;
        public string Nom       { get; set; } = string.Empty;
        public string Prenom    { get; set; } = string.Empty;
        public string Email     { get; set; } = string.Empty;
        public string Filiere   { get; set; } = string.Empty;
        public string Niveau    { get; set; } = string.Empty;
        public string Annee     { get; set; } = string.Empty;
    }

    public class CreateEtudiantDto
    {
        public string Matricule { get; set; } = string.Empty;
        public string Nom       { get; set; } = string.Empty;
        public string Prenom    { get; set; } = string.Empty;
        public string Email     { get; set; } = string.Empty;
        public string Filiere   { get; set; } = string.Empty;
        public string Niveau    { get; set; } = string.Empty;  // L1 L2 L3 M1 M2
        public string Annee     { get; set; } = string.Empty;  // ex: 2025/2026
    }
}
