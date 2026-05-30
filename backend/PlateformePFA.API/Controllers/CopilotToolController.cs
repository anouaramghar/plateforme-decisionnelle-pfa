using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;

namespace PlateformePFA.API.Controllers
{
    /// <summary>
    /// Tool callback surface for the Copilot agent-service. The agent loop POSTs
    /// here once per tool the LLM decides to call. Every tool returns the
    /// {ok,data}|{ok,error,hint} envelope the LLM is trained to recover from.
    ///
    /// Two gates: the user's JWT ([Authorize], so tools run with the caller's
    /// role — L2) AND the agent-service shared secret (X-Internal-Token), because
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

        public CopilotToolController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _internalToken = config["AGENT_INTERNAL_TOKEN"] ?? string.Empty;
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
                _ => Ok(new { ok = false, error = $"unknown tool: {name}" }),
            };
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

            var moy = s.Moyenne ?? 0m;
            // Heuristic risk mirrors PredictionsController.GetSummary. A shared
            // RiskScorer is a later refactor; kept inline here so this first tool
            // stays self-contained.
            decimal risk = (moy < 10m ? 0.55m : moy < 12m ? 0.32m : 0.12m)
                         + (s.AbsencesH > 30 ? 0.22m : s.AbsencesH > 18 ? 0.12m : 0m);
            risk = Math.Min(0.98m, risk);
            var bucket = risk >= 0.65m ? "eleve" : risk >= 0.35m ? "modere" : "faible";

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

            var query = _db.Etudiants
                .AsNoTracking()
                .Where(e =>
                    (filiereFilter == null || (e.Filiere != null && e.Filiere.Code.ToUpper() == filiereFilter)) &&
                    (niveauFilter  == null || e.Niveau == niveauFilter))
                .Select(e => new
                {
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

            var result = rows
                .Select(s =>
                {
                    var moy = s.Moyenne ?? 0m;
                    decimal risk = (moy < 10m ? 0.55m : moy < 12m ? 0.32m : 0.12m)
                                 + (s.AbsencesH > 30 ? 0.22m : s.AbsencesH > 18 ? 0.12m : 0m);
                    risk = Math.Min(0.98m, risk);
                    var bucket = risk >= 0.65m ? "eleve" : risk >= 0.35m ? "modere" : "faible";
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

            // Risk factor weights — mirrors StudentDrawer in frontend (Students.tsx:486-504)
            var wMoy  = moy  < 10m ? 0.82m : moy  < 12m ? 0.42m : 0.12m;
            var wAbs  = Math.Min(0.95m, abs / 30.0m);
            var wMod  = modTotal > 0 ? 1.0m - (modVal / (decimal)modTotal) : 0m;
            const decimal wTend = 0.34m; // TODO: compute real semestral trend

            // Score: use ML if available, else heuristic
            decimal score;
            string scoreSource;
            if (s.MlScore.HasValue)
            {
                score = s.MlScore.Value;
                scoreSource = "ml";
            }
            else
            {
                decimal h = (moy < 10m ? 0.55m : moy < 12m ? 0.32m : 0.12m)
                          + (abs > 30 ? 0.22m : abs > 18 ? 0.12m : 0m);
                score = Math.Min(0.98m, h);
                scoreSource = "heuristic";
            }
            var risque = score >= 0.65m ? "eleve" : score >= 0.35m ? "modere" : "faible";

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
                    score_source  = scoreSource,
                    risque,
                    risk_factors  = new[]
                    {
                        new { factor = "Moyenne basse",         weight = Math.Round(wMoy, 3),  negative = moy < 12m  },
                        new { factor = "Absences élevées",       weight = Math.Round(wAbs, 3),  negative = abs > 15   },
                        new { factor = "Modules non validés",    weight = Math.Round(wMod, 3),  negative = modTotal > 0 && modVal < Math.Ceiling(modTotal / 2.0) },
                        new { factor = "Tendance trimestrielle", weight = Math.Round(wTend, 3), negative = false },
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
