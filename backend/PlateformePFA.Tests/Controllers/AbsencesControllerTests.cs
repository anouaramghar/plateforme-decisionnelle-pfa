using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PlateformePFA.API.Models;
using PlateformePFA.Tests.Fixtures;
using Xunit;

namespace PlateformePFA.Tests.Controllers;

public class AbsencesControllerTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public AbsencesControllerTests(TestWebFactory factory)
    {
        _factory = factory;
        _factory.SeedAdmin();
        using var ctx = _factory.CreateContext();
        SampleData.SeedOne(ctx);
    }

    [Fact]
    public async Task Teacher_cannot_create_absence_for_other_module()
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

            SeedTeacher("teacherA_abs1@eniad.ma", "TeacherPass123!", modAId);
        }

        var client = await CreateTeacherClientAsync("teacherA_abs1@eniad.ma", "TeacherPass123!");

        // Try to create absence for modB
        var response = await client.PostAsJsonAsync("/api/absences", new
        {
            etudiantId = studentId,
            moduleId = modBId,
            nombreHeures = 2,
            justifiee = false,
            dateAbsence = DateTime.UtcNow.AddDays(-2),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_cannot_update_absence_for_other_module()
    {
        int modAId;
        int modBId;
        int absenceId;
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

            var otherAbsence = new Absence
            {
                EtudiantId = seeded.Etudiant.Id,
                ModuleId = modBId,
                NombreHeures = 4,
                Justifiee = false,
                DateAbsence = DateTime.UtcNow.AddDays(-5),
            };
            ctx.Absences.Add(otherAbsence);
            ctx.SaveChanges();
            absenceId = otherAbsence.Id;

            SeedTeacher("teacherA_abs2@eniad.ma", "TeacherPass123!", modAId);
        }

        var client = await CreateTeacherClientAsync("teacherA_abs2@eniad.ma", "TeacherPass123!");

        var response = await client.PutAsJsonAsync($"/api/absences/{absenceId}", new
        {
            nombreHeures = 6,
            justifiee = true,
            dateAbsence = DateTime.UtcNow.AddDays(-5),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_query_endpoints_only_return_assigned_module_absences()
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

            // Seed an absence for Mod A and an absence for Mod B
            ctx.Absences.Add(new Absence
            {
                EtudiantId = studentId,
                ModuleId = modAId,
                NombreHeures = 2,
                DateAbsence = DateTime.UtcNow.AddDays(-1),
            });
            ctx.Absences.Add(new Absence
            {
                EtudiantId = studentId,
                ModuleId = modBId,
                NombreHeures = 4,
                DateAbsence = DateTime.UtcNow.AddDays(-2),
            });
            ctx.SaveChanges();

            SeedTeacher("teacherA_abs3@eniad.ma", "TeacherPass123!", modAId);
        }

        var client = await CreateTeacherClientAsync("teacherA_abs3@eniad.ma", "TeacherPass123!");

        // 1. GetAbsences list
        var listResponse = await client.GetAsync("/api/absences");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult = await listResponse.Content.ReadFromJsonAsync<PlateformePFA.API.DTOs.Common.PaginatedResult<Absence>>();
        listResult!.Items.Should().OnlyContain(a => a.ModuleId == modAId);

        // 2. GetAbsencesByEtudiant
        var etuResponse = await client.GetAsync($"/api/absences/etudiant/{studentId}");
        etuResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var etuAbs = await etuResponse.Content.ReadFromJsonAsync<List<Absence>>();
        etuAbs.Should().OnlyContain(a => a.ModuleId == modAId);

        // 3. GetAbsencesByModule for other module B
        var modBResponse = await client.GetAsync($"/api/absences/module/{modBId}");
        modBResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Absence_creation_fails_if_student_and_module_mismatched()
    {
        int studentId;
        int mismatchedModuleId;
        using (var ctx = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(ctx);
            studentId = seeded.Etudiant.Id;
            
            var otherFil = new Filiere { Code = "IA", Intitule = "Intelligence Artificielle" };
            ctx.Filieres.Add(otherFil);
            ctx.SaveChanges();

            var otherMod = new Module
            {
                Code = "IA01", Nom = "Machine Learning",
                FiliereId = otherFil.Id, Niveau = "CI1",
                Coefficient = 4m, Semestre = "S1"
            };
            ctx.Modules.Add(otherMod);
            ctx.SaveChanges();
            mismatchedModuleId = otherMod.Id;

            SeedTeacher("teacherA_abs1@eniad.ma", "TeacherPass123!", mismatchedModuleId);
        }

        var client = await CreateTeacherClientAsync("teacherA_abs1@eniad.ma", "TeacherPass123!");
        var response = await client.PostAsJsonAsync("/api/absences", new
        {
            etudiantId = studentId,
            moduleId = mismatchedModuleId,
            nombreHeures = 2,
            justifiee = false,
            dateAbsence = DateTime.UtcNow.AddDays(-2)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("filière").And.Contain("niveau");
    }

    [Fact]
    public async Task Absence_update_fails_if_concurrency_token_stale()
    {
        int absenceId;
        string base64RowVersion;
        using (var ctx = _factory.CreateContext())
        {
            var seeded = SampleData.SeedOne(ctx);
            SeedTeacher("teacherA_abs_concur@eniad.ma", "TeacherPass123!", seeded.Module.Id);

            var absence = new Absence
            {
                EtudiantId = seeded.Etudiant.Id,
                ModuleId = seeded.Module.Id,
                NombreHeures = 2,
                DateAbsence = DateTime.UtcNow.AddDays(-1),
                RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
            };
            ctx.Absences.Add(absence);
            ctx.SaveChanges();
            absenceId = absence.Id;
            base64RowVersion = Convert.ToBase64String(absence.RowVersion);
        }

        // Simulate concurrent edit in DB
        using (var ctx2 = _factory.CreateContext())
        {
            var absInDb = ctx2.Absences.Find(absenceId);
            absInDb!.NombreHeures = 5;
            absInDb.RowVersion = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 };
            ctx2.SaveChanges();
        }

        var client = await CreateTeacherClientAsync("teacherA_abs_concur@eniad.ma", "TeacherPass123!");
        var response = await client.PutAsJsonAsync($"/api/absences/{absenceId}", new
        {
            nombreHeures = 4,
            justifiee = true,
            dateAbsence = DateTime.UtcNow.AddDays(-1),
            rowVersion = base64RowVersion // Stale
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
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

    private async Task<HttpClient> CreateTeacherClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var token = await AuthHelper.GetTokenAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
