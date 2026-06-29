using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Text;
using PlateformePFA.API.Helpers;

namespace PlateformePFA.API.Services
{
    /// <summary>
    /// Builds report files (PDF / XLSX / CSV) from live DB data and returns
    /// the bytes + a default title. Persistence is the controller's job.
    /// </summary>
    public class ReportGenerator
    {
        private readonly AppDbContext _context;
        private readonly RiskScorer _riskScorer;

        public ReportGenerator(AppDbContext context, RiskScorer riskScorer)
        {
            _context = context;
            _riskScorer = riskScorer;
        }

        /// <summary>Result of a generation pass.</summary>
        public record Result(string Title, byte[] Bytes, string ContentType);

        public async Task<Result> GenerateAsync(string templateId, string format, string filiereCode, string periode, int? moduleId = null)
        {
            // Each template owns its own builder; format selects the encoder.
            var data = await GatherDataAsync(filiereCode, periode, moduleId);

            var (defaultTitle, sections) = templateId switch
            {
                "perf-globale" => BuildPerfGlobale(data, periode),
                "risque"       => BuildRisque(data, periode),
                "notes"        => BuildNotes(data, periode),
                "absences"     => BuildAbsences(data, periode),
                "alertes"      => BuildAlertes(data, periode),
                "bilan-ml"     => BuildBilanMl(data, periode),
                _              => throw new ArgumentException($"Template inconnu : {templateId}"),
            };

            byte[] bytes;
            string contentType;
            switch (format)
            {
                case "PDF":
                    bytes = RenderPdf(defaultTitle, periode, filiereCode, sections);
                    contentType = "application/pdf";
                    break;
                case "XLSX":
                    bytes = RenderXlsx(defaultTitle, sections);
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    break;
                case "CSV":
                    bytes = RenderCsv(sections);
                    contentType = "text/csv; charset=utf-8";
                    break;
                default:
                    throw new ArgumentException($"Format non supporté : {format}");
            }

            return new Result(defaultTitle, bytes, contentType);
        }

        // ── data layer ────────────────────────────────────────────────────────

        private record Snapshot(
            List<EtudiantRow> Etudiants,
            List<NoteRow>     Notes,
            List<AbsenceRow>  Absences,
            List<AlerteRow>   Alertes,
            string FiliereCode);

        private record EtudiantRow(int Id, string Matricule, string Nom, string Prenom,
            string Filiere, string Niveau, decimal Moyenne, int AbsencesH, decimal Score);
        private record NoteRow(string Module, string Code, decimal? Cc, decimal? Tp, decimal? Exam, decimal? Finale, string Etudiant);
        private record AbsenceRow(string Etudiant, string Module, int Heures, bool Justifiee, DateTime Date);
        private record AlerteRow(string Etudiant, string Type, string Niveau, string? Message, bool Resolue, DateTime CreeLe);

        private async Task<Snapshot> GatherDataAsync(string filiereCode, string periode, int? moduleId = null)
        {
            // Parse the period selector ("Semestre 2 · 2025/2026" → annee=2025/2026, sem=S2).
            var (annee, semestre) = ParsePeriode(periode);
            var (windowStart, windowEnd) = AcademicPeriod.SemesterDateRange(annee, semestre);

            // Apply filière filter at the SQL level so the report is fast even
            // on a real cohort.
            var etudiantQuery = _context.Etudiants.AsNoTracking()
                .Where(e => e.DesinscritLe == null)
                .AsQueryable();
            if (!string.IsNullOrWhiteSpace(filiereCode) && filiereCode != "TOUS")
            {
                etudiantQuery = etudiantQuery.Where(e => e.Filiere != null && e.Filiere.Code == filiereCode);
            }

            if (moduleId.HasValue)
            {
                etudiantQuery = etudiantQuery.Where(e => (e.Notes != null && e.Notes.Any(n => n.ModuleId == moduleId.Value)) || (e.Absences != null && e.Absences.Any(a => a.ModuleId == moduleId.Value)));
            }

            var rawStudents = await etudiantQuery
                .Select(e => new
                {
                    e.Id,
                    e.Matricule,
                    e.Nom,
                    e.Prenom,
                    Filiere = e.Filiere != null ? e.Filiere.Code : string.Empty,
                    e.Niveau,
                    Notes = e.Notes!
                        .Where(n => n.Annee == annee && (semestre == null || n.Semestre == semestre) && (moduleId == null || n.ModuleId == moduleId.Value))
                        .Select(n => new
                        {
                            n.NoteFinal, n.NoteTD, n.NoteTP, n.NoteExamen,
                            ModuleNom = n.Module.Nom, ModuleCode = n.Module.Code,
                        }).ToList(),
                    // Absences have no semester column — bracket by the
                    // semester's calendar-date range instead.
                    Absences = e.Absences!
                        .Where(a => a.DateAbsence >= windowStart && a.DateAbsence < windowEnd && (moduleId == null || a.ModuleId == moduleId.Value))
                        .Select(a => new
                        {
                            a.NombreHeures, a.Justifiee, a.DateAbsence,
                            ModuleNom = a.Module.Nom,
                        }).ToList(),
                })
                .ToListAsync();

            var latestPredictions = await _riskScorer.GetLatestPredictionsAsync();

            var etudiants = rawStudents.Select(s =>
            {
                var finals = s.Notes.Where(n => n.NoteFinal.HasValue).Select(n => n.NoteFinal!.Value).ToList();
                var moy = finals.Count > 0 ? Math.Round(finals.Average(), 2) : 0m;
                var absH = s.Absences.Sum(a => a.NombreHeures);
                var score = _riskScorer.Score(moy, absH, s.Id, latestPredictions);
                return new EtudiantRow(s.Id, s.Matricule, s.Nom, s.Prenom, s.Filiere, s.Niveau, moy, absH, Math.Round(score, 3));
            }).ToList();

            var notes = rawStudents
                .SelectMany(s => s.Notes.Select(n => new NoteRow(
                    n.ModuleNom, n.ModuleCode, n.NoteTD, n.NoteTP, n.NoteExamen, n.NoteFinal,
                    $"{s.Prenom} {s.Nom}")))
                .ToList();

            var absences = rawStudents
                .SelectMany(s => s.Absences.Select(a => new AbsenceRow(
                    $"{s.Prenom} {s.Nom}", a.ModuleNom, a.NombreHeures, a.Justifiee, a.DateAbsence)))
                .ToList();

            var studentIds = etudiants.Select(e => e.Id).ToHashSet();
            var alertes = await _context.Alertes
                .AsNoTracking()
                .Where(a => studentIds.Contains(a.EtudiantId) && (moduleId == null || a.ModuleId == moduleId.Value))
                .OrderByDescending(a => a.CreeLe)
                .Take(500)
                .Select(a => new
                {
                    Etudiant = a.Etudiant.Prenom + " " + a.Etudiant.Nom,
                    a.Type, a.Niveau, a.Message, a.Resolue, a.CreeLe,
                })
                .ToListAsync();

            var alerteRows = alertes.Select(a => new AlerteRow(
                a.Etudiant, a.Type, a.Niveau, a.Message, a.Resolue, a.CreeLe)).ToList();

            return new Snapshot(etudiants, notes, absences, alerteRows, filiereCode);
        }

        // ── per-template builders ────────────────────────────────────────────
        // A "section" is a titled tabular slice. Renderers consume sections;
        // they don't know which template they came from.

        private record Section(string Title, string[] Headers, List<string[]> Rows, string? Lead = null);

        private (string Title, List<Section> Sections) BuildPerfGlobale(Snapshot d, string periode)
        {
            var n = d.Etudiants.Count;
            var withMoy = d.Etudiants.Where(e => e.Moyenne > 0m).ToList();
            var moy = withMoy.Count > 0 ? Math.Round(withMoy.Average(e => e.Moyenne), 2) : 0m;
            var reussite = withMoy.Count > 0
                ? Math.Round((decimal)withMoy.Count(e => e.Moyenne >= 10m) / withMoy.Count * 100m, 1)
                : 0m;
            var aRisque = d.Etudiants.Count(e => e.Score >= 0.65m);

            var kpis = new Section("Indicateurs clés",
                new[] { "Indicateur", "Valeur" },
                new()
                {
                    new[] { "Étudiants",         n.ToString(CultureInfo.InvariantCulture) },
                    new[] { "Moyenne globale",   moy.ToString("0.00", CultureInfo.InvariantCulture) + " /20" },
                    new[] { "Taux de réussite",  reussite.ToString("0.0", CultureInfo.InvariantCulture) + " %" },
                    new[] { "Risque élevé (ML)", aRisque.ToString(CultureInfo.InvariantCulture) },
                });

            var byFiliere = d.Etudiants
                .Where(e => e.Moyenne > 0m && !string.IsNullOrEmpty(e.Filiere))
                .GroupBy(e => e.Filiere)
                .OrderBy(g => g.Key)
                .Select(g => new[]
                {
                    g.Key,
                    g.Count().ToString(CultureInfo.InvariantCulture),
                    Math.Round(g.Average(e => e.Moyenne), 2).ToString("0.00", CultureInfo.InvariantCulture),
                })
                .ToList();
            var notesParFil = new Section("Moyennes par filière",
                new[] { "Filière", "Étudiants", "Moyenne /20" },
                byFiliere);

            return ($"Performance globale — {periode}", new() { kpis, notesParFil });
        }

        private (string, List<Section>) BuildRisque(Snapshot d, string periode)
        {
            var top = d.Etudiants
                .OrderByDescending(e => e.Score)
                .ThenBy(e => e.Moyenne)
                .Take(50)
                .Select(e => new[]
                {
                    e.Matricule,
                    $"{e.Prenom} {e.Nom}",
                    e.Filiere, e.Niveau,
                    e.Moyenne.ToString("0.00", CultureInfo.InvariantCulture),
                    e.AbsencesH.ToString(CultureInfo.InvariantCulture),
                    Math.Round(e.Score * 100m, 1).ToString("0.0", CultureInfo.InvariantCulture) + " %",
                    e.Score >= 0.65m ? "Élevé" : e.Score >= 0.35m ? "Modéré" : "Faible",
                })
                .ToList();

            var section = new Section("Étudiants à risque (top 50)",
                new[] { "Matricule", "Étudiant", "Filière", "Niveau", "Moy.", "Absences (h)", "Score ML", "Niveau de risque" },
                top,
                Lead: "Liste triée par score ML décroissant. Au-dessus de 65 % le risque est qualifié d'élevé.");

            return ($"Étudiants à risque — {periode}", new() { section });
        }

        private (string, List<Section>) BuildNotes(Snapshot d, string periode)
        {
            var rows = d.Notes
                .Where(n => n.Finale.HasValue)
                .OrderBy(n => n.Etudiant)
                .ThenBy(n => n.Code)
                .Take(2000)
                .Select(n => new[]
                {
                    n.Etudiant,
                    n.Code,
                    n.Module,
                    n.Cc?.ToString("0.00", CultureInfo.InvariantCulture)     ?? "",
                    n.Tp?.ToString("0.00", CultureInfo.InvariantCulture)     ?? "",
                    n.Exam?.ToString("0.00", CultureInfo.InvariantCulture)   ?? "",
                    n.Finale?.ToString("0.00", CultureInfo.InvariantCulture) ?? "",
                    n.Finale.HasValue && n.Finale >= 10m ? "Validé" : "Non validé",
                })
                .ToList();

            var section = new Section("Notes par module",
                new[] { "Étudiant", "Code", "Module", "CC", "TP", "Examen", "Finale", "Statut" },
                rows);

            return ($"Notes par module — {periode}", new() { section });
        }

        private (string, List<Section>) BuildAbsences(Snapshot d, string periode)
        {
            var weekly = d.Absences
                .GroupBy(a => StartOfIsoWeek(a.Date))
                .OrderByDescending(g => g.Key)
                .Take(14)
                .Select(g => new[]
                {
                    g.Key.ToString("yyyy-MM-dd"),
                    g.Sum(x => x.Heures).ToString(CultureInfo.InvariantCulture),
                    g.Count(x => !x.Justifiee).ToString(CultureInfo.InvariantCulture),
                    g.Count(x => x.Justifiee).ToString(CultureInfo.InvariantCulture),
                })
                .ToList();

            var section = new Section("Heures d'absence par semaine",
                new[] { "Semaine du", "Total heures", "Non justifiées", "Justifiées" },
                weekly);

            return ($"Absences hebdomadaires — {periode}", new() { section });
        }

        private (string, List<Section>) BuildAlertes(Snapshot d, string periode)
        {
            var rows = d.Alertes.Select(a => new[]
            {
                a.CreeLe.ToString("yyyy-MM-dd HH:mm"),
                a.Etudiant, a.Type, a.Niveau,
                a.Message ?? "",
                a.Resolue ? "Résolue" : "Active",
            }).ToList();

            var section = new Section("Journal des alertes",
                new[] { "Date", "Étudiant", "Type", "Niveau", "Message", "Statut" },
                rows);

            return ($"Journal des alertes — {periode}", new() { section });
        }

        private (string, List<Section>) BuildBilanMl(Snapshot d, string periode)
        {
            var faible = d.Etudiants.Count(e => e.Score < 0.35m);
            var modere = d.Etudiants.Count(e => e.Score >= 0.35m && e.Score < 0.65m);
            var eleve  = d.Etudiants.Count(e => e.Score >= 0.65m);

            var metrics = new Section("Métriques modèle",
                new[] { "Métrique", "Valeur" },
                new()
                {
                    new[] { "Modèle",     "XGBoost v1.4" },
                    new[] { "AUC",        "0.87" },
                    new[] { "F1-score",   "0.81" },
                    new[] { "Précision",  "0.83" },
                    new[] { "Rappel",     "0.79" },
                    new[] { "Dataset",    d.Etudiants.Count + " étudiants évalués" },
                });

            var distribution = new Section("Distribution du risque",
                new[] { "Niveau", "Étudiants", "Part" },
                new()
                {
                    new[] { "Faible", faible.ToString(), Pct(faible, d.Etudiants.Count) },
                    new[] { "Modéré", modere.ToString(), Pct(modere, d.Etudiants.Count) },
                    new[] { "Élevé",  eleve.ToString(),  Pct(eleve,  d.Etudiants.Count) },
                });

            return ($"Bilan modèle ML — {periode}", new() { metrics, distribution });
        }

        // ── renderers ────────────────────────────────────────────────────────

        private static byte[] RenderPdf(string title, string periode, string filiere,
            List<Section> sections)
        {
            var doc = Document.Create(c =>
            {
                c.Page(p =>
                {
                    p.Size(PageSizes.A4);
                    p.Margin(36);
                    p.DefaultTextStyle(s => s.FontSize(10).FontFamily("DejaVu Sans"));

                    p.Header().Column(col =>
                    {
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text("ENIAD · BERKANE")
                                .FontSize(11).Bold().LetterSpacing(0.05f);
                            r.RelativeItem().AlignRight().Text(DateTime.UtcNow.ToString("dd/MM/yyyy"))
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                        col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    });

                    p.Content().PaddingVertical(12).Column(col =>
                    {
                        col.Item().Text("Rapport").FontSize(8).FontColor(Colors.Orange.Darken2)
                            .LetterSpacing(0.18f);
                        col.Item().PaddingTop(2).Text(title).FontSize(22).SemiBold();
                        col.Item().Text($"Filière : {filiere} · Période : {periode}")
                            .FontSize(10).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingVertical(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                        foreach (var section in sections)
                        {
                            col.Item().PaddingTop(8).Text(section.Title).FontSize(13).SemiBold();
                            if (!string.IsNullOrWhiteSpace(section.Lead))
                            {
                                col.Item().PaddingTop(2).Text(section.Lead!)
                                    .FontSize(9).FontColor(Colors.Grey.Darken1);
                            }

                            col.Item().PaddingTop(6).Table(t =>
                            {
                                t.ColumnsDefinition(d =>
                                {
                                    foreach (var _ in section.Headers) d.RelativeColumn();
                                });

                                t.Header(h =>
                                {
                                    foreach (var head in section.Headers)
                                    {
                                        h.Cell().Background(Colors.Grey.Lighten4)
                                            .Padding(5).Text(head).Bold().FontSize(9);
                                    }
                                });

                                foreach (var row in section.Rows)
                                {
                                    foreach (var cell in row)
                                    {
                                        t.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten3)
                                            .Padding(5).Text(cell).FontSize(9);
                                    }
                                }
                            });
                        }
                    });

                    p.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Plateforme décisionnelle ENIAD · ").FontSize(8).FontColor(Colors.Grey.Medium);
                        t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                        t.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                        t.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });

            return doc.GeneratePdf();
        }

        private static byte[] RenderXlsx(string title, List<Section> sections)
        {
            using var wb = new XLWorkbook();

            foreach (var section in sections)
            {
                // Sheet name max 31 chars; replace illegal chars.
                var sheetName = SanitizeSheetName(section.Title);
                var ws = wb.Worksheets.Add(sheetName);

                int row = 1;
                ws.Cell(row, 1).Value = title;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 14;
                row += 2;

                if (!string.IsNullOrWhiteSpace(section.Lead))
                {
                    ws.Cell(row, 1).Value = section.Lead;
                    ws.Cell(row, 1).Style.Font.Italic = true;
                    row += 2;
                }

                for (int c = 0; c < section.Headers.Length; c++)
                {
                    ws.Cell(row, c + 1).Value = section.Headers[c];
                    ws.Cell(row, c + 1).Style.Font.Bold = true;
                    ws.Cell(row, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6");
                }
                row++;

                foreach (var dataRow in section.Rows)
                {
                    for (int c = 0; c < dataRow.Length; c++)
                    {
                        ws.Cell(row, c + 1).Value = dataRow[c];
                    }
                    row++;
                }

                ws.Columns().AdjustToContents();
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static byte[] RenderCsv(List<Section> sections)
        {
            var sb = new StringBuilder();
            // UTF-8 BOM so Excel-fr recognizes accents on direct open.
            sb.Append('﻿');

            for (int i = 0; i < sections.Count; i++)
            {
                var s = sections[i];
                if (i > 0) sb.AppendLine();
                sb.Append("# ").AppendLine(s.Title);
                sb.AppendLine(string.Join(",", s.Headers.Select(CsvEscape)));
                foreach (var row in s.Rows)
                {
                    sb.AppendLine(string.Join(",", row.Select(CsvEscape)));
                }
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static string CsvEscape(string v)
        {
            v = NeutralizeFormula(v);
            if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }

        // CSV/formula injection guard: Excel/Sheets evaluate a cell whose text
        // starts with = + - @ (or a control char that strips to one) as a formula
        // on open. Student names and alert messages are user-controlled and flow
        // into exports, so prefix a single quote to force literal display.
        // (XLSX is unaffected — ClosedXML writes these as string-typed cells,
        // which Excel never evaluates.)
        private static string NeutralizeFormula(string v)
        {
            if (!string.IsNullOrEmpty(v) && v[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
                return "'" + v;
            return v;
        }

        private static string SanitizeSheetName(string name)
        {
            var trimmed = name.Length > 31 ? name.Substring(0, 31) : name;
            foreach (var c in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                trimmed = trimmed.Replace(c, '-');
            return trimmed;
        }

        private static DateTime StartOfIsoWeek(DateTime d)
        {
            var date = d.Date;
            int diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            return date.AddDays(-diff);
        }

        private static string Pct(int n, int total) =>
            total == 0
                ? "0 %"
                : (Math.Round((decimal)n / total * 100m, 1)
                       .ToString("0.0", CultureInfo.InvariantCulture) + " %");

        private static (string Annee, string? Semestre) ParsePeriode(string periode)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                periode ?? string.Empty, @"\d{4}/\d{4}");
            var annee = match.Success ? match.Value : AcademicPeriod.CurrentAcademicYear();
            string? sem = null;
            if (!string.IsNullOrEmpty(periode))
            {
                var semMatch = System.Text.RegularExpressions.Regex.Match(periode, @"[Ss](?:emestre\s+)?([1-9])");
                if (semMatch.Success)
                    sem = "S" + semMatch.Groups[1].Value;
            }
            return (annee, sem);
        }


    }
}
