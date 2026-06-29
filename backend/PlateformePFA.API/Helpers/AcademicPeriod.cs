using System;
using System.Text.RegularExpressions;

namespace PlateformePFA.API.Helpers
{
    public static class AcademicPeriod
    {
        public const double SessionsPerModule = 32.0;

        public static int? SemesterNumber(string? semestre)
        {
            if (string.IsNullOrWhiteSpace(semestre))
                return null;

            var match = Regex.Match(semestre, @"(?:S|Semestre\s*)([1-9])", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n >= 1 && n <= 9)
                return n;
            return null;
        }

        public static (DateTime Start, DateTime End) SemesterDateRange(string annee, string? semestre)
        {
            var parts = annee.Split('/');
            if (parts.Length == 0 || !int.TryParse(parts[0], out var startYear))
            {
                startYear = DateTime.UtcNow.Year;
            }

            var semNum = SemesterNumber(semestre);
            if (semNum.HasValue)
            {
                var isFirstHalf = semNum.Value % 2 == 1;

                if (isFirstHalf)
                    return (new DateTime(startYear, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(startYear + 1, 2, 1, 0, 0, 0, DateTimeKind.Utc));
                else
                    return (new DateTime(startYear + 1, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(startYear + 1, 8, 1, 0, 0, 0, DateTimeKind.Utc));
            }

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
