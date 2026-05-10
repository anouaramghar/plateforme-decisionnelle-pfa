using PlateformePFA.API.Data;
using PlateformePFA.API.Models;

namespace PlateformePFA.Tests.Fixtures;

/// <summary>
/// Seeds enough rows to exercise list / dashboard / report endpoints
/// without depending on the production DataSeeder. Idempotent — safe to
/// call from multiple test classes sharing the same TestWebFactory.
/// </summary>
public static class SampleData
{
    public static (Filiere Filiere, Module Module, Etudiant Etudiant) SeedOne(AppDbContext ctx)
    {
        var fil = ctx.Filieres.FirstOrDefault(f => f.Code == "GI");
        if (fil == null)
        {
            fil = new Filiere { Code = "GI", Intitule = "Génie Informatique" };
            ctx.Filieres.Add(fil);
            ctx.SaveChanges();
        }

        var mod = ctx.Modules.FirstOrDefault(m => m.Code == "GI01");
        if (mod == null)
        {
            mod = new Module
            {
                Code = "GI01", Nom = "Architecture Logicielle",
                FiliereId = fil.Id, Niveau = "CI1",
                Coefficient = 4m, Semestre = "S1",
            };
            ctx.Modules.Add(mod);
            ctx.SaveChanges();
        }

        var etu = ctx.Etudiants.FirstOrDefault(e => e.Matricule == "E10001");
        if (etu == null)
        {
            etu = new Etudiant
            {
                Matricule = "E10001", Nom = "Test", Prenom = "Student",
                FiliereId = fil.Id, Niveau = "CI1", Annee = "2025/2026",
            };
            ctx.Etudiants.Add(etu);
            ctx.SaveChanges();

            ctx.Notes.Add(new Note
            {
                EtudiantId = etu.Id, ModuleId = mod.Id,
                NoteTD = 14m, NoteTP = 13m, NoteExamen = 11m, NoteFinal = 12.2m,
                Annee = "2025/2026", Semestre = "S2",
            });
            ctx.SaveChanges();
        }

        return (fil, mod, etu);
    }
}
