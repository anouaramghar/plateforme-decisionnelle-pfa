using Bogus;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Data
{
    public static class DataSeeder
    {
        public static void Initialize(AppDbContext context, IConfiguration configuration)
        {
            // Separate block, NOT inside the Filieres block
            if (!context.Utilisateurs.Any(u => u.Role == "Admin"))
            {
                var adminEmail    = configuration["ADMIN_SEED_EMAIL"];
                var adminPassword = configuration["ADMIN_SEED_PASSWORD"];
                var adminNom      = configuration["ADMIN_SEED_NOM"]    ?? "Admin";
                var adminPrenom   = configuration["ADMIN_SEED_PRENOM"] ?? "ENIAD";

                if (string.IsNullOrWhiteSpace(adminEmail) ||
                    string.IsNullOrWhiteSpace(adminPassword) ||
                    adminPassword.Length < 12 ||
                    adminPassword.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "ADMIN_SEED_EMAIL and ADMIN_SEED_PASSWORD must be set before first start. " +
                        "Password must be at least 12 characters and not a placeholder. " +
                        "See .env.example for details.");
                }

                context.Utilisateurs.Add(new Utilisateur
                {
                    Nom = adminNom,
                    Prenom = adminPrenom,
                    Email = adminEmail,
                    MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    Role = "Admin",
                    EstActif = true,
                    CreeLe = DateTime.UtcNow
                });
                context.SaveChanges();
            }

            if (context.Filieres.Any()) return;

            var seedSampleDataConfig = configuration["SEED_SAMPLE_DATA"];
            bool seedSampleData;
            if (bool.TryParse(seedSampleDataConfig, out var explicitFlag))
            {
                seedSampleData = explicitFlag;
            }
            else
            {
                var envName = configuration["ASPNETCORE_ENVIRONMENT"]
                              ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                seedSampleData = string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase);
            }
            if (!seedSampleData) return;

            var anneeCourante = configuration["CurrentAcademicYear"]
                                ?? CurrentAcademicYear();

            var responsableId = context.Utilisateurs
                .Where(u => u.Role == "Responsable" || u.Role == "Enseignant" || u.Role == "Admin")
                .Select(u => u.Id)
                .FirstOrDefault();

            // ==========================================
            // 1. FILIÈRES
            // ==========================================
            var filieres = new List<Filiere>
            {
                new Filiere { Code = "TCP",  Intitule = "Tronc Commun Préparatoire",                         ResponsableId = responsableId },
                new Filiere { Code = "GI",   Intitule = "Génie Informatique",                                ResponsableId = responsableId },
                new Filiere { Code = "IA",   Intitule = "Intelligence Artificielle",                         ResponsableId = responsableId },
                new Filiere { Code = "ROC",  Intitule = "Robotique et Objets Connectés",                     ResponsableId = responsableId },
                new Filiere { Code = "IRSI", Intitule = "Ingénierie en Réseaux et Systèmes d'Information",   ResponsableId = responsableId },
            };
            context.Filieres.AddRange(filieres);
            context.SaveChanges();

            // ==========================================
            // 2. MODULES — 4 par filière pour que nb_modules soit significatif
            //    comme feature ML. Chaque module reçoit des notes pour S1 et S2
            //    dans la boucle de génération ci-dessous.
            // ==========================================
            var modules = new List<Module>
            {
                // TCP — 4 modules (CP1 + CP2)
                new Module { Code = "TCP11", Nom = "Analyse Mathématique",            FiliereId = filieres[0].Id, Niveau = "CP1", Coefficient = 5m, Semestre = "S1" },
                new Module { Code = "TCP12", Nom = "Algèbre Linéaire",                FiliereId = filieres[0].Id, Niveau = "CP1", Coefficient = 5m, Semestre = "S1" },
                new Module { Code = "TCP21", Nom = "Physique Quantique & Mécanique",  FiliereId = filieres[0].Id, Niveau = "CP2", Coefficient = 4m, Semestre = "S2" },
                new Module { Code = "TCP22", Nom = "Initiation à l'Algorithmique",    FiliereId = filieres[0].Id, Niveau = "CP2", Coefficient = 3m, Semestre = "S2" },

                // GI — 4 modules (CI1 + CI2)
                new Module { Code = "GI01", Nom = "Architecture Logicielle",          FiliereId = filieres[1].Id, Niveau = "CI1", Coefficient = 4m, Semestre = "S1" },
                new Module { Code = "GI02", Nom = "Développement Fullstack",          FiliereId = filieres[1].Id, Niveau = "CI2", Coefficient = 5m, Semestre = "S2" },
                new Module { Code = "GI03", Nom = "Bases de données avancées",        FiliereId = filieres[1].Id, Niveau = "CI1", Coefficient = 4m, Semestre = "S1" },
                new Module { Code = "GI04", Nom = "Systèmes d'exploitation",          FiliereId = filieres[1].Id, Niveau = "CI2", Coefficient = 3m, Semestre = "S2" },

                // IA — 4 modules (CI1 + CI2)
                new Module { Code = "IA01", Nom = "Fondamentaux du Machine Learning", FiliereId = filieres[2].Id, Niveau = "CI1", Coefficient = 5m, Semestre = "S1" },
                new Module { Code = "IA02", Nom = "Deep Learning & Vision",           FiliereId = filieres[2].Id, Niveau = "CI2", Coefficient = 5m, Semestre = "S2" },
                new Module { Code = "IA03", Nom = "Traitement du langage naturel",    FiliereId = filieres[2].Id, Niveau = "CI1", Coefficient = 4m, Semestre = "S1" },
                new Module { Code = "IA04", Nom = "Mathématiques pour l'IA",          FiliereId = filieres[2].Id, Niveau = "CI2", Coefficient = 4m, Semestre = "S2" },

                // ROC — 4 modules (CI1 + CI2)
                new Module { Code = "ROC01", Nom = "Systèmes Embarqués",              FiliereId = filieres[3].Id, Niveau = "CI1", Coefficient = 4m, Semestre = "S1" },
                new Module { Code = "ROC02", Nom = "Protocoles IoT & Microcontrôleurs", FiliereId = filieres[3].Id, Niveau = "CI2", Coefficient = 4m, Semestre = "S2" },
                new Module { Code = "ROC03", Nom = "Automatique et contrôle",         FiliereId = filieres[3].Id, Niveau = "CI1", Coefficient = 3m, Semestre = "S1" },
                new Module { Code = "ROC04", Nom = "Communication sans fil",          FiliereId = filieres[3].Id, Niveau = "CI2", Coefficient = 3m, Semestre = "S2" },

                // IRSI — 4 modules (CI1 + CI2)
                new Module { Code = "IRSI01", Nom = "Architecture des Réseaux Avancés", FiliereId = filieres[4].Id, Niveau = "CI1", Coefficient = 4m, Semestre = "S1" },
                new Module { Code = "IRSI02", Nom = "Cybersécurité et Cryptographie",   FiliereId = filieres[4].Id, Niveau = "CI2", Coefficient = 5m, Semestre = "S2" },
                new Module { Code = "IRSI03", Nom = "Administration système Linux",     FiliereId = filieres[4].Id, Niveau = "CI1", Coefficient = 3m, Semestre = "S1" },
                new Module { Code = "IRSI04", Nom = "Sécurité des infrastructures",    FiliereId = filieres[4].Id, Niveau = "CI2", Coefficient = 4m, Semestre = "S2" },
            };
            context.Modules.AddRange(modules);
            context.SaveChanges();

            // ==========================================
            // 3. NOMS MAROCAINS
            // ==========================================
            var prenomsMarocains = new[] {
                "Youssef", "Fatima", "Amine", "Salma", "Karim", "Aya", "Mehdi", "Khadija", "Hamza", "Imane",
                "Omar", "Sara", "Yassine", "Meryem", "Ilyas", "Hiba", "Marouane", "Anouar", "Aymen", "Reda",
                "Zineb", "Saad", "Kenza", "Zakaria", "Hajar", "Ayoub", "Nada", "Oussama", "Najat", "Bilal"
            };
            var nomsMarocains = new[] {
                "Alaoui", "Benali", "Amrani", "El Idrissi", "Bennani", "Tazi", "Ait Ali", "Amghar", "Engar",
                "Chraibi", "Tahiri", "Zeroual", "Berrada", "El Fassi", "Mansouri", "Ouazzani", "Guessous",
                "Lahlou", "El Oufir", "Benjelloun", "Belghiti", "Moutaouakil", "Zidane", "El Amrani"
            };

            // ==========================================
            // 4. ÉTUDIANTS
            // ==========================================
            var niveauxCP = new[] { "CP1", "CP2" };
            int matriculeCounter = 10001;

            var etudiantFaker = new Faker<Etudiant>()
                .RuleFor(e => e.Nom,      f => f.PickRandom(nomsMarocains))
                .RuleFor(e => e.Prenom,   f => f.PickRandom(prenomsMarocains))
                .RuleFor(e => e.Matricule, _ => $"E{matriculeCounter++:D5}")
                .RuleFor(e => e.Email,    (f, e) => f.Internet.Email(e.Prenom, e.Nom, "eniad.ma").ToLower())
                .RuleFor(e => e.Niveau,   f => f.PickRandom(new[] { "CP1", "CP2", "CI1", "CI2", "CI3" }))
                .RuleFor(e => e.FiliereId, (f, e) => {
                    if (niveauxCP.Contains(e.Niveau)) return filieres[0].Id;
                    return f.PickRandom(filieres.Skip(1)).Id;
                })
                .RuleFor(e => e.Annee,  anneeCourante)
                .RuleFor(e => e.CreeLe, f => f.Date.Past(1));

            var etudiants = etudiantFaker.Generate(300);
            context.Etudiants.AddRange(etudiants);
            context.SaveChanges();

            var etudiantFiliereMap = etudiants.ToDictionary(e => e.Id, e => e.FiliereId);
            var filiereModulesMap  = modules
                .GroupBy(m => m.FiliereId)
                .ToDictionary(g => g.Key, g => g.Select(m => m.Id).ToList());

            // ==========================================
            // 5. NOTES — 3 profils pour forcer le modèle ML à apprendre
            //    une frontière probabiliste (et non binaire) :
            //
            //  À risque   (25 %, i%4==3) :
            //    NoteExamen 1–7,  NoteTD 3–11, NoteTP 3–11
            //    → NoteFinal 2–9  (clairement < 10, at_risk=1 dans DW)
            //
            //  Fragile    (25 %, i%4==1) :
            //    NoteExamen 5–13, NoteTD 7–15, NoteTP 7–15
            //    → NoteFinal 6–14 (croise le seuil 10 → zone grise)
            //    → at_risk=1 quand moy<10, at_risk=0 quand moy≥10
            //    → force le modèle à prédire 30–70% dans cette zone
            //
            //  Normal     (50 %, reste) :
            //    NoteExamen 10–18, NoteTD 12–20, NoteTP 12–20
            //    → NoteFinal 11–19 (clairement > 10, at_risk=0 dans DW)
            // ==========================================
            var noteRng = new Random(42);

            var atRiskIds = new HashSet<int>(
                etudiants.Where((_, i) => i % 4 == 3).Select(e => e.Id)
            );
            var fragileIds = new HashSet<int>(
                etudiants.Where((_, i) => i % 4 == 1).Select(e => e.Id)
            );

            var notes = new List<Note>();
            foreach (var etudiant in etudiants)
            {
                bool isAtRisk  = atRiskIds.Contains(etudiant.Id);
                bool isFragile = !isAtRisk && fragileIds.Contains(etudiant.Id);
                var  moduleIds = filiereModulesMap[etudiant.FiliereId];

                foreach (var moduleId in moduleIds)
                {
                    foreach (var semestre in new[] { "S1", "S2" })
                    {
                        decimal noteExamen, noteTD, noteTP;
                        if (isAtRisk)
                        {
                            // En difficulté : NoteFinal 2–9 (clairement < 10)
                            noteExamen = Math.Round((decimal)(noteRng.NextDouble() * 6  + 1),  2); // 1–7
                            noteTD     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 3),  2); // 3–11
                            noteTP     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 3),  2); // 3–11
                        }
                        else if (isFragile)
                        {
                            // Fragile : NoteFinal 6–14 (zone grise autour de 10)
                            noteExamen = Math.Round((decimal)(noteRng.NextDouble() * 8  + 5),  2); // 5–13
                            noteTD     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 7),  2); // 7–15
                            noteTP     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 7),  2); // 7–15
                        }
                        else
                        {
                            // Normal : NoteFinal 11–19 (clairement > 10)
                            noteExamen = Math.Round((decimal)(noteRng.NextDouble() * 8  + 10), 2); // 10–18
                            noteTD     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 12), 2); // 12–20
                            noteTP     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 12), 2); // 12–20
                        }

                        notes.Add(new Note
                        {
                            EtudiantId = etudiant.Id,
                            ModuleId   = moduleId,
                            Annee      = anneeCourante,
                            Semestre   = semestre,
                            NoteExamen = noteExamen,
                            NoteTD     = noteTD,
                            NoteTP     = noteTP,
                            NoteFinal  = Math.Round(noteExamen * 0.6m + noteTD * 0.2m + noteTP * 0.2m, 2),
                            CreeLe     = DateTime.UtcNow.AddDays(-noteRng.Next(1, 365)),
                        });
                    }
                }
            }
            context.Notes.AddRange(notes);
            context.SaveChanges();

            // ==========================================
            // 6. ABSENCES — 3 profils cohérents avec les notes
            //
            //  À risque : 7–14 événements × 2–6h → total 14–84h (avg ~48h)
            //             → dépasse le seuil 30h → taux DW ~37%
            //
            //  Fragile  : 3–8  événements × 2–4h → total 6–32h  (avg ~20h)
            //             → zone grise : parfois > 18h, rarement > 30h
            //             → taux DW ~16% → model incertain
            //
            //  Normal   : 0–3  événements × 2–4h → total 0–12h  (avg ~4h)
            //             → clairement sous 18h → taux DW ~3%
            // ==========================================
            var absRng   = new Random(123);
            var absences = new List<Absence>();

            foreach (var etudiant in etudiants)
            {
                bool isAtRisk  = atRiskIds.Contains(etudiant.Id);
                bool isFragile = !isAtRisk && fragileIds.Contains(etudiant.Id);
                var  moduleIds = filiereModulesMap[etudiant.FiliereId];

                int nbEvents = isAtRisk  ? absRng.Next(7, 15)
                             : isFragile ? absRng.Next(3, 9)
                             :             absRng.Next(0, 4);

                for (int i = 0; i < nbEvents; i++)
                {
                    var dateAbsence = DateTime.UtcNow.Date.AddDays(-absRng.Next(1, 101));
                    absences.Add(new Absence
                    {
                        EtudiantId   = etudiant.Id,
                        ModuleId     = moduleIds[absRng.Next(moduleIds.Count)],
                        NombreHeures = isAtRisk  ? absRng.Next(1, 4) * 2   // 2, 4 ou 6 h
                                     : isFragile ? absRng.Next(1, 3) * 2   // 2 ou 4 h
                                     :             absRng.Next(1, 3) * 2,   // 2 ou 4 h
                        Justifiee    = absRng.NextDouble() < (isAtRisk ? 0.15 : isFragile ? 0.35 : 0.55),
                        DateAbsence  = dateAbsence,
                        CreeLe       = dateAbsence,
                    });
                }
            }

            context.Absences.AddRange(absences);
            context.SaveChanges();
        }

        private static string CurrentAcademicYear()
        {
            var now   = DateTime.UtcNow;
            var start = now.Month >= 9 ? now.Year : now.Year - 1;
            return $"{start}/{start + 1}";
        }
    }
}
