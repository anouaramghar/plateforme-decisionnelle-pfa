using Microsoft.Extensions.Logging.Abstractions;
using PlateformePFA.API.Models;
using PlateformePFA.API.Services;
using PlateformePFA.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace PlateformePFA.Tests.Services;

public class AlerteServiceTests : IClassFixture<TestWebFactory>
{
    private readonly TestWebFactory _factory;

    public AlerteServiceTests(TestWebFactory factory)
    {
        _factory = factory;
        using var context = _factory.CreateContext();
        SampleData.SeedOne(context);
    }

    [Fact]
    public async Task ScanAll_ignores_withdrawn_students()
    {
        using var context = _factory.CreateContext();
        var seeded = SampleData.SeedOne(context);
        var withdrawn = new Etudiant
        {
            Matricule = "E-WITHDRAWN-SCAN", Nom = "Scan", Prenom = "Withdrawn",
            FiliereId = seeded.Filiere.Id, Niveau = "CI1", Annee = "2025/2026",
            DesinscritLe = DateTime.UtcNow.AddDays(-1),
        };
        context.Etudiants.Add(withdrawn);
        context.SaveChanges();
        context.Notes.Add(new Note
        {
            EtudiantId = withdrawn.Id, ModuleId = seeded.Module.Id,
            NoteFinal = 3m, Annee = "2027/2028", Semestre = "S1",
        });
        context.SaveChanges();
        var service = new AlerteService(context, NullLogger<AlerteService>.Instance);

        var result = await service.ScanAllAsync();

        result.notes.Should().Be(0);
        context.Alertes.Should().NotContain(a => a.EtudiantId == withdrawn.Id);
    }

    [Fact]
    public async Task ScanAll_counts_creations_independently_from_resolutions()
    {
        using var context = _factory.CreateContext();
        var seeded = SampleData.SeedOne(context);
        var resolvedStudent = AddStudent(context, seeded.Filiere.Id, "E-SCAN-RESOLVE");
        var createdStudent = AddStudent(context, seeded.Filiere.Id, "E-SCAN-CREATE");

        context.Notes.AddRange(
            new Note
            {
                EtudiantId = resolvedStudent.Id, ModuleId = seeded.Module.Id,
                NoteFinal = 15m, Annee = "2028/2029", Semestre = "S1",
            },
            new Note
            {
                EtudiantId = createdStudent.Id, ModuleId = seeded.Module.Id,
                NoteFinal = 4m, Annee = "2028/2029", Semestre = "S1",
            });
        context.Alertes.Add(new Alerte
        {
            EtudiantId = resolvedStudent.Id, ModuleId = seeded.Module.Id,
            Type = "NoteFaible", Niveau = "Eleve", Message = "Old", Resolue = false,
        });
        context.SaveChanges();
        var service = new AlerteService(context, NullLogger<AlerteService>.Instance);

        var result = await service.ScanAllAsync();

        result.notes.Should().Be(1);
        context.Alertes.Single(a => a.EtudiantId == resolvedStudent.Id).Resolue.Should().BeTrue();
        context.Alertes.Should().ContainSingle(a => a.EtudiantId == createdStudent.Id && !a.Resolue);
    }

    private static Etudiant AddStudent(
        PlateformePFA.API.Data.AppDbContext context, int filiereId, string matricule)
    {
        var student = new Etudiant
        {
            Matricule = matricule, Nom = "Scan", Prenom = "Active",
            FiliereId = filiereId, Niveau = "CI1", Annee = "2025/2026",
        };
        context.Etudiants.Add(student);
        context.SaveChanges();
        return student;
    }
}
