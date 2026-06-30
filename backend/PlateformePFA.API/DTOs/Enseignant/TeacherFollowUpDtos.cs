namespace PlateformePFA.API.DTOs.Enseignant;

public sealed record TeacherFollowUpCardDto(
    int CaseId,
    int EtudiantId,
    string StudentName,
    string Motif,
    string Priority,
    string Column,
    string? LastAction,
    DateTime CreeLe);

public sealed record TeacherFollowUpActionDto(string Contenu);
