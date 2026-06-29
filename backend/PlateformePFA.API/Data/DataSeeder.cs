using Bogus;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Data
{
    public static class DataSeeder
    {
        public static void Initialize(AppDbContext context, IConfiguration configuration)
        {
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

            var hasExistingFilieres = context.Filieres.Any();
            if (hasExistingFilieres && context.Modules.Count() >= 188) return;

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
            var anneeCourante = configuration["CurrentAcademicYear"]
                                ?? CurrentAcademicYear();

            var responsableId = context.Utilisateurs
                .Where(u => u.Role == "Responsable" || u.Role == "Enseignant" || u.Role == "Admin")
                .Select(u => u.Id)
                .FirstOrDefault();

            var filiereDefinitions = new List<Filiere>
            {
                new Filiere { Code = "EPSI", Intitule = "Etudes Preparatoires en Sciences de l'Ingenieur",  ResponsableId = responsableId },
                new Filiere { Code = "IA",   Intitule = "Intelligence Artificielle",                        ResponsableId = responsableId },
                new Filiere { Code = "ROC",  Intitule = "Robotique et Objets Connectes",                    ResponsableId = responsableId },
                new Filiere { Code = "IRSI", Intitule = "Ingenierie Reseaux et Securite Informatique",     ResponsableId = responsableId },
                new Filiere { Code = "GINF", Intitule = "Genie Informatique",                               ResponsableId = responsableId },
            };
            if (hasExistingFilieres)
            {
                var existingFiliereCodes = context.Filieres.Select(f => f.Code).ToHashSet();
                context.Filieres.AddRange(filiereDefinitions.Where(f => !existingFiliereCodes.Contains(f.Code)));
            }
            else
            {
                context.Filieres.AddRange(filiereDefinitions);
            }
            context.SaveChanges();

            var filiereOrder = new[] { "EPSI", "IA", "ROC", "IRSI", "GINF" };
            var filieres = context.Filieres
                .Where(f => filiereOrder.Contains(f.Code))
                .ToList()
                .OrderBy(f => Array.IndexOf(filiereOrder, f.Code))
                .ToList();

            // Semester → Niveau mapping
            var semNiveauMap = new Dictionary<string, string>
            {
                ["S1"] = "CP1", ["S2"] = "CP1", ["S3"] = "CP2", ["S4"] = "CP2",
                ["S5"] = "CI1", ["S6"] = "CI1", ["S7"] = "CI2", ["S8"] = "CI2", ["S9"] = "CI3",
            };

            var modules = new List<Module>();
            int counter;

            // ── EPSI (S1–S4, 7 modules each) ──
            counter = 1;
            foreach (var (sem, names) in new[] {
                ("S1", new[] { "ALGEBRE 1", "ANALYSE 1", "MECANIQUE DU POINT", "ELECTROCINETIQUE",
                              "ALGORITHMIQUE ET ARCHITECTURE DES ORDINATEURS",
                              "METHODOLOGIE DE TRAVAIL UNIVERSITAIRE", "LANGUES ET COMMUNICATIONS 1" }),
                ("S2", new[] { "ALGEBRE 2", "ANALYSE 2", "ELECTROMAGNETISME", "ELECTRONIQUE ANALOGIQUE",
                              "PROGRAMMATION EN C", "CULTURE DIGITALE", "LANGUES ET COMMUNICATIONS 2" }),
                ("S3", new[] { "ALGEBRE 3", "ANALYSE 3", "TRAITEMENT DU SIGNAL ET SYSTEMES NUMERIQUES",
                              "ALGORITHMIQUE AVANCE & STRUCTURE DES DONNEES", "PROGRAMMATION PYTHON",
                              "SYSTEMES D'INFORMATIONS ET BASES DE DONNEES", "LANGUES ET COMMUNICATIONS 3" }),
                ("S4", new[] { "ANALYSE 4", "STATISTIQUES & PROBABILITES",
                              "INTRODUCTION AUX RESEAUX INFORMATIQUES ET SYSTEMES D'EXPLOITATION",
                              "ANALYSE NUMERIQUE", "DEVELOPPEMENT WEB", "ELECTRONIQUE NUMERIQUE",
                              "LANGUES ET COMMUNICATIONS 4" })
            })
            {
                foreach (var name in names)
                {
                    modules.Add(new Module { Code = $"EPSI{counter++:D2}", Nom = name, FiliereId = filieres[0].Id, Niveau = semNiveauMap[sem], Coefficient = 4m, Semestre = sem });
                }
            }

            // ── IA (S5–S9, 8 modules each) ──
            counter = 1;
            foreach (var (sem, names) in new[] {
                ("S5", new[] { "PROGRAMMATION ORIENTE OBJET EN JAVA", "PROGRAMMATION ORIENTE OBJET EN PYTHON",
                              "INGENIERIE DES BASES DE DONNEES AVANCEE", "SYSTEME D'EXPLOITATION ET PROGRAMMATION SYSTEMES",
                              "DEVELOPPEMENT D'APPLICATIONS WEB", "STATISTIQUES DESCRIPTIVES, INFERENTIELLES ET EXPLORATOIRES",
                              "COMPTABILITE ET CALCUL DES COUTS", "LANGUES ET TECHNIQUES DE COMMUNICATION 1" }),
                ("S6", new[] { "DEVELOPPEMENT D'APPLICATIONS WEB AVANCEE", "MACHINE LEARNING",
                              "RESEAUX INFORMATIQUE", "MODELISATION LOGICIELLE ET DONNEES STRUCTUREES",
                              "ANALYSE DE DONNEES", "RECHERCHE OPERATIONNELLE", "INGENIERIE DU PROMPTING",
                              "LANGUES ET TECHNIQUES DE COMMUNICATION 2" }),
                ("S7", new[] { "DEEP LEARNING", "VISION ARTIFICIELLE", "GESTION AGILE DE PROJET INFORMATIQUE",
                              "DEVELOPPEMENT MOBILE MULTIPLATEFORME", "OPTIMISATION COMBINATOIRE ET METAHEURISTIQUES",
                              "RESEAUX DE COMMUNICATION IOT", "MANAGEMENT ET MARKETING",
                              "LANGUES ET TECHNIQUES DE COMMUNICATION 3" }),
                ("S8", new[] { "SYSTEMES MULTI-AGENTS", "BUSINESS INTELLIGENCE ET ERP",
                              "ATELIER DES ACTIVITES PRATIQUES ET PROJETS", "MODELES DE LANGAGE ET TRAITEMENT AUTOMATIQUE DU TEXTE",
                              "VISION PAR ORDINATEUR ET INTELLIGENCE ARTIFICIELLE", "APPRENTISSAGE PAR RENFORCEMENT",
                              "DEVELOPPEMENT PERSONNEL", "LANGUES ET TECHNIQUES DE COMMUNICATION 4" }),
                ("S9", new[] { "INGENIERIE BIG DATA", "DEVOPS & MLOPS", "ATELIERS IA AVANCEE",
                              "CLOUD COMPUTING ET VIRTUALISATION", "CYBERSECURITY POUR L'IA ET LA ROBOTIQUE",
                              "EXPLAINABLE AI", "ETHIQUES ET DROITS", "LANGUES ET TECHNIQUES DE COMMUNICATION 5" })
            })
            {
                foreach (var name in names)
                {
                    modules.Add(new Module { Code = $"IA{counter++:D2}", Nom = name, FiliereId = filieres[1].Id, Niveau = semNiveauMap[sem], Coefficient = 4m, Semestre = sem });
                }
            }

            // ── ROC (S5–S9, 8 modules each) ──
            counter = 1;
            foreach (var (sem, names) in new[] {
                ("S5", new[] { "PROGRAMMATION ORIENTE OBJET EN JAVA", "PROGRAMMATION EMBARQUEE",
                              "STATISTIQUES DESCRIPTIVES, INFERENTIELLES ET EXPLORATOIRES",
                              "DEVELOPPEMENT D'APPLICATIONS WEB", "SYSTEME D'EXPLOITATION ET PROGRAMMATION SYSTEMES",
                              "PERCEPTION ET CAPTEURS POUR OBJETS ET ROBOTS CONNECTES",
                              "COMPTABILITE ET CALCUL DES COUTS", "Langues et Techniques de Communication 1" }),
                ("S6", new[] { "DEVELOPPEMENT D'APPLICATIONS WEB AVANCEE", "ROBOT OPERATING SYSTEM (ROS 1 & 2, RTOS)",
                              "RESEAUX INFORMATIQUE", "METHODOLOGIES DE NAVIGATION ET DE LOCALISATION DES ROBOTS",
                              "ANALYSE DES DONNEES", "RECHERCHE OPERATIONNELLE ET OPTIMISATION COMBINATOIRE",
                              "INGENIERIE DU PROMPTING", "Langues et Techniques de Communication 2" }),
                ("S7", new[] { "PROGRAMMATION EN ROBOTIQUE ET CONCEPTION 3D", "RESEAUX DE COMMUNICATION IOT",
                              "GESTION AGILE DE PROJET INFORMATIQUE", "DEVELOPPEMENT MOBILE MULTIPLATEFORME",
                              "MACHINE LEARNING", "VISION ARTIFICIELLE", "MANAGEMENT ET MARKETING",
                              "Langues et Techniques de Communication 3" }),
                ("S8", new[] { "SYSTEMES MULTI-AGENTS", "BUSINESS INTELLIGENCE ET ERP", "IA EMBARQUEE & EDGE AI",
                              "DEEP LEARNING", "REINFORCEMENT LEARNING", "ATELIER DES ACTIVITES PRATIQUES ET PROJETS",
                              "DEVELOPPEMENT PERSONNEL", "Langues et Techniques de Communication 4" }),
                ("S9", new[] { "INGENIERIE BIG DATA", "REALITE VIRTUEL ET REALITE AUGMENTEE",
                              "ATELIER ROBOTIQUE AVANCEE (COBOTIQUE, MOBILITE)",
                              "TECHNOLOGIES POUR L'AUTOMOBILE, L'AERONAUTIQUE ET LES DRONES",
                              "CLOUD COMPUTING ET VIRTUALISATION", "CYBERSECURITY POUR L'IA ET LA ROBOTIQUE",
                              "ETHIQUES ET DROITS", "Langues et Techniques de Communication 5" })
            })
            {
                foreach (var name in names)
                {
                    modules.Add(new Module { Code = $"ROC{counter++:D2}", Nom = name, FiliereId = filieres[2].Id, Niveau = semNiveauMap[sem], Coefficient = 4m, Semestre = sem });
                }
            }

            // ── IRSI (S5–S9, 8 modules each) ──
            counter = 1;
            foreach (var (sem, names) in new[] {
                ("S5", new[] { "STATISTIQUES DESCRIPTIVES, INFERENTIELLES ET EXPLORATOIRES",
                              "PROGRAMMATION ORIENTE OBJET EN PYTHON", "SYSTEME D'EXPLOITATION ET PROGRAMMATION SYSTEMES",
                              "RESEAUX INFORMATIQUE", "DEVELOPPEMENT D'APPLICATIONS WEB",
                              "PROGRAMMATION ORIENTE OBJET EN JAVA", "COMPTABILITE ET CALCUL DES COUTS",
                              "Langues et Techniques de Communication 1" }),
                ("S6", new[] { "ANALYSE DE DONNEES", "RECHERCHE OPERATIONNELLE ET OPTIMISATION COMBINATOIRE",
                              "ADMINISTRATION SYSTEMES LINUX", "INTERCONNEXION DES RESEAUX",
                              "INGENIERIE DES BASES DE DONNEES AVANCEE", "PROGRAMMATION SHELL & POWERSHELL",
                              "INGENIERIE DU PROMPTING", "Langues et Techniques de Communication 2" }),
                ("S7", new[] { "INTERCONNEXION DES RESEAUX AVANCEE", "ADMINISTRATION ET SECURITE DES SERVICES",
                              "GESTION AGILE DE PROJET INFORMATIQUE", "MACHINE LEARNING",
                              "CRYPTOGRAPHIE : PROTOCOLES ET APPLICATIONS", "RESEAUX DE COMMUNICATION IOT",
                              "MANAGEMENT ET MARKETING", "Langues et Techniques de Communication 3" }),
                ("S8", new[] { "SECURITE DES RESEAUX", "ATELIER DES ACTIVITES PRATIQUES ET PROJETS",
                              "CLOUD COMPUTING", "DEEP LEARNING", "APPRENTISSAGE PAR RENFORCEMENT",
                              "CYBERSECURITE", "DEVELOPPEMENT PERSONNEL", "Langues et Techniques de Communication 4" }),
                ("S9", new[] { "ATELIER PENTESTING WEB", "ATELIER ETHICAL HACKING", "ATELIER FIREWALL",
                              "TECHNOLOGIE BLOCKCHAIN", "GOUVERNANCE DE LA SECURITE ET ANALYSE DES RISQUES",
                              "ATELIER DEVSECOPS & SOC", "ETHIQUES ET DROITS", "Langues et Techniques de Communication 5" })
            })
            {
                foreach (var name in names)
                {
                    modules.Add(new Module { Code = $"IRSI{counter++:D2}", Nom = name, FiliereId = filieres[3].Id, Niveau = semNiveauMap[sem], Coefficient = 4m, Semestre = sem });
                }
            }

            // ── GINF (S5–S9, 8 modules each) ──
            counter = 1;
            foreach (var (sem, names) in new[] {
                ("S5", new[] { "PROGRAMMATION ORIENTE OBJET EN JAVA", "PROGRAMMATION ORIENTE OBJET EN PYTHON",
                              "INGENIERIE DES BASES DE DONNEES AVANCEE", "DEVELOPPEMENT D'APPLICATIONS WEB",
                              "SYSTEMES D'EXPLOITATION ET PROGRAMMATION SYSTEME",
                              "STATISTIQUES DESCRIPTIVES, INFERENTIELLES ET EXPLORATOIRES",
                              "COMPTABILITE ET CALCUL DES COUTS", "LANGUES ET TECHNIQUES DE COMMUNICATION 1" }),
                ("S6", new[] { "PROGRAMMATION ORIENTE OBJET EN C++", "MODELISATION LOGICIELLE ET DONNEES STRUCTUREES",
                              "DEVELOPPEMENT D'APPLICATIONS WEB AVANCEE", "RESEAUX INFORMATIQUES",
                              "ANALYSE DES DONNEES", "RECHERCHE OPERATIONNELLE ET OPTIMISATION COMBINATOIRE",
                              "INGENIERIE DU PROMPTING", "LANGUES ET TECHNIQUES DE COMMUNICATION 2" }),
                ("S7", new[] { "INGENIERIE J2E ET APPLICATIONS DISTRIBUEES", "DEVELOPPEMENT D'APPLICATION .NET",
                              "DEVELOPPEMENT MOBILE MULTIPLATEFORME", "ADMINISTRATION DES SYSTEMES",
                              "MACHINE LEARNING", "GESTION AGILE DE PROJET INFORMATIQUE",
                              "MANAGEMENT ET MARKETING", "LANGUES ET TECHNIQUES DE COMMUNICATION 3" }),
                ("S8", new[] { "INGENIERIE DEVOPS", "ADMINISTRATION DE BASES DE DONNEES", "DEEP LEARNING",
                              "APPRENTISSAGE PAR RENFORCEMENT", "BUSINESS INTELLIGENCE ET ERP",
                              "ATELIER DES ACTIVITES PRATIQUES ET PROJETS", "DEVELOPPEMENT PERSONNEL",
                              "LANGUES ET TECHNIQUES DE COMMUNICATION 4" }),
                ("S9", new[] { "URBANISATION DES SYSTEMES D'INFORMATION", "ARCHITECTURE LOGICIELLE ET DESIGN PATTERNS",
                              "INGENIERIE BIG DATA", "CLOUD COMPUTING ET VIRTUALISATION",
                              "ATELIER PENTESTING WEB", "INTERCONNEXION RESEAUX ET SECURITE RESEAUX",
                              "ETHIQUES ET DROITS", "LANGUES ET TECHNIQUES DE COMMUNICATION 5" })
            })
            {
                foreach (var name in names)
                {
                    modules.Add(new Module { Code = $"GINF{counter++:D2}", Nom = name, FiliereId = filieres[4].Id, Niveau = semNiveauMap[sem], Coefficient = 4m, Semestre = sem });
                }
            }

            var existingModuleCodes = context.Modules.Select(m => m.Code).ToHashSet();
            context.Modules.AddRange(modules.Where(m => !existingModuleCodes.Contains(m.Code)));
            context.SaveChanges();

            if (hasExistingFilieres || !seedSampleData) return;

            // Niveau → semesters for each filiere
            var niveauSemesters = new Dictionary<string, Dictionary<string, string[]>>
            {
                ["EPSI"] = new() { ["CP1"] = new[] { "S1", "S2" }, ["CP2"] = new[] { "S3", "S4" } },
                ["IA"]   = new() { ["CI1"] = new[] { "S5", "S6" }, ["CI2"] = new[] { "S7", "S8" }, ["CI3"] = new[] { "S9" } },
                ["ROC"]  = new() { ["CI1"] = new[] { "S5", "S6" }, ["CI2"] = new[] { "S7", "S8" }, ["CI3"] = new[] { "S9" } },
                ["IRSI"] = new() { ["CI1"] = new[] { "S5", "S6" }, ["CI2"] = new[] { "S7", "S8" }, ["CI3"] = new[] { "S9" } },
                ["GINF"] = new() { ["CI1"] = new[] { "S5", "S6" }, ["CI2"] = new[] { "S7", "S8" }, ["CI3"] = new[] { "S9" } },
            };

            var filiereCodeById = filieres.ToDictionary(f => f.Id, f => f.Code);

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

            int matriculeCounter = 10001;
            var etudiantFaker = new Faker<Etudiant>()
                .RuleFor(e => e.Nom,      f => f.PickRandom(nomsMarocains))
                .RuleFor(e => e.Prenom,   f => f.PickRandom(prenomsMarocains))
                .RuleFor(e => e.Matricule, _ => $"E{matriculeCounter++:D5}")
                .RuleFor(e => e.Email,    (f, e) => f.Internet.Email(e.Prenom, e.Nom, "eniad.ma").ToLower())
                .RuleFor(e => e.Annee,  anneeCourante)
                .RuleFor(e => e.CreeLe, f => f.Date.Past(1));

            // Assign filiere + niveau together so each student gets a coherent
            // curriculum slice.
            var studentDefs = new List<(string filiereCode, string niveau)>();
            // EPSI: 60 students (30 CP1, 30 CP2) — 20%
            // Each engineering: 60 students (20 CI1, 20 CI2, 20 CI3) — 20% each
            for (int i = 0; i < 30; i++) studentDefs.Add(("EPSI", "CP1"));
            for (int i = 0; i < 30; i++) studentDefs.Add(("EPSI", "CP2"));
            foreach (var code in new[] { "IA", "ROC", "IRSI", "GINF" })
            {
                for (int i = 0; i < 20; i++) studentDefs.Add((code, "CI1"));
                for (int i = 0; i < 20; i++) studentDefs.Add((code, "CI2"));
                for (int i = 0; i < 20; i++) studentDefs.Add((code, "CI3"));
            }

            var filiereCodeToId = filieres.ToDictionary(f => f.Code, f => f.Id);
            var etudiants = new List<Etudiant>();
            foreach (var (code, niveau) in studentDefs)
            {
                var e = etudiantFaker.Generate();
                e.FiliereId = filiereCodeToId[code];
                e.Niveau = niveau;
                etudiants.Add(e);
            }
            context.Etudiants.AddRange(etudiants);
            context.SaveChanges();

            // Module lookup: filiereId + semestre → module IDs
            var filiereSemModules = modules
                .GroupBy(m => (m.FiliereId, m.Semestre))
                .ToDictionary(g => g.Key, g => g.Select(m => m.Id).ToList());

            // Note generation — 3 profiles (same logic as before)
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
                var filiereCode = filiereCodeById[etudiant.FiliereId];
                var semesters = niveauSemesters[filiereCode][etudiant.Niveau];

                foreach (var semestre in semesters)
                {
                    var key = (etudiant.FiliereId, semestre);
                    if (!filiereSemModules.TryGetValue(key, out var moduleIds)) continue;

                    foreach (var moduleId in moduleIds)
                    {
                        decimal noteExamen, noteTD, noteTP;
                        if (isAtRisk)
                        {
                            noteExamen = Math.Round((decimal)(noteRng.NextDouble() * 6  + 1),  2);
                            noteTD     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 3),  2);
                            noteTP     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 3),  2);
                        }
                        else if (isFragile)
                        {
                            noteExamen = Math.Round((decimal)(noteRng.NextDouble() * 8  + 5),  2);
                            noteTD     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 7),  2);
                            noteTP     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 7),  2);
                        }
                        else
                        {
                            noteExamen = Math.Round((decimal)(noteRng.NextDouble() * 8  + 10), 2);
                            noteTD     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 12), 2);
                            noteTP     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 12), 2);
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

            // Absences — same 3 profiles, pick random modules from student's semesters
            var absRng   = new Random(123);
            var absences = new List<Absence>();

            foreach (var etudiant in etudiants)
            {
                bool isAtRisk  = atRiskIds.Contains(etudiant.Id);
                bool isFragile = !isAtRisk && fragileIds.Contains(etudiant.Id);
                var filiereCode = filiereCodeById[etudiant.FiliereId];
                var semesters = niveauSemesters[filiereCode][etudiant.Niveau];

                var allModIds = new List<int>();
                foreach (var sem in semesters)
                {
                    var key = (etudiant.FiliereId, sem);
                    if (filiereSemModules.TryGetValue(key, out var mids))
                        allModIds.AddRange(mids);
                }
                if (allModIds.Count == 0) continue;

                int nbEvents = isAtRisk  ? absRng.Next(7, 15)
                             : isFragile ? absRng.Next(3, 9)
                             :             absRng.Next(0, 4);

                for (int i = 0; i < nbEvents; i++)
                {
                    var dateAbsence = DateTime.UtcNow.Date.AddDays(-absRng.Next(1, 101));
                    absences.Add(new Absence
                    {
                        EtudiantId   = etudiant.Id,
                        ModuleId     = allModIds[absRng.Next(allModIds.Count)],
                        NombreHeures = isAtRisk  ? absRng.Next(1, 4) * 2
                                     : isFragile ? absRng.Next(1, 3) * 2
                                     :             absRng.Next(1, 3) * 2,
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
