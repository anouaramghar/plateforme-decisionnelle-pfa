using System;

namespace PlateformePFA.API.Helpers
{
    public static class AcademicPeriod
    {
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
