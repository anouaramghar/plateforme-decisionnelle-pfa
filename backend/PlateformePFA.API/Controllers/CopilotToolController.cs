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
                "get_student"  => Ok(await GetStudentAsync(body.Args, ct)),
                "list_at_risk" => Ok(await ListAtRiskAsync(body.Args, ct)),
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
            if (args.ValueKind != JsonValueKind.Object ||
                !args.TryGetProperty("threshold", out var threshEl) ||
                threshEl.ValueKind != JsonValueKind.Number ||
                !threshEl.TryGetDouble(out var threshDouble) ||
                threshDouble < 0.0 || threshDouble > 1.0)
            {
                return new { ok = false, error = "threshold must be a number between 0.0 and 1.0" };
            }
            var threshold = (decimal)threshDouble;

            string? filiereFilter = null;
            if (args.TryGetProperty("filiere", out var filEl) &&
                filEl.ValueKind == JsonValueKind.String)
            {
                filiereFilter = filEl.GetString()?.Trim().ToUpperInvariant();
            }

            string? niveauFilter = null;
            if (args.TryGetProperty("niveau", out var nivEl) &&
                nivEl.ValueKind == JsonValueKind.String)
            {
                niveauFilter = nivEl.GetString()?.Trim();
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

        private static bool FixedTimeEquals(string a, string b) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    }
}
