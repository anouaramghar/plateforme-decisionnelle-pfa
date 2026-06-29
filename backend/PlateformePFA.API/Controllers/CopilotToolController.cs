using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.Services;

namespace PlateformePFA.API.Controllers
{
    /// <summary>
    /// Tool callback surface for the Copilot runtime (copilot-runtime, CopilotKit).
    /// The agent POSTs here once per tool the LLM decides to call. Every tool
    /// returns the {ok,data}|{ok,error,hint} envelope the LLM is trained to recover from.
    ///
    /// Two gates: the user's JWT ([Authorize], so tools run with the caller's
    /// role — L2) AND the copilot-runtime shared secret (X-Internal-Token), because
    /// this route is reachable through nginx like any /api route and must not be
    /// callable by a logged-in user bypassing the agent loop.
    /// </summary>
    [ApiController]
    [Route("api/copilot/tool")]
    [Authorize(Roles = "Admin,Responsable")]
    public class CopilotToolController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly string _internalToken;
        private readonly RiskScorer _riskScorer;
        private readonly IConfiguration _config;
        private readonly ILogger<CopilotToolController> _logger;

        public CopilotToolController(
            AppDbContext db,
            IConfiguration config,
            RiskScorer riskScorer,
            ILogger<CopilotToolController> logger)
        {
            _db = db;
            _internalToken = config["AGENT_INTERNAL_TOKEN"] ?? string.Empty;
            _riskScorer = riskScorer;
            _config = config;
            _logger = logger;
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        public class ToolCallBody
        {
            public JsonElement Args { get; set; }
        }

        [HttpPost("{name}")]
        public async Task<IActionResult> InvokeAsync(
            string name, [FromBody] ToolCallBody body, CancellationToken ct)
        {
            var provided = Request.Headers["X-Internal-Token"].ToString();
            if (string.IsNullOrEmpty(_internalToken) ||
                string.IsNullOrEmpty(provided) ||
                !FixedTimeEquals(provided, _internalToken))
            {
                return Unauthorized();
            }

            return name switch
            {
                "get_student"   => Ok(await GetStudentAsync(body.Args, ct)),
                "list_at_risk"  => Ok(await ListAtRiskAsync(body.Args, ct)),
                "explain_risk"  => Ok(await ExplainRiskAsync(body.Args, ct)),
                "draft_alert"   => Ok(await DraftAlertAsync(body.Args, ct)),
                "query_dw"      => Ok(await QueryDwAsync(body.Args, ct)),
                _ => Ok(new { ok = false, error = $"unknown tool: {name}" }),
            };
        }

        private async Task<object> QueryDwAsync(JsonElement args, CancellationToken ct)
        {
            if (args.ValueKind != JsonValueKind.Object ||
                !args.TryGetProperty("question", out var qEl) ||
                qEl.ValueKind != JsonValueKind.String)
            {
                return new { ok = false, error = "missing required arg: question" };
            }
            var question = qEl.GetString()!;
            var q = question.ToLowerInvariant();

            try
            {
                var connStr = _config["COPILOT_DW_READONLY_CONN"];
                if (string.IsNullOrEmpty(connStr))
                    return new { ok = false, error = "no database connection (COPILOT_DW_READONLY_CONN is empty)" };

                await using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(ct);

                // ── Classify intent ───────────────────────────────────────────
                // 1. Count students
                if ((q.Contains("combien") || q.Contains("nombre") || q.Contains("total")) &&
                    (q.Contains("étudiant") || q.Contains("etudiant") || q.Contains("élève") || q.Contains("inscrit")))
                {
                    var count = await ExecScalarAsync<long>(conn,
                        "SELECT COUNT(*) FROM [PFA_DW].[dbo].[DimEtudiant]", ct);
                    return new { ok = true, data = new { question, result = $"{count} étudiants inscrits" } };
                }

                // 2. Average by filière
                if ((q.Contains("moyenne") && (q.Contains("filière") || q.Contains("filiere") || q.Contains("par filière"))) ||
                    q.Contains("par programme"))
                {
                    var rows = await QueryAsync(conn, @"
                        SELECT e.Filiere, ROUND(AVG(f.NoteFinale), 2) AS Moyenne
                        FROM [PFA_DW].[dbo].[FaitNotes] f
                        JOIN [PFA_DW].[dbo].[DimEtudiant] e ON f.EtudiantKey = e.EtudiantKey
                        WHERE f.NoteFinale IS NOT NULL
                        GROUP BY e.Filiere
                        ORDER BY Moyenne DESC", ct);
                    return new { ok = true, data = new { question, result = rows } };
                }

                // 3. Average by niveau (1A, 2A, 3A, …)
                if (q.Contains("moyenne") && (q.Contains("niveau") || q.Contains("année") || q.Contains("annee") || q.Contains("promo")))
                {
                    var rows = await QueryAsync(conn, @"
                        SELECT e.Niveau, ROUND(AVG(f.NoteFinale), 2) AS Moyenne,
                               COUNT(DISTINCT e.EtudiantKey) AS NbEtudiants
                        FROM [PFA_DW].[dbo].[FaitNotes] f
                        JOIN [PFA_DW].[dbo].[DimEtudiant] e ON f.EtudiantKey = e.EtudiantKey
                        WHERE f.NoteFinale IS NOT NULL
                        GROUP BY e.Niveau
                        ORDER BY e.Niveau", ct);
                    return new { ok = true, data = new { question, result = rows } };
                }

                // 4. Module failure rates
                if ((q.Contains("module") && (q.Contains("échec") || q.Contains("echec") || q.Contains("taux") || q.Contains("non validé") || q.Contains("difficile"))) ||
                    (q.Contains("taux") && q.Contains("échec")))
                {
                    var rows = await QueryAsync(conn, @"
                        SELECT m.Code, m.Nom AS NomModule, COUNT(f.Id) AS Total,
                               SUM(CASE WHEN f.NoteFinale IS NULL OR f.NoteFinale < 10 THEN 1 ELSE 0 END) AS Echecs,
                               ROUND(CAST(SUM(CASE WHEN f.NoteFinale IS NULL OR f.NoteFinale < 10 THEN 1 ELSE 0 END) AS FLOAT)
                                 / NULLIF(COUNT(f.Id), 0) * 100, 1) AS TauxEchec
                        FROM [PFA_DW].[dbo].[FaitNotes] f
                        JOIN [PFA_DW].[dbo].[DimModule] m ON f.ModuleKey = m.ModuleKey
                        GROUP BY m.Code, m.Nom
                        ORDER BY TauxEchec DESC", ct);
                    return new { ok = true, data = new { question, result = rows } };
                }

                // 5. Overall average
                if ((q.Contains("moyenne") && (q.Contains("générale") || q.Contains("generale") || q.Contains("globale") || q.Contains("global"))) ||
                    q.Contains("note moyenne") || q.Contains("note générale"))
                {
                    var avg = await ExecScalarAsync<double>(conn,
                        "SELECT ISNULL(AVG(CAST(NoteFinale AS FLOAT)), 0) FROM [PFA_DW].[dbo].[FaitNotes] WHERE NoteFinale IS NOT NULL", ct);
                    return new { ok = true, data = new { question, result = $"Moyenne générale : {avg:F2}/20" } };
                }

                // 6. Count modules
                if (q.Contains("nombre") && (q.Contains("module") || q.Contains("matière") || q.Contains("matiere")))
                {
                    var count = await ExecScalarAsync<long>(conn,
                        "SELECT COUNT(*) FROM [PFA_DW].[dbo].[DimModule]", ct);
                    return new { ok = true, data = new { question, result = $"{count} modules référencés" } };
                }

                // 7. Absences
                if (q.Contains("absence") || q.Contains("absent") || q.Contains("présence") || q.Contains("presences"))
                {
                    var total = await ExecScalarAsync<long>(conn,
                        "SELECT ISNULL(SUM(CAST(NbAbsences AS BIGINT)), 0) FROM [PFA_DW].[dbo].[FaitNotes]", ct);
                    return new { ok = true, data = new { question, result = $"Total des absences enregistrées : {total}h" } };
                }

                // 8. Top students (best average)
                if ((q.Contains("meilleur") || q.Contains("top") || q.Contains("premier") || q.Contains("classement") || q.Contains("podium")) &&
                    (q.Contains("étudiant") || q.Contains("elève") || q.Contains("note") || q.Contains("moyenne")))
                {
                    var rows = await QueryAsync(conn, @"
                        SELECT TOP 10 e.Matricule, e.Nom, e.Prenom, e.Filiere,
                               ROUND(AVG(f.NoteFinale), 2) AS MoyenneGenerale
                        FROM [PFA_DW].[dbo].[FaitNotes] f
                        JOIN [PFA_DW].[dbo].[DimEtudiant] e ON f.EtudiantKey = e.EtudiantKey
                        WHERE f.NoteFinale IS NOT NULL
                        GROUP BY e.Matricule, e.Nom, e.Prenom, e.Filiere
                        ORDER BY MoyenneGenerale DESC", ct);
                    return new { ok = true, data = new { question, result = rows } };
                }

                // 9. Students most at risk (lowest average)
                if ((q.Contains("risque") || q.Contains("en difficulté") || q.Contains("bas") || q.Contains("faible")) &&
                    (q.Contains("étudiant") || q.Contains("elève") || q.Contains("moyenne")))
                {
                    var rows = await QueryAsync(conn, @"
                        SELECT TOP 10 e.Matricule, e.Nom, e.Prenom, e.Filiere, e.Niveau,
                               ROUND(AVG(f.NoteFinale), 2) AS MoyenneGenerale,
                               ISNULL(SUM(f.NbAbsences), 0) AS TotalAbsences
                        FROM [PFA_DW].[dbo].[FaitNotes] f
                        JOIN [PFA_DW].[dbo].[DimEtudiant] e ON f.EtudiantKey = e.EtudiantKey
                        WHERE f.NoteFinale IS NOT NULL
                        GROUP BY e.Matricule, e.Nom, e.Prenom, e.Filiere, e.Niveau
                        ORDER BY MoyenneGenerale ASC", ct);
                    return new { ok = true, data = new { question, result = rows } };
                }

                // 10. Module list
                if ((q.Contains("liste") || q.Contains("quels")) && (q.Contains("module") || q.Contains("matière") || q.Contains("matiere")))
                {
                    var rows = await QueryAsync(conn,
                        "SELECT Code, Nom FROM [PFA_DW].[dbo].[DimModule] ORDER BY Nom", ct);
                    return new { ok = true, data = new { question, result = rows } };
                }

                // 11. Semester / period analysis
                if (q.Contains("semestre") || q.Contains("s1") || q.Contains("s2"))
                {
                    var rows = await QueryAsync(conn, @"
                        SELECT t.Semestre, t.Annee,
                               COUNT(DISTINCT f.EtudiantKey) AS NbEtudiants,
                               ROUND(AVG(f.NoteFinale), 2) AS MoyenneGenerale
                        FROM [PFA_DW].[dbo].[FaitNotes] f
                        JOIN [PFA_DW].[dbo].[DimTemps] t ON f.TempsKey = t.TempsKey
                        WHERE f.NoteFinale IS NOT NULL
                        GROUP BY t.Semestre, t.Annee
                        ORDER BY t.Annee DESC, t.Semestre", ct);
                    return new { ok = true, data = new { question, result = rows } };
                }

                // 12. Fallback: DW summary
                var summary = new
                {
                    Etudiants = await ExecScalarAsync<long>(conn,
                        "SELECT COUNT(*) FROM [PFA_DW].[dbo].[DimEtudiant]", ct),
                    Modules = await ExecScalarAsync<long>(conn,
                        "SELECT COUNT(*) FROM [PFA_DW].[dbo].[DimModule]", ct),
                    Enregistrements = await ExecScalarAsync<long>(conn,
                        "SELECT COUNT(*) FROM [PFA_DW].[dbo].[FaitNotes]", ct),
                    MoyenneGlobale = await ExecScalarAsync<double>(conn,
                        "SELECT ISNULL(AVG(CAST(NoteFinale AS FLOAT)), 0) FROM [PFA_DW].[dbo].[FaitNotes] WHERE NoteFinale IS NOT NULL", ct),
                };
                return new { ok = true, data = new { question, hint = "Question non reconnue. Voici un résumé du DW.", result = summary } };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Copilot DW query failed");
                return new { ok = false, error = "DW unavailable", hint = "Vérifiez que la base PFA_DW est accessible." };
            }
        }

        private static async Task<T> ExecScalarAsync<T>(SqlConnection conn, string sql, CancellationToken ct)
        {
            await using var cmd = new SqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync(ct);
            return (T)Convert.ChangeType(result, typeof(T));
        }

        private static async Task<List<Dictionary<string, object?>>> QueryAsync(SqlConnection conn, string sql, CancellationToken ct)
        {
            await using var cmd = new SqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = val;
                }
                rows.Add(row);
            }
            return rows;
        }

        private async Task<object> GetStudentAsync(JsonElement args, CancellationToken ct)
        {
            if (args.ValueKind != JsonValueKind.Object ||
                !args.TryGetProperty("matricule", out var matEl) ||
                matEl.ValueKind != JsonValueKind.String)
            {
                return new { ok = false, error = "missing required arg: matricule" };
            }
            var matricule = matEl.GetString()!;

            var s = await _db.Etudiants
                .AsNoTracking()
                .Where(e => e.Matricule == matricule)
                .Select(e => new
                {
                    e.Id,
                    e.Matricule,
                    e.Nom,
                    e.Prenom,
                    e.Niveau,
                    Filiere = e.Filiere != null ? e.Filiere.Code : "",
                    Moyenne = (decimal?)e.Notes!
                        .Where(n => n.NoteFinal.HasValue)
                        .Average(n => n.NoteFinal),
                    AbsencesH = (int?)e.Absences!.Sum(a => a.NombreHeures) ?? 0,
                })
                .FirstOrDefaultAsync(ct);

            if (s is null)
            {
                return new
                {
                    ok = false,
                    error = $"étudiant introuvable: {matricule}",
                    hint = "Vérifiez le matricule.",
                };
            }

            var latestPredictions = await _riskScorer.GetLatestPredictionsAsync();
            var moy = s.Moyenne ?? 0m;
            var risk = _riskScorer.Score(moy, s.AbsencesH, s.Id, latestPredictions);
            var bucket = RiskScorer.Bucket(risk);

            return new
            {
                ok = true,
                data = new
                {
                    matricule = s.Matricule,
                    nom = s.Nom,
                    prenom = s.Prenom,
                    filiere = s.Filiere,
                    niveau = s.Niveau,
                    moyenne_generale = Math.Round(moy, 2),
                    total_absences_h = s.AbsencesH,
                    risque = bucket,
                    score_risque = Math.Round(risk, 3),
                },
            };
        }

        private async Task<object> ListAtRiskAsync(JsonElement args, CancellationToken ct)
        {
            // Parse threshold — accept both JSON number and JSON string (LLM sometimes passes "0").
            double threshDouble = 0.0;
            if (!args.TryGetProperty("threshold", out var threshEl))
                return new { ok = false, error = "threshold is required" };
            if (threshEl.ValueKind == JsonValueKind.Number)
                threshEl.TryGetDouble(out threshDouble);
            else if (threshEl.ValueKind == JsonValueKind.String &&
                     double.TryParse(threshEl.GetString(), System.Globalization.NumberStyles.Any,
                                     System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                threshDouble = parsed;
            else
                return new { ok = false, error = "threshold must be a number between 0.0 and 1.0" };

            if (threshDouble < 0.0 || threshDouble > 1.0)
                return new { ok = false, error = "threshold must be a number between 0.0 and 1.0" };

            var threshold = (decimal)threshDouble;

            // Optional filters — treat JSON null, the string "null", or empty string as absent.
            static bool IsAbsent(string? v) =>
                v is null || string.IsNullOrWhiteSpace(v)
                          || v.Equals("null", StringComparison.OrdinalIgnoreCase)
                          || v.Equals("undefined", StringComparison.OrdinalIgnoreCase);

            string? filiereFilter = null;
            if (args.TryGetProperty("filiere", out var filEl) &&
                filEl.ValueKind == JsonValueKind.String)
            {
                var v = filEl.GetString()?.Trim().ToUpperInvariant();
                if (!IsAbsent(v)) filiereFilter = v;
            }

            string? niveauFilter = null;
            if (args.TryGetProperty("niveau", out var nivEl) &&
                nivEl.ValueKind == JsonValueKind.String)
            {
                var v = nivEl.GetString()?.Trim();
                if (!IsAbsent(v)) niveauFilter = v;
            }

            const int Cap = 50;

            // Risk is computed as a C# heuristic from (avg, absences); filtering by
            // exact score can't be pushed to SQL cleanly without duplicating the formula.
            // At ENIAD scale (single school, hundreds of students) materializing the
            // filtered set is acceptable — a SQL pre-filter on correlated aggregates
            // can't be verified against the in-memory EF Core test provider.
            var query = _db.Etudiants
                .AsNoTracking()
                .Where(e =>
                    (filiereFilter == null || (e.Filiere != null && e.Filiere.Code.ToUpper() == filiereFilter)) &&
                    (niveauFilter  == null || e.Niveau == niveauFilter))
                .Select(e => new
            {
                e.Id,
                e.Matricule,
                e.Nom,
                e.Prenom,
                e.Niveau,
                Filiere   = e.Filiere != null ? e.Filiere.Code : "",
                Moyenne   = (decimal?)e.Notes!
                    .Where(n => n.NoteFinal.HasValue)
                    .Average(n => n.NoteFinal),
                AbsencesH = (int?)e.Absences!.Sum(a => a.NombreHeures) ?? 0,
            });

            var rows = await query.ToListAsync(ct);
            var latestPredictions = await _riskScorer.GetLatestPredictionsAsync();

            var result = rows
                .Select(s =>
                {
                    var moy = s.Moyenne ?? 0m;
                    var risk = _riskScorer.Score(moy, s.AbsencesH, s.Id, latestPredictions);
                    var bucket = RiskScorer.Bucket(risk);
                    return new
                    {
                        matricule        = s.Matricule,
                        nom              = s.Nom,
                        prenom           = s.Prenom,
                        filiere          = s.Filiere,
                        niveau           = s.Niveau,
                        moyenne_generale = Math.Round(moy, 2),
                        total_absences_h = s.AbsencesH,
                        score_risque     = Math.Round(risk, 3),
                        risque           = bucket,
                    };
                })
                .Where(s => s.score_risque >= threshold)
                .OrderByDescending(s => s.score_risque)
                .ToList();

            var total      = result.Count;
            var capApplied = total > Cap;
            var students   = result.Take(Cap).ToList();

            return new
            {
                ok   = true,
                data = new { students, total, cap_applied = capApplied },
            };
        }

        private async Task<object> ExplainRiskAsync(JsonElement args, CancellationToken ct)
        {
            if (args.ValueKind != JsonValueKind.Object ||
                !args.TryGetProperty("matricule", out var matEl) ||
                matEl.ValueKind != JsonValueKind.String)
            {
                return new { ok = false, error = "missing required arg: matricule" };
            }
            var matricule = matEl.GetString()!;

            var s = await _db.Etudiants
                .AsNoTracking()
                .Where(e => e.Matricule == matricule)
                .Select(e => new
                {
                    e.Id,
                    e.Matricule,
                    e.Nom,
                    e.Prenom,
                    e.Niveau,
                    Filiere        = e.Filiere != null ? e.Filiere.Code : "",
                    Moyenne        = (decimal?)e.Notes!.Where(n => n.NoteFinal.HasValue).Average(n => n.NoteFinal),
                    AbsencesH      = (int?)e.Absences!.Sum(a => a.NombreHeures) ?? 0,
                    ModulesTotal   = e.Notes!.Select(n => n.ModuleId).Distinct().Count(),
                    ModulesValides = e.Notes!.Where(n => n.NoteFinal.HasValue && n.NoteFinal >= 10m)
                                             .Select(n => n.ModuleId).Distinct().Count(),
                    MlScore        = (decimal?)e.PredictionsML!
                                       .Where(p => p.TypeModele == "RisqueEchec" && p.ScoreRisque.HasValue)
                                       .OrderByDescending(p => p.CreeLe)
                                       .Select(p => p.ScoreRisque)
                                       .FirstOrDefault(),
                })
                .FirstOrDefaultAsync(ct);

            if (s is null)
            {
                return new { ok = false, error = $"étudiant introuvable: {matricule}", hint = "Vérifiez le matricule." };
            }

            var moy = s.Moyenne ?? 0m;
            var abs = s.AbsencesH;
            var modVal = s.ModulesValides;
            var modTotal = s.ModulesTotal;

            // Risk factor weights — reuse the heuristic score's step functions so the
            // displayed breakdown can never drift from the headline score.
            var wMoy  = RiskScorer.MoyenneWeight(moy);
            var wAbs  = RiskScorer.AbsenceWeight(abs);
            var wMod  = modTotal > 0 ? 1.0m - (modVal / (decimal)modTotal) : 0m;

            var wSum = wMoy + wAbs + wMod;
            if (wSum > 0m)
            {
                wMoy /= wSum;
                wAbs /= wSum;
                wMod /= wSum;
            }

            // Score: use ML if available, else heuristic
            var score = s.MlScore ?? RiskScorer.HeuristicScore(moy, abs);
            var risque = RiskScorer.Bucket(score);

            return new
            {
                ok = true,
                data = new
                {
                    matricule     = s.Matricule,
                    nom           = s.Nom,
                    prenom        = s.Prenom,
                    filiere       = s.Filiere,
                    niveau        = s.Niveau,
                    score_risque  = Math.Round(score, 3),
                    score_source  = s.MlScore.HasValue ? "ml" : "heuristic",
                    risque,
                    risk_factors  = new[]
                    {
                        new { factor = "Moyenne basse",      weight = Math.Round(wMoy, 3),  negative = moy < 12m  },
                        new { factor = "Absences élevées",    weight = Math.Round(wAbs, 3),  negative = abs > 18   },
                        new { factor = "Modules non validés", weight = Math.Round(wMod, 3),  negative = modTotal > 0 && modVal < Math.Ceiling(modTotal / 2.0) },
                    },
                    details = new
                    {
                        moyenne_generale  = Math.Round(moy, 2),
                        total_absences_h  = abs,
                        modules_valides   = modVal,
                        modules_total     = modTotal,
                    },
                },
            };
        }

        private async Task<object> DraftAlertAsync(JsonElement args, CancellationToken ct)
        {
            if (args.ValueKind != JsonValueKind.Object ||
                !args.TryGetProperty("matricule", out var matEl) ||
                matEl.ValueKind != JsonValueKind.String)
            {
                return new { ok = false, error = "missing required arg: matricule" };
            }
            var matricule = matEl.GetString()!;

            if (!args.TryGetProperty("severity", out var sevEl) ||
                sevEl.ValueKind != JsonValueKind.String)
            {
                return new { ok = false, error = "missing required arg: severity" };
            }
            var severity = sevEl.GetString()!;
            if (severity is not ("low" or "medium" or "high"))
            {
                return new { ok = false, error = "severity must be one of: low, medium, high" };
            }

            if (!args.TryGetProperty("message_fr", out var msgEl) ||
                msgEl.ValueKind != JsonValueKind.String)
            {
                return new { ok = false, error = "missing required arg: message_fr" };
            }
            var messageFr = msgEl.GetString()!;
            if (string.IsNullOrWhiteSpace(messageFr))
            {
                return new { ok = false, error = "message_fr must not be empty" };
            }

            var userId = GetUserId();
            if (userId is null)
            {
                return new { ok = false, error = "unauthorized: no user identity in token" };
            }

            var etudiant = await _db.Etudiants
                .AsNoTracking()
                .Where(e => e.Matricule == matricule)
                .Select(e => new { e.Id, e.Nom, e.Prenom })
                .FirstOrDefaultAsync(ct);

            if (etudiant is null)
            {
                return new { ok = false, error = $"étudiant introuvable: {matricule}", hint = "Vérifiez le matricule." };
            }

            var now = DateTime.UtcNow;
            var draft = new PlateformePFA.API.Models.AlertDraft
            {
                StudentId  = etudiant.Id,
                Severity   = severity,
                MessageFr  = messageFr,
                CreatedBy  = userId.Value,
                CreatedAt  = now,
                ExpiresAt  = now.AddMinutes(5),
                Status     = "pending_user_confirm",
            };
            _db.AlertDrafts.Add(draft);
            await _db.SaveChangesAsync(ct);

            return new
            {
                ok = true,
                data = new
                {
                    draft_id     = draft.Id,
                    student_name = $"{etudiant.Prenom} {etudiant.Nom}".Trim(),
                    matricule,
                    severity,
                    message      = messageFr,
                    expires_at   = draft.ExpiresAt.ToString("o"),
                },
            };
        }

        private static bool FixedTimeEquals(string a, string b) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    }
}
