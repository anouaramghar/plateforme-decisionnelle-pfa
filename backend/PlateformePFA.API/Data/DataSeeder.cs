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
            // 5. NOTES — profils réalistes et cohérents avec les seuils ML
            //
            //  À risque (25 %, index % 4 == 3, seed=42 → déterministe) :
            //    NoteExamen 1–7, NoteTD 4–12, NoteTP 4–12
            //    → NoteFinal 2.2 – 9.0  (clairement < 10)
            //
            //  Normal (75 %) :
            //    NoteExamen 8–18, NoteTD 10–20, NoteTP 10–20
            //    → NoteFinal 8.8 – 18.8 (majoritairement > 10, réaliste)
            // ==========================================
            var noteRng = new Random(42);

            var atRiskIds = new HashSet<int>(
                etudiants.Where((_, i) => i % 4 == 3).Select(e => e.Id)
            );

            var notes = new List<Note>();
            foreach (var etudiant in etudiants)
            {
                bool isAtRisk  = atRiskIds.Contains(etudiant.Id);
                var  moduleIds = filiereModulesMap[etudiant.FiliereId];

                foreach (var moduleId in moduleIds)
                {
                    foreach (var semestre in new[] { "S1", "S2" })
                    {
                        decimal noteExamen, noteTD, noteTP;
                        if (isAtRisk)
                        {
                            // Profil en difficulté : exam très bas → NoteFinal 2–9
                            noteExamen = Math.Round((decimal)(noteRng.NextDouble() * 6  + 1),  2); // 1–7
                            noteTD     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 4),  2); // 4–12
                            noteTP     = Math.Round((decimal)(noteRng.NextDouble() * 8  + 4),  2); // 4–12
                        }
                        else
                        {
                            // Profil normal : exam correct → NoteFinal 9–19
                            noteExamen = Math.Round((decimal)(noteRng.NextDouble() * 10 + 8),  2); // 8–18
                            noteTD     = Math.Round((decimal)(noteRng.NextDouble() * 10 + 10), 2); // 10–20
                            noteTP     = Math.Round((decimal)(noteRng.NextDouble() * 10 + 10), 2); // 10–20
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
            // 6. ABSENCES — corrélées au profil de risque
            //
            //  À risque : 5–14 événements × 2–6h → total 10–84h (avg ~38h)
            //             → dépasse le seuil 30h de l'heuristique RiskScorer
            //             → taux DW ≈ 38h / (4 modules × 32h) ≈ 30 % (feature ML utile)
            //
            //  Normal   : 0–5  événements × 2–4h → total 0–20h  (avg ~7h)
            //             → sous le seuil 18h de l'heuristique
            //             → taux DW ≈ 7h / 128h ≈ 5 %
            // ==========================================
            var absRng   = new Random(123);
            var absences = new List<Absence>();

            foreach (var etudiant in etudiants)
            {
                bool isAtRisk  = atRiskIds.Contains(etudiant.Id);
                var  moduleIds = filiereModulesMap[etudiant.FiliereId];

                int nbEvents = isAtRisk
                    ? absRng.Next(5, 15)   // 5–14 absences
                    : absRng.Next(0, 6);   // 0–5 absences

                for (int i = 0; i < nbEvents; i++)
                {
                    var dateAbsence = DateTime.UtcNow.Date.AddDays(-absRng.Next(1, 101));
                    absences.Add(new Absence
                    {
                        EtudiantId   = etudiant.Id,
                        ModuleId     = moduleIds[absRng.Next(moduleIds.Count)],
                        NombreHeures = isAtRisk
                            ? absRng.Next(1, 4) * 2   // 2, 4 ou 6 heures
                            : absRng.Next(1, 3) * 2,  // 2 ou 4 heures
                        Justifiee    = absRng.NextDouble() < (isAtRisk ? 0.15 : 0.50),
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
