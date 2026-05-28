# ENIAD Copilot — Phase 1.B: Agent Loop + First Read Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the P1.A chat-only proxy into a real **multi-iteration tool-calling agent**: the LLM can call one Tier-1 read tool (`get_student`), the agent-service executes it by calling back into the backend (JWT forwarded), feeds the result back to the model, and loops until the model produces a final answer — all streamed as typed SSE events. Proven end-to-end with `curl`. No UI yet (that is the next P1.B slice), no Tier-2/3 tools, no L1/L4/L7 guards (those are later P1.B slices).

**Architecture:** `agent-service` gains a tool registry (`tools.py`, role-filtered), per-tool pydantic arg validation (`tool_args.py` = safety layer **L3**), a backend callback client (`tool_executor.py`), and a rewritten `agent.py` loop that drives NIM with `tools=[...]`, handles `tool_calls`, and bounds iterations (safety layer **L6**). The backend gains `POST /api/copilot/tool/{name}` (`CopilotToolController`) — gated by the user JWT (**L2**) *and* the agent-service shared secret — which dispatches to the `get_student` handler. agent-service reaches the backend over the existing `pfa_internal_ml` network.

**Tech Stack:** Python 3.11 + FastAPI + `openai` SDK + `httpx` + pydantic 2 (agent-service); ASP.NET Core 8 + EF Core (backend); SSE over HTTP/1.1; pytest + xUnit. No new third-party dependencies.

**Reference spec:** [`docs/superpowers/specs/2026-05-26-eniad-copilot-design.md`](../specs/2026-05-26-eniad-copilot-design.md). This plan implements the agent loop in §4.1 (steps 7a–7d, chat + tool-call iterations), the `get_student` tool from §2.1, safety layers **L2** (§3.2), **L3** (§3.3), and **L6** (§3.6, the iteration bound), and the `tool_call` / `tool_result` SSE events from §4.2. L1/L4/L5/L7/L8, Tier-2/3 tools, `query_dw`, `list_at_risk`, and all frontend work are explicitly **out of scope** for this slice.

---

## File Structure

### agent-service (create)
- `agent-service/tools.py` — tool JSON-Schema registry + `tools_for_role(role)` (role filtering, spec §3.2 / open-Q 4)
- `agent-service/tool_args.py` — per-tool pydantic arg models + `validate_tool_args(name, raw)` (L3)
- `agent-service/tool_executor.py` — `ToolExecutor` that POSTs to the backend tool route with the forwarded JWT + internal token
- `agent-service/tests/test_tools.py`
- `agent-service/tests/test_tool_args.py`
- `agent-service/tests/test_tool_executor.py`

### agent-service (modify)
- `agent-service/config.py` — add `backend_internal_url` setting
- `agent-service/provider.py` — add `ScriptedLLMProvider` (multi-turn mock for loop tests)
- `agent-service/agent.py` — rewrite `run_stream` as a bounded tool-calling loop
- `agent-service/main.py` — build `httpx.AsyncClient` + `ToolExecutor` in lifespan; pass `settings` + `tool_caller` into `run_stream`
- `agent-service/tests/test_agent.py` — update to the new `run_stream` signature; add tool-loop tests

### backend (create)
- `backend/PlateformePFA.API/Controllers/CopilotToolController.cs` — `POST /api/copilot/tool/{name}` + `get_student` handler
- `backend/PlateformePFA.Tests/Controllers/CopilotToolControllerTests.cs`

### Infrastructure (modify)
- `docker-compose.yml` — add `BACKEND_INTERNAL_URL` to the `agent-service` env block
- `.env.example` — document `BACKEND_INTERNAL_URL`

---

## Conventions

- **agent-service:** snake_case, type-hinted Python 3.11. Tests live in `agent-service/tests/`, run with `python -m pytest tests/ -q` from inside `agent-service/`.
- **backend:** PascalCase C#. Copilot subsystem types are English. Tests run with `dotnet test` from `backend/PlateformePFA.Tests`.
- **Tool result envelope** (the LLM's "API contract", spec §2): every tool returns JSON `{"ok": true, "data": <obj>}` or `{"ok": false, "error": <str>, "hint"?: <str>}`. Logical failures (not-found, bad arg) are `ok:false` with HTTP **200** so the model can recover; only auth/transport failures are non-200.
- **Commits:** one per task, Conventional Commits: `feat(copilot): ...`, `test(copilot): ...`.
- **TDD:** failing test first, then minimal code, except infra tasks (compose/env) where a verification step replaces the test.

---

## Pre-flight

- [ ] **Step 0.1: Confirm branch + clean baseline**

```bash
cd /d/eniad/pfa/plateforme-decisionnelle-pfa
git status --short
git branch --show-current
```

Expected: branch `feat/copilot-p1a-foundations` (we continue on it), and the only untracked entries are the pre-existing `CDC.pdf`, `.understand-anything/`, `frontend/tsconfig.*.tsbuildinfo`, `test_endpoints.ps1`.

- [ ] **Step 0.2: Confirm both test suites are green before starting**

```bash
cd agent-service && python -m pytest tests/ -q && cd ..
cd backend/PlateformePFA.Tests && dotnet test --nologo && cd ../..
```

Expected: agent-service `10 passed, 1 skipped`; backend `Passed! ... Total: 13`.

---

## Task 1: Backend — `CopilotToolController` + `get_student`

Implements the backend half of the loop: a single tool route the agent-service calls back into, gated by both the user JWT (**L2**) and the agent-service shared secret.

**Files:**
- Create: `backend/PlateformePFA.API/Controllers/CopilotToolController.cs`
- Create: `backend/PlateformePFA.Tests/Controllers/CopilotToolControllerTests.cs`

- [ ] **Step 1.1: Write the failing tests**

Create `backend/PlateformePFA.Tests/Controllers/CopilotToolControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlateformePFA.API.Data;
using PlateformePFA.API.Models;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class CopilotToolControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public CopilotToolControllerTests(TestWebFactory factory) => _factory = factory;

    // The internal token the in-memory test config injects (see TestWebFactory).
    private const string InternalToken = "test-agent-token";

    private async Task<HttpClient> AuthedClientAsync()
    {
        _factory.SeedAdmin();
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private void SeedStudent(string matricule)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (db.Etudiants.Any(e => e.Matricule == matricule)) return;

        var filiere = new Filiere { Code = "GI", Intitule = "Génie Informatique" };
        db.Filieres.Add(filiere);
        db.SaveChanges();
        db.Etudiants.Add(new Etudiant
        {
            Matricule = matricule,
            Nom = "ENGAR",
            Prenom = "Ahmed",
            FiliereId = filiere.Id,
            Niveau = "CI2",
            Annee = "2025/2026",
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Tool_without_internal_token_returns_401_even_with_jwt()
    {
        var client = await AuthedClientAsync();
        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/get_student",
            new { args = new { matricule = "X" } });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_student_happy_path_returns_ok_envelope()
    {
        var client = await AuthedClientAsync();
        SeedStudent("20231042");
        client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/get_student",
            new { args = new { matricule = "20231042" } });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"ok\":true");
        body.Should().Contain("\"matricule\":\"20231042\"");
        body.Should().Contain("\"risque\":");
    }

    [Fact]
    public async Task Get_student_not_found_returns_ok_false()
    {
        var client = await AuthedClientAsync();
        client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/get_student",
            new { args = new { matricule = "NOPE-9999" } });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"ok\":false");
    }

    [Fact]
    public async Task Unknown_tool_returns_ok_false()
    {
        var client = await AuthedClientAsync();
        client.DefaultRequestHeaders.Add("X-Internal-Token", InternalToken);

        var res = await client.PostAsJsonAsync(
            "/api/copilot/tool/does_not_exist",
            new { args = new { } });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain("\"ok\":false");
    }
}
```

- [ ] **Step 1.2: Run the tests to verify they fail**

Run: `cd backend/PlateformePFA.Tests && dotnet test --filter CopilotToolControllerTests --nologo; cd ../..`
Expected: build fails / tests fail — `CopilotToolController` does not exist (404s, not the asserted bodies).

- [ ] **Step 1.3: Implement `CopilotToolController`**

Create `backend/PlateformePFA.API/Controllers/CopilotToolController.cs`:

```csharp
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
                "get_student" => Ok(await GetStudentAsync(body.Args, ct)),
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

        private static bool FixedTimeEquals(string a, string b) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
    }
}
```

- [ ] **Step 1.4: Run the tests to verify they pass**

Run: `cd backend/PlateformePFA.Tests && dotnet test --filter CopilotToolControllerTests --nologo; cd ../..`
Expected: `Passed! ... Total: 4`.

- [ ] **Step 1.5: Run the full backend suite (no regressions)**

Run: `cd backend/PlateformePFA.Tests && dotnet test --nologo; cd ../..`
Expected: `Passed! ... Total: 17` (13 existing + 4 new).

- [ ] **Step 1.6: Commit**

```bash
git add backend/PlateformePFA.API/Controllers/CopilotToolController.cs backend/PlateformePFA.Tests/Controllers/CopilotToolControllerTests.cs
git commit -m "feat(copilot): P1.B backend tool route + get_student (L2 JWT + internal-token gate)"
```

---

## Task 2: agent-service config — backend callback URL

**Files:**
- Modify: `agent-service/config.py`

- [ ] **Step 2.1: Add the `backend_internal_url` setting**

In `agent-service/config.py`, inside `class Settings`, immediately after the `dw_readonly_conn` field, add:

```python
    # Where agent-service reaches the backend to execute tools (L2 callback).
    # Resolves over pfa_internal_ml; never the public nginx host.
    backend_internal_url: str = Field(
        default="http://backend:8080", alias="BACKEND_INTERNAL_URL"
    )
```

- [ ] **Step 2.2: Verify settings still load (existing tests cover this)**

Run: `cd agent-service && python -m pytest tests/test_main.py -q && cd ..`
Expected: `3 passed` (health, internal-token, mock-stream tests still pass; the new field has a default so `Settings()` validates with no extra env).

- [ ] **Step 2.3: Commit**

```bash
git add agent-service/config.py
git commit -m "feat(copilot): agent-service BACKEND_INTERNAL_URL setting"
```

---

## Task 3: agent-service tool registry (`tools.py`)

The JSON-Schema definitions advertised to NIM, filtered by role (spec §3.2 / open-Q 4 — Responsables never even see Tier-3 tools; for this slice the one tool is allowed to both staff roles).

**Files:**
- Create: `agent-service/tools.py`
- Create: `agent-service/tests/test_tools.py`

- [ ] **Step 3.1: Write the failing test**

Create `agent-service/tests/test_tools.py`:

```python
"""Tool registry + role filtering."""
from __future__ import annotations


def test_get_student_tool_is_advertised_to_staff_roles():
    from tools import tools_for_role

    for role in ("Admin", "Responsable"):
        specs = tools_for_role(role)
        names = [s["function"]["name"] for s in specs]
        assert "get_student" in names


def test_unknown_role_gets_no_tools():
    from tools import tools_for_role

    assert tools_for_role("Etudiant") == []


def test_get_student_schema_shape():
    from tools import tools_for_role

    spec = next(
        s for s in tools_for_role("Admin")
        if s["function"]["name"] == "get_student"
    )
    assert spec["type"] == "function"
    params = spec["function"]["parameters"]
    assert params["required"] == ["matricule"]
    assert params["properties"]["matricule"]["type"] == "string"
    assert params["additionalProperties"] is False
```

- [ ] **Step 3.2: Run to verify it fails**

Run: `cd agent-service && python -m pytest tests/test_tools.py -q && cd ..`
Expected: `ModuleNotFoundError: No module named 'tools'`.

- [ ] **Step 3.3: Implement `agent-service/tools.py`**

```python
"""
Tool catalog advertised to the LLM (OpenAI function-calling schema).

P1.B slice: one Tier-1 read tool — get_student. Role filtering is real even
with one tool: agent-service only advertises tools the caller's role may use,
so the LLM is never tempted by capabilities it cannot exercise (spec §3.2).
"""
from __future__ import annotations

from typing import Any

GET_STUDENT_TOOL: dict[str, Any] = {
    "type": "function",
    "function": {
        "name": "get_student",
        "description": (
            "Récupère le profil d'un étudiant par son matricule exact : "
            "filière, niveau, moyenne générale, total d'absences (heures) et "
            "niveau de risque d'échec."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "matricule": {
                    "type": "string",
                    "description": "Matricule exact de l'étudiant, ex: 20231042",
                },
            },
            "required": ["matricule"],
            "additionalProperties": False,
        },
    },
}

# name -> (spec, allowed roles)
TOOL_REGISTRY: dict[str, dict[str, Any]] = {
    "get_student": {"spec": GET_STUDENT_TOOL, "roles": {"Admin", "Responsable"}},
}


def tools_for_role(role: str) -> list[dict[str, Any]]:
    """Return the OpenAI tool specs the given role is allowed to call."""
    return [
        entry["spec"]
        for entry in TOOL_REGISTRY.values()
        if role in entry["roles"]
    ]
```

- [ ] **Step 3.4: Run to verify it passes**

Run: `cd agent-service && python -m pytest tests/test_tools.py -q && cd ..`
Expected: `3 passed`.

- [ ] **Step 3.5: Commit**

```bash
git add agent-service/tools.py agent-service/tests/test_tools.py
git commit -m "feat(copilot): agent-service tool registry + role filtering (get_student)"
```

---

## Task 4: agent-service L3 arg validation (`tool_args.py`)

Pydantic model per tool. Rejects unknown/missing/oversized args before any backend call (spec §3.3). The LLM's raw `arguments` string is JSON; we parse + validate it here.

**Files:**
- Create: `agent-service/tool_args.py`
- Create: `agent-service/tests/test_tool_args.py`

- [ ] **Step 4.1: Write the failing test**

Create `agent-service/tests/test_tool_args.py`:

```python
"""L3 — per-tool argument validation."""
from __future__ import annotations


def test_valid_get_student_args():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args("get_student", {"matricule": "20231042"})
    assert ok is True
    assert result == {"matricule": "20231042"}


def test_missing_required_arg_rejected():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args("get_student", {})
    assert ok is False
    assert isinstance(result, str)  # an error message


def test_extra_arg_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args(
        "get_student", {"matricule": "x", "email": "a@b.ma"}
    )
    assert ok is False  # additionalProperties forbidden — no exfil via args


def test_oversized_matricule_rejected():
    from tool_args import validate_tool_args

    ok, _ = validate_tool_args("get_student", {"matricule": "x" * 50})
    assert ok is False


def test_unknown_tool_rejected():
    from tool_args import validate_tool_args

    ok, result = validate_tool_args("drop_table", {})
    assert ok is False
    assert "unknown tool" in result
```

- [ ] **Step 4.2: Run to verify it fails**

Run: `cd agent-service && python -m pytest tests/test_tool_args.py -q && cd ..`
Expected: `ModuleNotFoundError: No module named 'tool_args'`.

- [ ] **Step 4.3: Implement `agent-service/tool_args.py`**

```python
"""
L3 — tool argument validation. One pydantic model per tool. extra='forbid'
enforces the "no free-text recipients / no exfil via args" rule from spec §3.3:
the model rejects any field the schema didn't declare.
"""
from __future__ import annotations

from typing import Any

from pydantic import BaseModel, Field, ValidationError


class GetStudentArgs(BaseModel):
    model_config = {"extra": "forbid"}
    matricule: str = Field(min_length=1, max_length=20)


# name -> pydantic model
TOOL_ARG_MODELS: dict[str, type[BaseModel]] = {
    "get_student": GetStudentArgs,
}


def validate_tool_args(
    name: str, raw: dict[str, Any]
) -> tuple[bool, dict[str, Any] | str]:
    """
    Validate raw LLM-supplied args against the tool's model.

    Returns (True, cleaned_args_dict) on success, or (False, error_message) on
    failure — the loop relays the message back to the LLM so it can retry.
    """
    model = TOOL_ARG_MODELS.get(name)
    if model is None:
        return False, f"unknown tool: {name}"
    try:
        parsed = model(**raw)
    except ValidationError as exc:
        return False, f"invalid args for {name}: {exc.errors()[0]['msg']}"
    except TypeError as exc:  # raw not a mapping, etc.
        return False, f"invalid args for {name}: {exc}"
    return True, parsed.model_dump()
```

- [ ] **Step 4.4: Run to verify it passes**

Run: `cd agent-service && python -m pytest tests/test_tool_args.py -q && cd ..`
Expected: `5 passed`.

- [ ] **Step 4.5: Commit**

```bash
git add agent-service/tool_args.py agent-service/tests/test_tool_args.py
git commit -m "feat(copilot): agent-service L3 tool-arg validation (extra=forbid)"
```

---

## Task 5: agent-service backend callback (`tool_executor.py`)

POSTs the validated tool call to the backend route from Task 1, forwarding the user's JWT (L2) and the internal token. Maps HTTP failures into the `{ok:false}` envelope so the loop never crashes on a tool error (spec §3.9).

**Files:**
- Create: `agent-service/tool_executor.py`
- Create: `agent-service/tests/test_tool_executor.py`

- [ ] **Step 5.1: Write the failing test (uses httpx.MockTransport — no real network, no new dep)**

Create `agent-service/tests/test_tool_executor.py`:

```python
"""Backend tool callback client."""
from __future__ import annotations

import json

import httpx
import pytest


def _client(handler) -> httpx.AsyncClient:
    return httpx.AsyncClient(
        transport=httpx.MockTransport(handler),
        base_url="http://backend:8080",
    )


@pytest.mark.asyncio
async def test_execute_forwards_jwt_and_internal_token_and_returns_envelope():
    seen = {}

    def handler(request: httpx.Request) -> httpx.Response:
        seen["url"] = str(request.url)
        seen["auth"] = request.headers.get("Authorization")
        seen["internal"] = request.headers.get("X-Internal-Token")
        seen["body"] = json.loads(request.content)
        return httpx.Response(200, json={"ok": True, "data": {"matricule": "42"}})

    from tool_executor import ToolExecutor

    async with _client(handler) as c:
        ex = ToolExecutor(c, internal_token="sekret")
        result = await ex.execute("get_student", {"matricule": "42"}, jwt="jwt-abc")

    assert result == {"ok": True, "data": {"matricule": "42"}}
    assert seen["url"] == "http://backend:8080/api/copilot/tool/get_student"
    assert seen["auth"] == "Bearer jwt-abc"
    assert seen["internal"] == "sekret"
    assert seen["body"] == {"args": {"matricule": "42"}}


@pytest.mark.asyncio
async def test_execute_maps_403_to_forbidden_envelope():
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(403)

    from tool_executor import ToolExecutor

    async with _client(handler) as c:
        ex = ToolExecutor(c, internal_token="sekret")
        result = await ex.execute("get_student", {"matricule": "42"}, jwt="x")

    assert result == {"ok": False, "error": "forbidden"}


@pytest.mark.asyncio
async def test_execute_maps_transport_error_to_envelope():
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("backend down")

    from tool_executor import ToolExecutor

    async with _client(handler) as c:
        ex = ToolExecutor(c, internal_token="sekret")
        result = await ex.execute("get_student", {"matricule": "42"}, jwt="x")

    assert result["ok"] is False
    assert "transport" in result["error"]
```

- [ ] **Step 5.2: Run to verify it fails**

Run: `cd agent-service && python -m pytest tests/test_tool_executor.py -q && cd ..`
Expected: `ModuleNotFoundError: No module named 'tool_executor'`.

- [ ] **Step 5.3: Implement `agent-service/tool_executor.py`**

```python
"""
Executes a validated tool call by POSTing to the backend tool route, forwarding
the user's JWT (L2: the tool runs with the caller's role) and the agent-service
internal token. All failure modes collapse to the {ok:false} envelope so the
agent loop continues gracefully (spec §3.9).
"""
from __future__ import annotations

from typing import Any

import httpx


class ToolExecutor:
    def __init__(self, client: httpx.AsyncClient, internal_token: str):
        self._client = client
        self._internal_token = internal_token

    async def execute(
        self, name: str, args: dict[str, Any], *, jwt: str | None
    ) -> dict[str, Any]:
        headers = {"X-Internal-Token": self._internal_token}
        if jwt:
            headers["Authorization"] = f"Bearer {jwt}"
        try:
            resp = await self._client.post(
                f"/api/copilot/tool/{name}",
                json={"args": args},
                headers=headers,
            )
        except httpx.HTTPError as exc:
            return {"ok": False, "error": f"tool transport error: {type(exc).__name__}"}

        if resp.status_code == 403:
            return {"ok": False, "error": "forbidden"}
        if resp.status_code >= 400:
            return {"ok": False, "error": f"tool http {resp.status_code}"}
        try:
            return resp.json()
        except ValueError:
            return {"ok": False, "error": "tool returned non-JSON"}
```

- [ ] **Step 5.4: Run to verify it passes**

Run: `cd agent-service && python -m pytest tests/test_tool_executor.py -q && cd ..`
Expected: `3 passed`.

- [ ] **Step 5.5: Commit**

```bash
git add agent-service/tool_executor.py agent-service/tests/test_tool_executor.py
git commit -m "feat(copilot): agent-service backend tool callback (JWT + internal token)"
```

---

## Task 6: agent-service — `ScriptedLLMProvider` for multi-turn loop tests

`MockLLMProvider` is single-shot (text only). The loop test needs a provider that returns a `tool_call` on the first `chat_stream` call and a final text answer on the second. Add a new provider rather than changing the existing one (keeps current tests intact).

**Files:**
- Modify: `agent-service/provider.py`
- Modify: `agent-service/tests/test_provider.py`

- [ ] **Step 6.1: Write the failing test**

Append to `agent-service/tests/test_provider.py`:

```python
@pytest.mark.asyncio
async def test_scripted_provider_replays_turns_in_order():
    from provider import ScriptedLLMProvider

    p = ScriptedLLMProvider(turns=[
        {"tool_call": {"id": "c1", "name": "get_student",
                       "arguments": '{"matricule": "42"}'}},
        {"text": ["Voici ", "le résumé."]},
    ])

    # First call -> a tool_call delta then finish 'tool_calls'.
    first = [d async for d in p.chat_stream(model="m", messages=[])]
    assert first[0].tool_call == {"id": "c1", "name": "get_student",
                                  "arguments": '{"matricule": "42"}'}
    assert first[-1].finish_reason == "tool_calls"

    # Second call -> text deltas then finish 'stop'.
    second = [d async for d in p.chat_stream(model="m", messages=[])]
    assert "".join(d.text or "" for d in second) == "Voici le résumé."
    assert second[-1].finish_reason == "stop"
```

- [ ] **Step 6.2: Run to verify it fails**

Run: `cd agent-service && python -m pytest tests/test_provider.py -q && cd ..`
Expected: `ImportError: cannot import name 'ScriptedLLMProvider'`.

- [ ] **Step 6.3: Implement `ScriptedLLMProvider`**

In `agent-service/provider.py`, immediately after the `MockLLMProvider` class (before `NIMProvider`), add:

```python
class ScriptedLLMProvider(LLMProvider):
    """
    Multi-turn test provider. Each chat_stream() call consumes the next scripted
    turn. A turn is either:
      {"text": ["chunk", ...]}                      -> finish_reason "stop"
      {"tool_call": {"id","name","arguments"}}      -> finish_reason "tool_calls"
    `arguments` is a JSON string, matching what NIMProvider emits.
    """

    def __init__(self, turns: list[dict[str, Any]]):
        self._turns = list(turns)
        self.last_tokens_in: int = 0
        self.last_tokens_out: int = 0

    async def chat_stream(
        self,
        *,
        model: str,
        messages: list[ChatMessage],
        tools: list[dict[str, Any]] | None = None,
        temperature: float = 0.0,
        max_tokens: int | None = None,
    ) -> AsyncIterator[StreamDelta]:
        if not self._turns:
            yield StreamDelta(text="(no more scripted turns)")
            yield StreamDelta(finish_reason="stop")
            return

        turn = self._turns.pop(0)
        if "tool_call" in turn:
            yield StreamDelta(tool_call=turn["tool_call"])
            yield StreamDelta(finish_reason="tool_calls")
        else:
            for chunk in turn.get("text", []):
                yield StreamDelta(text=chunk)
            yield StreamDelta(finish_reason="stop")
```

- [ ] **Step 6.4: Run to verify it passes (and nothing regressed)**

Run: `cd agent-service && python -m pytest tests/test_provider.py -q && cd ..`
Expected: `4 passed, 1 skipped` (3 prior provider tests + the new one; live NIM still skipped).

- [ ] **Step 6.5: Commit**

```bash
git add agent-service/provider.py agent-service/tests/test_provider.py
git commit -m "test(copilot): ScriptedLLMProvider for multi-turn agent-loop tests"
```

---

## Task 7: agent-service — rewrite `run_stream` as a tool-calling loop

The core of this phase. Replaces the chat-only `run_stream` with a bounded loop:
advertise role-filtered tools → stream tokens → on `tool_calls`, validate (L3),
execute (callback), feed results back → loop, up to `max_tool_iterations` (L6).

**Files:**
- Modify: `agent-service/agent.py`
- Modify: `agent-service/tests/test_agent.py`

- [ ] **Step 7.1: Rewrite the existing chat-only test + add loop tests**

Replace the entire contents of `agent-service/tests/test_agent.py` with:

```python
"""Agent loop — chat-only and tool-calling runs (MockLLMProvider / Scripted)."""
from __future__ import annotations

import pytest

from config import Settings
from provider import MockLLMProvider, ScriptedLLMProvider
from schemas import AgentRunRequest, ChatMessage, UserContext


def _settings(max_iters: int = 8) -> Settings:
    # Minimal valid Settings for the loop (router model + iteration bound).
    return Settings(
        AGENT_INTERNAL_TOKEN="t",
        NVIDIA_NIM_API_KEY="k",
        COPILOT_MAX_TOOL_ITERATIONS=max_iters,
    )


def _req(role: str = "Responsable", jwt: str | None = "jwt-x") -> AgentRunRequest:
    return AgentRunRequest(
        trace_id="t1",
        user_ctx=UserContext(user_id=1, role=role, jwt=jwt),
        messages=[ChatMessage(role="user", content="Bonjour")],
    )


async def _never_called(name, args, *, jwt):  # tool_caller for chat-only paths
    raise AssertionError("tool_caller should not be called")


@pytest.mark.asyncio
async def test_chat_only_run_yields_tokens_then_done():
    from agent import run_stream

    provider = MockLLMProvider(scripted_chunks=["Bon", "jour"], tokens_in=5, tokens_out=2)
    events = [
        ev async for ev in run_stream(
            _req(), provider=provider, settings=_settings(), tool_caller=_never_called
        )
    ]
    names = [n for n, _ in events]
    assert names == ["token", "token", "done"]
    assert events[-1][1]["tokens_in"] == 5


@pytest.mark.asyncio
async def test_tool_call_loop_executes_then_answers():
    from agent import run_stream

    provider = ScriptedLLMProvider(turns=[
        {"tool_call": {"id": "c1", "name": "get_student",
                       "arguments": '{"matricule": "20231042"}'}},
        {"text": ["Ahmed ", "est à risque modéré."]},
    ])

    calls = []

    async def tool_caller(name, args, *, jwt):
        calls.append((name, args, jwt))
        return {"ok": True, "data": {"matricule": "20231042", "risque": "modere"}}

    events = [
        ev async for ev in run_stream(
            _req(), provider=provider, settings=_settings(), tool_caller=tool_caller
        )
    ]
    names = [n for n, _ in events]

    assert "tool_call" in names
    assert "tool_result" in names
    assert names[-1] == "done"
    # Tool executed once with the validated args + forwarded JWT.
    assert calls == [("get_student", {"matricule": "20231042"}, "jwt-x")]
    # Final assistant text streamed after the tool result.
    text = "".join(p["text"] for n, p in events if n == "token")
    assert "risque modéré" in text


@pytest.mark.asyncio
async def test_invalid_tool_args_do_not_call_backend():
    from agent import run_stream

    provider = ScriptedLLMProvider(turns=[
        {"tool_call": {"id": "c1", "name": "get_student",
                       "arguments": '{"email": "a@b.ma"}'}},  # forbidden arg
        {"text": ["Désolé."]},
    ])

    async def tool_caller(name, args, *, jwt):
        raise AssertionError("backend must not be called on invalid args")

    events = [
        ev async for ev in run_stream(
            _req(), provider=provider, settings=_settings(), tool_caller=tool_caller
        )
    ]
    # A tool_result with ok=false (the L3 rejection) was emitted, loop continued.
    tool_results = [p for n, p in events if n == "tool_result"]
    assert tool_results and tool_results[0]["ok"] is False
    assert events[-1][0] == "done"


@pytest.mark.asyncio
async def test_loop_bound_emits_error_when_tools_never_stop():
    from agent import run_stream

    # Every turn asks for a tool again -> must hit the iteration cap (L6).
    turns = [
        {"tool_call": {"id": f"c{i}", "name": "get_student",
                       "arguments": '{"matricule": "42"}'}}
        for i in range(20)
    ]
    provider = ScriptedLLMProvider(turns=turns)

    async def tool_caller(name, args, *, jwt):
        return {"ok": True, "data": {}}

    events = [
        ev async for ev in run_stream(
            _req(), provider=provider, settings=_settings(max_iters=3),
            tool_caller=tool_caller
        )
    ]
    names = [n for n, _ in events]
    assert names[-1] == "error"
    # Exactly the capped number of tool executions.
    assert sum(1 for n in names if n == "tool_call") == 3
```

- [ ] **Step 7.2: Run to verify it fails**

Run: `cd agent-service && python -m pytest tests/test_agent.py -q && cd ..`
Expected: failures — `run_stream` does not accept `settings`/`tool_caller` and has no loop yet.

- [ ] **Step 7.3: Rewrite `agent-service/agent.py`**

Replace the entire contents of `agent-service/agent.py` with:

```python
"""
ENIAD Copilot agent loop (P1.B).

Bounded tool-calling loop:
  1. Advertise role-filtered tools to the model.
  2. Stream assistant tokens.
  3. On finish_reason == "tool_calls": for each call, validate args (L3),
     execute via the backend callback, feed the result back, and loop.
  4. On finish_reason == "stop": emit done.
  5. Iteration cap (L6) -> emit error if the model never stops.

Safety layers L1 (input guard), L4 (SQL), L5 (confirm), L7 (output guard),
and L8 (audit) are later P1.B slices.
"""
from __future__ import annotations

import json
import time
from collections.abc import AsyncIterator, Awaitable, Callable
from typing import Any

from config import Settings
from provider import LLMProvider
from schemas import (
    AgentRunRequest, ChatMessage, DoneEvent, ErrorEvent,
    ToolCallEvent, ToolResultEvent, TokenEvent,
)
from tool_args import validate_tool_args
from tools import tools_for_role

# (name, args, *, jwt) -> tool result envelope dict
ToolCaller = Callable[..., Awaitable[dict[str, Any]]]

_SYSTEM_PROMPT_FR = (
    "Tu es ENIAD Copilot, un assistant d'aide à la décision pour le personnel "
    "pédagogique de l'ENIAD. Réponds en français, de façon concise et factuelle. "
    "Utilise les outils fournis pour récupérer des données réelles ; n'invente "
    "jamais de chiffres. Si un outil renvoie une erreur, explique-la simplement."
)


def _build_system_prompt(req: AgentRunRequest) -> str:
    prompt = _SYSTEM_PROMPT_FR
    pc = req.user_ctx.page_context
    if pc:
        prompt += f"\n\nContexte de la page courante: {json.dumps(pc, ensure_ascii=False)}"
    return prompt


def _summarize(result: dict[str, Any]) -> str:
    """Short human label for the tool_result SSE chip."""
    if result.get("ok"):
        return "ok"
    return str(result.get("error", "error"))[:200]


async def run_stream(
    req: AgentRunRequest,
    *,
    provider: LLMProvider,
    settings: Settings,
    tool_caller: ToolCaller,
) -> AsyncIterator[tuple[str, dict[str, Any]]]:
    started = time.monotonic()
    tokens_in = 0
    tokens_out = 0

    # Working message list seeded with system prompt + the inbound history.
    messages: list[ChatMessage] = [
        ChatMessage(role="system", content=_build_system_prompt(req)),
        *req.messages,
    ]
    tools = tools_for_role(req.user_ctx.role)
    jwt = req.user_ctx.jwt

    try:
        for _ in range(settings.max_tool_iterations):
            assistant_text = ""
            pending_calls: list[dict[str, Any]] = []
            finish_reason: str | None = None

            async for delta in provider.chat_stream(
                model=settings.model_router, messages=messages, tools=tools,
            ):
                if delta.text:
                    assistant_text += delta.text
                    yield "token", TokenEvent(text=delta.text).model_dump()
                if delta.tool_call:
                    pending_calls.append(delta.tool_call)
                if delta.finish_reason:
                    finish_reason = delta.finish_reason

            tokens_in = getattr(provider, "last_tokens_in", 0) or 0
            tokens_out = getattr(provider, "last_tokens_out", 0) or 0

            if finish_reason != "tool_calls" or not pending_calls:
                # Model produced a final answer.
                yield "done", DoneEvent(
                    tokens_in=tokens_in, tokens_out=tokens_out,
                    latency_ms=int((time.monotonic() - started) * 1000),
                ).model_dump()
                return

            # Record the assistant turn that requested the tools (OpenAI shape).
            messages.append(ChatMessage(
                role="assistant",
                content=assistant_text or None,
                tool_calls=[
                    {
                        "id": c["id"],
                        "type": "function",
                        "function": {"name": c["name"], "arguments": c["arguments"]},
                    }
                    for c in pending_calls
                ],
            ))

            # Execute each requested tool, append its result as a tool message.
            for call in pending_calls:
                name = call["name"]
                call_id = call["id"]
                yield "tool_call", ToolCallEvent(
                    name=name, args={}, call_id=call_id,
                ).model_dump()

                try:
                    raw_args = json.loads(call["arguments"] or "{}")
                    valid, payload = validate_tool_args(name, raw_args)
                except json.JSONDecodeError:
                    valid, payload = False, "arguments were not valid JSON"

                if not valid:
                    result = {"ok": False, "error": payload}
                else:
                    result = await tool_caller(name, payload, jwt=jwt)

                yield "tool_result", ToolResultEvent(
                    name=name, call_id=call_id,
                    ok=bool(result.get("ok")), summary=_summarize(result),
                    error=None if result.get("ok") else str(result.get("error", "")),
                ).model_dump()

                messages.append(ChatMessage(
                    role="tool",
                    tool_call_id=call_id,
                    name=name,
                    content=json.dumps(result, ensure_ascii=False),
                ))
            # loop back to the model with the tool results appended

        # Fell out of the loop without a final answer -> L6 bound hit.
        yield "error", ErrorEvent(
            message="Limite d'itérations atteinte (COPILOT_MAX_TOOL_ITERATIONS).",
        ).model_dump()

    except Exception as exc:  # noqa: BLE001 — surface as an SSE error event
        yield "error", ErrorEvent(message=f"{type(exc).__name__}: {exc}").model_dump()
```

- [ ] **Step 7.4: Run to verify the agent tests pass**

Run: `cd agent-service && python -m pytest tests/test_agent.py -q && cd ..`
Expected: `4 passed`.

- [ ] **Step 7.5: Commit**

```bash
git add agent-service/agent.py agent-service/tests/test_agent.py
git commit -m "feat(copilot): agent-service tool-calling loop (L3 validate + L6 bound)"
```

---

## Task 8: agent-service — wire the executor into `main.py`

Build the backend `httpx.AsyncClient` + `ToolExecutor` once at startup and pass
`settings` + `tool_caller` into `run_stream`.

**Files:**
- Modify: `agent-service/main.py`

- [ ] **Step 8.1: Update imports + lifespan + the `/agent/run` handler**

In `agent-service/main.py`:

1. Add imports near the top, after the existing `from provider import NIMProvider`:

```python
import httpx

from tool_executor import ToolExecutor
```

2. In `lifespan`, after `app.state.provider = NIMProvider(...)`, add:

```python
    # Backend callback client for tool execution (L2). Rides the internal
    # network; 30s covers a normal API query.
    app.state.backend_client = httpx.AsyncClient(
        base_url=settings.backend_internal_url, timeout=30.0,
    )
    app.state.tool_executor = ToolExecutor(
        app.state.backend_client, internal_token=settings.internal_token,
    )
```

3. Change the shutdown portion of `lifespan` (the part after `yield`) to close the client:

```python
    yield
    await app.state.backend_client.aclose()
    logger.info("agent-service shutting down.")
```

4. Replace the body of `agent_run` down to (but not including) the `return StreamingResponse(...)` with:

```python
    provider = request.app.state.provider
    settings = request.app.state.settings
    executor: ToolExecutor = request.app.state.tool_executor

    async def gen():
        async for name, payload in run_stream(
            req,
            provider=provider,
            settings=settings,
            tool_caller=executor.execute,
        ):
            yield _sse_format(name, payload)
```

(Leave the `StreamingResponse(...)` return and its headers unchanged.)

- [ ] **Step 8.2: Run the full agent-service suite**

Run: `cd agent-service && python -m pytest tests/ -q && cd ..`
Expected: all green. `test_main.py` still passes because the mock-provider streaming test produces no tool calls, so `executor.execute` is never invoked (no real backend needed). Total `25 passed, 1 skipped`.

- [ ] **Step 8.3: Commit**

```bash
git add agent-service/main.py
git commit -m "feat(copilot): wire ToolExecutor + settings into /agent/run"
```

---

## Task 9: Infrastructure — `BACKEND_INTERNAL_URL`

**Files:**
- Modify: `docker-compose.yml`
- Modify: `.env.example`

- [ ] **Step 9.1: Add `BACKEND_INTERNAL_URL` to the agent-service env block**

In `docker-compose.yml`, inside the `agent-service` service `environment:` list, after the `COPILOT_DW_READONLY_CONN` line, add:

```yaml
      # How agent-service reaches the backend to execute tools (L2 callback).
      # Resolves over pfa_internal_ml — both services share that network.
      - BACKEND_INTERNAL_URL=${BACKEND_INTERNAL_URL:-http://backend:8080}
```

- [ ] **Step 9.2: Document it in `.env.example`**

In `.env.example`, immediately after the `AGENT_SERVICE_URL=http://agent-service:8001` line, add:

```bash

# How the agent-service reaches the backend to run tools (internal network).
BACKEND_INTERNAL_URL=http://backend:8080
```

- [ ] **Step 9.3: Validate compose still parses**

Run: `docker compose config --quiet && echo OK`
Expected: `OK` (no YAML/interpolation errors).

- [ ] **Step 9.4: Commit**

```bash
git add docker-compose.yml .env.example
git commit -m "feat(copilot): BACKEND_INTERNAL_URL for agent-service tool callbacks"
```

---

## Task 10: End-to-end verification (real stack)

No code — prove the loop runs through the full stack with a real NIM key.

- [ ] **Step 10.1: Rebuild + start the stack**

```bash
docker compose up -d --build
```

Wait until `docker compose ps` shows `db`, `backend`, `agent-service` all `healthy`
(db can take ~5 min on first boot). Confirm `.env` has a real `NVIDIA_NIM_API_KEY`
and the `READONLY_DB_PASSWORD` added in the P1.A review fixes.

- [ ] **Step 10.2: Find a known student matricule (or seed one via the API)**

```bash
docker exec -i $(docker compose ps -q db) bash -c \
  "SQLCMDPASSWORD=\$SA_PASSWORD /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -d PFA_DB -C -Q \"SET NOCOUNT ON; SELECT TOP 3 Matricule FROM Etudiants;\""
```

Note one matricule (e.g. `20231042`). If none exist, create a student through the
normal Étudiants API first (admin token).

- [ ] **Step 10.3: Drive a tool-calling turn through the public endpoint**

```bash
TOKEN=$(curl -s -X POST http://localhost/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_SEED_EMAIL\",\"motDePasse\":\"$ADMIN_SEED_PASSWORD\"}" \
  | python -c "import sys,json; print(json.load(sys.stdin)['token'])")

curl -fsN -X POST http://localhost/api/copilot/chat \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"message":"Donne-moi le profil de l etudiant 20231042"}'
```

Expected SSE stream contains, in order: zero-or-more `event: token`, then
`event: tool_call` with `"name": "get_student"`, then `event: tool_result`
with `"ok": true`, then more `event: token` (the model's French summary citing
the real moyenne/risque), then `event: done`.

- [ ] **Step 10.4: Confirm the direct tool route is NOT publicly callable**

```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost/api/copilot/tool/get_student \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"args":{"matricule":"20231042"}}'
```

Expected: `401` — a valid user JWT alone, without the internal token, cannot
invoke tools directly (L2 + internal-token gate).

- [ ] **Step 10.5: Final regression sweep**

```bash
cd agent-service && python -m pytest tests/ -q && cd ..
cd backend/PlateformePFA.Tests && dotnet test --nologo && cd ../..
```

Expected: agent-service `25 passed, 1 skipped`; backend `Total: 17`.

- [ ] **Step 10.6: (Optional) Record the smoke test**

If you captured useful output, append a "P1.B tool loop" section to
`agent-service/README.md` and commit:

```bash
git add agent-service/README.md
git commit -m "docs(copilot): record P1.B tool-loop smoke test"
```

---

## Self-Review Notes (for the executor)

- **Spec coverage:** §4.1 loop (token + tool_call iterations) → Task 7; `get_student` (§2.1) → Tasks 1+3; L2 (§3.2) → Task 1 gate + Task 5 JWT forwarding; L3 (§3.3) → Task 4; L6 iteration bound (§3.6) → Task 7; `tool_call`/`tool_result` SSE events (§4.2) → Task 7. Out of scope (stated up front): L1, L4, L5, L7, L8, Tier-2/3 tools, `query_dw`, `list_at_risk`, all frontend.
- **Type consistency:** the tool-call dict shape `{"id","name","arguments"}` is produced by `NIMProvider`/`ScriptedLLMProvider` and consumed identically in `agent.py`. `tool_caller` is called as `tool_caller(name, payload, jwt=jwt)`, matching `ToolExecutor.execute(self, name, args, *, jwt)`. The `{ok,data}|{ok,error,hint}` envelope is produced by `CopilotToolController` (Task 1) and consumed by `ToolExecutor`/`agent.py`.
- **Known follow-ups (next P1.B slices, not this plan):** shared `RiskScorer` to de-duplicate the heuristic in `CopilotToolController` vs `PredictionsController`; `query_dw` + L4; Tier-2 `draft_alert` + `/api/copilot/confirm` (L5); the React Copilot panel; L1/L7 safety guards; L8 audit writes.
```

