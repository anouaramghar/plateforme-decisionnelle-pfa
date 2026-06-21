namespace PlateformePFA.API.Services
{
    /// <summary>
    /// Auto-generates alerts when business rules are violated:
    ///   - NoteFaible:        student's final grade on a module is below 10
    ///   - AbsenceExcessive:  student's total unjustified absence hours exceed the threshold
    /// Called by NotesController and AbsencesController after every create/update.
    /// </summary>
    public interface IAlerteService
    {
        /// <summary>
        /// Check if the student's grade on a given module warrants a "NoteFaible" alert.
        /// </summary>
        Task CheckNoteAlertAsync(int etudiantId, int moduleId);

        /// <summary>
        /// Check if the student's cumulative unjustified absences warrant an "AbsenceExcessive" alert.
        /// </summary>
        Task CheckAbsenceAlertAsync(int etudiantId);

        /// <summary>
        /// Full scan: checks every student's notes and absences and creates missing alerts.
        /// Call after seeding or bulk imports.
        /// </summary>
        Task<(int notes, int absences)> ScanAllAsync();
    }
}
