namespace PlateformePFA.API.DTOs.Common;

/// <summary>
/// Shared regex/format constants used by data-annotation validators.
/// Keeping them in one place prevents the kind of cross-DTO drift that bit
/// us with Annee MaxLength (DB=9 / DTO=20).
/// </summary>
public static class Validation
{
    /// <summary>Academic year, e.g. "2025/2026".</summary>
    public const string AnneePattern = @"^\d{4}/\d{4}$";
    public const string AnneeError   = "Annee must be in the form 'YYYY/YYYY' (e.g. 2025/2026).";

    /// <summary>Semester literal: S1 through S9.</summary>
    public const string SemestrePattern = @"^S[1-9]$";
    public const string SemestreError   = "Semestre must be 'S1' through 'S9'.";
}
