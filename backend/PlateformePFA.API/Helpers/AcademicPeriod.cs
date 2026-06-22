using System;

namespace PlateformePFA.API.Helpers
{
    public static class AcademicPeriod
    {
        // Scheduled sessions per module per semester. Used to turn an absence-hour
        // count into a rate (absenceHours / (nbModules * SessionsPerModule)).
        // ponytail: single school-wide constant; if modules ever differ in volume,
        // move this onto the Module row (e.g. a VolumeHoraire column) and read it
        // per-module. Keep in sync with ml_service/data/db_loader.SESSIONS_PER_MODULE.
        public const double SessionsPerModule = 32.0;

        public static (DateTime Start, DateTime End) SemesterDateRange(string annee, string? semestre)
        {
            var parts = annee.Split('/');
            if (parts.Length == 0 || !int.TryParse(parts[0], out var startYear))
            {
                startYear = DateTime.UtcNow.Year;
            }
            if (semestre == "S1") return (new DateTime(startYear, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(startYear + 1, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            if (semestre == "S2") return (new DateTime(startYear + 1, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(startYear + 1, 8, 1, 0, 0, 0, DateTimeKind.Utc));
            return (new DateTime(startYear, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(startYear + 1, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        public static string CurrentAcademicYear()
        {
            var now = DateTime.UtcNow;
            var start = now.Month >= 9 ? now.Year : now.Year - 1;
            return $"{start}/{start + 1}";
        }
    }
}
