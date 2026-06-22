using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.API.Models;
using PlateformePFA.Tests.Fixtures;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class NotesControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public NotesControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Fact]
    public async Task Upsert_creates_new_natural_key()
    {
        var client = await CreateAdminClientAsync();
        using var seedContext = _factory.CreateContext();
        var studentId = seedContext.Etudiants.Single(e => e.Matricule == "E10001").Id;
        var moduleId = seedContext.Modules.Single(m => m.Code == "GI01").Id;

        var response = await client.PutAsJsonAsync("/api/notes/upsert", new
        {
            etudiantId = studentId,
            moduleId,
            noteFinal = 9m,
            annee = "2026/2027",
            semestre = "S1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var verifyContext = _factory.CreateContext();
        verifyContext.Notes.Count(n =>
            n.EtudiantId == studentId && n.ModuleId == moduleId &&
            n.Annee == "2026/2027" && n.Semestre == "S1").Should().Be(1);
    }

    [Fact]
    public async Task Upsert_updates_matching_natural_key_without_duplicate()
    {
        var client = await CreateAdminClientAsync();
        using var seedContext = _factory.CreateContext();
        var studentId = seedContext.Etudiants.Single(e => e.Matricule == "E10001").Id;
        var moduleId = seedContext.Modules.Single(m => m.Code == "GI01").Id;

        var response = await client.PutAsJsonAsync("/api/notes/upsert", new
        {
            etudiantId = studentId,
            moduleId,
            noteFinal = 15m,
            annee = "2025/2026",
            semestre = "S2",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var verifyContext = _factory.CreateContext();
        var matching = verifyContext.Notes.Where(n =>
            n.EtudiantId == studentId && n.ModuleId == moduleId &&
            n.Annee == "2025/2026" && n.Semestre == "S2").ToList();
        matching.Should().ContainSingle();
        matching.Single().NoteFinal.Should().Be(15m);
    }

    [Fact]
    public async Task Upsert_rejects_withdrawn_student()
    {
        int studentId;
        int moduleId;
        using (var context = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(context);
            var withdrawn = new Etudiant
            {
                Matricule = "E-WITHDRAWN-NOTE",
                Nom = "Retire",
                Prenom = "Note",
                FiliereId = seeded.Filiere.Id,
                Niveau = "CI1",
                Annee = "2025/2026",
                DesinscritLe = DateTime.UtcNow.AddDays(-1),
            };
            context.Etudiants.Add(withdrawn);
            context.SaveChanges();
            studentId = withdrawn.Id;
            moduleId = seeded.Module.Id;
        }

        var client = await CreateAdminClientAsync();
        var response = await client.PutAsJsonAsync("/api/notes/upsert", new
        {
            etudiantId = studentId,
            moduleId,
            noteFinal = 12m,
            annee = "2025/2026",
            semestre = "S1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Teacher_cannot_create_note_for_other_module()
    {
        // 1. Seed second module & student & teacher
        int modAId;
        int modBId;
        int studentId;
        using (var ctx = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(ctx);
            modAId = seeded.Module.Id;
            studentId = seeded.Etudiant.Id;

            var modB = new Module
            {
                Code = "GI02", Nom = "Other Module",
                FiliereId = seeded.Filiere.Id, Niveau = "CI1",
                Coefficient = 4m, Semestre = "S1",
            };
            ctx.Modules.Add(modB);
            ctx.SaveChanges();
            modBId = modB.Id;

            SeedTeacher("teacherA@eniad.ma", "TeacherPass123!", modAId);
        }

        var client = await CreateTeacherClientAsync("teacherA@eniad.ma", "TeacherPass123!");

        // Try to create note for modB (other module)
        var response = await client.PostAsJsonAsync("/api/notes", new
        {
            etudiantId = studentId,
            moduleId = modBId,
            noteFinal = 12m,
            annee = "2025/2026",
            semestre = "S2",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_cannot_update_note_for_other_module()
    {
        int modAId;
        int modBId;
        int noteId;
        using (var ctx = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(ctx);
            modAId = seeded.Module.Id;

            var modB = new Module
            {
                Code = "GI02", Nom = "Other Module",
                FiliereId = seeded.Filiere.Id, Niveau = "CI1",
                Coefficient = 4m, Semestre = "S1",
            };
            ctx.Modules.Add(modB);
            ctx.SaveChanges();
            modBId = modB.Id;

            var otherNote = new Note
            {
                EtudiantId = seeded.Etudiant.Id,
                ModuleId = modBId,
                NoteFinal = 10m,
                Annee = "2025/2026",
                Semestre = "S2",
            };
            ctx.Notes.Add(otherNote);
            ctx.SaveChanges();
            noteId = otherNote.Id;

            SeedTeacher("teacherA2@eniad.ma", "TeacherPass123!", modAId);
        }

        var client = await CreateTeacherClientAsync("teacherA2@eniad.ma", "TeacherPass123!");

        var response = await client.PutAsJsonAsync($"/api/notes/{noteId}", new
        {
            noteFinal = 14m,
            annee = "2025/2026",
            semestre = "S2",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_cannot_upsert_note_for_other_module()
    {
        int modAId;
        int modBId;
        int studentId;
        using (var ctx = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(ctx);
            modAId = seeded.Module.Id;
            studentId = seeded.Etudiant.Id;

            var modB = new Module
            {
                Code = "GI02", Nom = "Other Module",
                FiliereId = seeded.Filiere.Id, Niveau = "CI1",
                Coefficient = 4m, Semestre = "S1",
            };
            ctx.Modules.Add(modB);
            ctx.SaveChanges();
            modBId = modB.Id;

            SeedTeacher("teacherA3@eniad.ma", "TeacherPass123!", modAId);
        }

        var client = await CreateTeacherClientAsync("teacherA3@eniad.ma", "TeacherPass123!");

        var response = await client.PutAsJsonAsync("/api/notes/upsert", new
        {
            etudiantId = studentId,
            moduleId = modBId,
            noteFinal = 14m,
            annee = "2025/2026",
            semestre = "S2",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_query_endpoints_only_return_assigned_module_notes()
    {
        int modAId;
        int modBId;
        int studentId;
        using (var ctx = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(ctx);
            modAId = seeded.Module.Id;
            studentId = seeded.Etudiant.Id;

            var modB = new Module
            {
                Code = "GI02", Nom = "Other Module",
                FiliereId = seeded.Filiere.Id, Niveau = "CI1",
                Coefficient = 4m, Semestre = "S1",
            };
            ctx.Modules.Add(modB);
            ctx.SaveChanges();
            modBId = modB.Id;

            // Seed note for Mod B as well
            ctx.Notes.Add(new Note
            {
                EtudiantId = studentId,
                ModuleId = modBId,
                NoteFinal = 15m,
                Annee = "2025/2026",
                Semestre = "S2",
            });
            ctx.SaveChanges();

            SeedTeacher("teacherA4@eniad.ma", "TeacherPass123!", modAId);
        }

        var client = await CreateTeacherClientAsync("teacherA4@eniad.ma", "TeacherPass123!");

        // 1. GetNotes list
        var listResponse = await client.GetAsync("/api/notes");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult = await listResponse.Content.ReadFromJsonAsync<PlateformePFA.API.DTOs.Common.PaginatedResult<Note>>();
        listResult!.Items.Should().OnlyContain(n => n.ModuleId == modAId);

        // 2. GetNotesByEtudiant
        var etuResponse = await client.GetAsync($"/api/notes/etudiant/{studentId}");
        etuResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var etuNotes = await etuResponse.Content.ReadFromJsonAsync<List<Note>>();
        etuNotes.Should().OnlyContain(n => n.ModuleId == modAId);

        // 3. GetNotesByModule for other module B
        var modBResponse = await client.GetAsync($"/api/notes/module/{modBId}");
        modBResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private void SeedTeacher(string email, string password, int moduleId)
    {
        using var ctx = _factory.CreateContext();
        if (!ctx.Utilisateurs.Any(u => u.Email == email))
        {
            ctx.Utilisateurs.Add(new Utilisateur
            {
                Email = email,
                Nom = "Teacher",
                Prenom = "Test",
                MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "Enseignant",
                ModuleId = moduleId,
                EstActif = true,
            });
            ctx.SaveChanges();
        }
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> CreateTeacherClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetTokenAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
