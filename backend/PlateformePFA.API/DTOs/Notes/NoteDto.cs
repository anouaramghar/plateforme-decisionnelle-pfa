namespace PlateformePFA.API.DTOs.Notes
{
    public class NoteDto
    {
        public int     Id          { get; set; }
        public int     EtudiantId  { get; set; }
        public string  Matricule   { get; set; } = string.Empty;
        public string  NomEtudiant { get; set; } = string.Empty;
        public int     ModuleId    { get; set; }
        public string  NomModule   { get; set; } = string.Empty;
        public decimal? NoteExamen { get; set; }
        public decimal? NoteTD     { get; set; }
        public decimal? NoteTP     { get; set; }
        public decimal? NoteFinal  { get; set; }
        public string  Annee       { get; set; } = string.Empty;
        public string  Semestre    { get; set; } = string.Empty;
    }

    public class CreateNoteDto
    {
        public int     EtudiantId  { get; set; }
        public int     ModuleId    { get; set; }
        public decimal? NoteExamen { get; set; }
        public decimal? NoteTD     { get; set; }
        public decimal? NoteTP     { get; set; }
        public decimal? NoteFinal  { get; set; }
        public string  Annee       { get; set; } = string.Empty;
        public string  Semestre    { get; set; } = string.Empty;
    }
}
