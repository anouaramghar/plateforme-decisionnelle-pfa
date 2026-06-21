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
                // Read admin seed credentials from configuration (env vars in Docker).
                // Fail fast rather than silently baking a known password into the binary.
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

            // Use the configured academic year if set; otherwise compute it from
            // UTC clock (school year flips on 1 September). This avoids the
            // historic drift where seeded data was hardcoded to 2025/2026 even
            // though predictions stamped a different year, leaving zero overlap.
            var anneeCourante = configuration["CurrentAcademicYear"]
                                ?? CurrentAcademicYear();

            var responsableId = context.Utilisateurs
                .Where(u => u.Role == "Responsable" || u.Role == "Enseignant" || u.Role == "Admin")
                .Select(u => u.Id)
                .FirstOrDefault();

            // ==========================================
            // 1. CRÉATION DES FILIÈRES (Inclus le Tronc Commun)
            // ==========================================
            var filieres = new List<Filiere>
            {
                new Filiere { Code = "TCP", Intitule = "Tronc Commun Préparatoire", ResponsableId = responsableId },
                new Filiere { Code = "GI", Intitule = "Génie Informatique", ResponsableId = responsableId },
                new Filiere { Code = "IA", Intitule = "Intelligence Artificielle", ResponsableId = responsableId },
                new Filiere { Code = "ROC", Intitule = "Robotique et Objets Connectés", ResponsableId = responsableId },
                new Filiere { Code = "IRSI", Intitule = "Ingénierie en Réseaux et Systèmes d'Information", ResponsableId = responsableId }
            };
            context.Filieres.AddRange(filieres);
            context.SaveChanges(); 

            // ==========================================
            // 2. CRÉATION DES MODULES PAR FILIÈRE
            // ==========================================
            var modules = new List<Module>
            {
                // Modules Tronc Commun (CP)
                new Module { Code = "TCP11", Nom = "Analyse Mathématique", FiliereId = filieres[0].Id, Niveau = "CP1", Coefficient = 5m, Semestre = "S1" },
                new Module { Code = "TCP12", Nom = "Algèbre Linéaire", FiliereId = filieres[0].Id, Niveau = "CP1", Coefficient = 5m, Semestre = "S1" },
                new Module { Code = "TCP21", Nom = "Physique Quantique & Mécanique", FiliereId = filieres[0].Id, Niveau = "CP2", Coefficient = 4m, Semestre = "S3" },
                new Module { Code = "TCP22", Nom = "Initiation à l'Algorithmique", FiliereId = filieres[0].Id, Niveau = "CP2", Coefficient = 3m, Semestre = "S4" },

                // Modules Génie Informatique (GI)
                new Module { Code = "GI01", Nom = "Architecture Logicielle", FiliereId = filieres[1].Id, Niveau = "CI1", Coefficient = 4m, Semestre = "S1" },
                new Module { Code = "GI02", Nom = "Développement Fullstack", FiliereId = filieres[1].Id, Niveau = "CI2", Coefficient = 5m, Semestre = "S3" },

                // Modules Intelligence Artificielle (IA)
                new Module { Code = "IA01", Nom = "Fondamentaux du Machine Learning", FiliereId = filieres[2].Id, Niveau = "CI1", Coefficient = 5m, Semestre = "S1" },
                new Module { Code = "IA02", Nom = "Deep Learning & Vision par Ordinateur", FiliereId = filieres[2].Id, Niveau = "CI2", Coefficient = 5m, Semestre = "S3" },

                // Modules Robotique et Objets Connectés (ROC)
                new Module { Code = "ROC01", Nom = "Systèmes Embarqués", FiliereId = filieres[3].Id, Niveau = "CI1", Coefficient = 4m, Semestre = "S1" },
                new Module { Code = "ROC02", Nom = "Protocoles IoT & Microcontrôleurs", FiliereId = filieres[3].Id, Niveau = "CI2", Coefficient = 4m, Semestre = "S3" },

                // Modules Ingénierie en Réseaux et SI (IRSI)
                new Module { Code = "IRSI01", Nom = "Architecture des Réseaux Avancés", FiliereId = filieres[4].Id, Niveau = "CI1", Coefficient = 4m, Semestre = "S1" },
                new Module { Code = "IRSI02", Nom = "Cybersécurité et Cryptographie", FiliereId = filieres[4].Id, Niveau = "CI2", Coefficient = 5m, Semestre = "S3" }
            };
            context.Modules.AddRange(modules);
            context.SaveChanges();

            // ==========================================
            // 3. DICTIONNAIRES DE NOMS MAROCAINS
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
            // 4. GÉNÉRATION DES ÉTUDIANTS
            // ==========================================
            var niveauxCP = new[] { "CP1", "CP2" };
            var niveauxCI = new[] { "CI1", "CI2", "CI3" };

            // Counter-based matricule guarantees UNIQUE(Matricule) — random numbers
            // have a ~39% collision rate at 300 students in a 90 000-value space.
            int matriculeCounter = 10001;

            var etudiantFaker = new Faker<Etudiant>()
                .RuleFor(e => e.Nom, f => f.PickRandom(nomsMarocains))
                .RuleFor(e => e.Prenom, f => f.PickRandom(prenomsMarocains))
                .RuleFor(e => e.Matricule, _ => $"E{matriculeCounter++:D5}") // guaranteed unique
                .RuleFor(e => e.Email, (f, e) => f.Internet.Email(e.Prenom, e.Nom, "eniad.ma").ToLower())
                // Tirage aléatoire du niveau (CP ou CI)
                .RuleFor(e => e.Niveau, f => f.PickRandom(new[] { "CP1", "CP2", "CI1", "CI2", "CI3" }))
                // Attribution intelligente de la filière en fonction du niveau
                .RuleFor(e => e.FiliereId, (f, e) => {
                    if (niveauxCP.Contains(e.Niveau)) return filieres[0].Id; // Tronc Commun pour CP
                    return f.PickRandom(filieres.Skip(1)).Id; // L'une des 4 autres filières pour CI
                })
                .RuleFor(e => e.Annee, anneeCourante)
                .RuleFor(e => e.CreeLe, f => f.Date.Past(1));

            var etudiants = etudiantFaker.Generate(300); // 300 étudiants répartis sur les 5 niveaux
            context.Etudiants.AddRange(etudiants);
            context.SaveChanges();

            // ==========================================
            // 5. OPTIMISATION : MAPPING ETUDIANT -> MODULES
            // ==========================================
            // Pour s'assurer qu'un étudiant en GI n'a que des notes de modules GI, 
            // et un étudiant en CP n'a que des notes de TCP.
            var etudiantFiliereMap = etudiants.ToDictionary(e => e.Id, e => e.FiliereId);
            var filiereModulesMap = modules.GroupBy(m => m.FiliereId).ToDictionary(g => g.Key, g => g.Select(m => m.Id).ToList());

            // ==========================================
            // 6. GÉNÉRATION DES NOTES (par étudiant × module × semestre)
            // ==========================================
            // Random mass-generation caused UNIQUE(EtudiantId, ModuleId, Annee, Semestre)
            // violations. We now iterate the full cartesian product to guarantee uniqueness.
            var noteRng = new Random(42); // seeded — reproducible data across restarts
            var notes   = new List<Note>();

            // ~25% of students are "at risk" (moyenne < 10) so the DW has both
            // classes for ML binary classification. The set is deterministic
            // (seed=42) so reseeds always produce the same split.
            var atRiskIds = new HashSet<int>(
                etudiants.Where((_, i) => i % 4 == 3).Select(e => e.Id)
            );

            foreach (var etudiant in etudiants)
            {
                bool isAtRisk = atRiskIds.Contains(etudiant.Id);
                var moduleIds = filiereModulesMap[etudiant.FiliereId];
                foreach (var moduleId in moduleIds)
                {
                    foreach (var semestre in new[] { "S1", "S2" })
                    {
                        decimal noteExamen, noteTD, noteTP;
                        if (isAtRisk)
                        {
                            // At-risk profile: low exam grades, leading to moyenne < 10
                            noteExamen = Math.Round((decimal)(noteRng.NextDouble() * 5 + 1), 2);   // 1–6
                            noteTD     = Math.Round((decimal)(noteRng.NextDouble() * 8 + 5), 2);   // 5–13
                            noteTP     = Math.Round((decimal)(noteRng.NextDouble() * 8 + 5), 2);   // 5–13
                        }
                        else
                        {
                            // Normal profile: varied grades, mostly above 10
                            noteExamen = Math.Round((decimal)(noteRng.NextDouble() * 15 + 4), 2);  // 4–19
                            noteTD     = Math.Round((decimal)(noteRng.NextDouble() * 10 + 10), 2); // 10–20
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
                            CreeLe     = DateTime.UtcNow.AddDays(-noteRng.Next(1, 365))
                        });
                    }
                }
            }

            context.Notes.AddRange(notes);
            context.SaveChanges();

            // ==========================================
            // 7. GÉNÉRATION DES ABSENCES
            // ==========================================
            var absenceFaker = new Faker<Absence>()
                .RuleFor(a => a.EtudiantId, f => f.PickRandom(etudiants).Id)
                // Même logique stricte pour les absences
                .RuleFor(a => a.ModuleId, (f, a) => {
                    var filiereId = etudiantFiliereMap[a.EtudiantId];
                    var modulesDeCetteFiliere = filiereModulesMap[filiereId];
                    return f.PickRandom(modulesDeCetteFiliere);
                })
                .RuleFor(a => a.NombreHeures, f => f.PickRandom(new[] { 2, 4 })) 
                .RuleFor(a => a.Justifiee, f => f.Random.Bool(0.3f)) 
                .RuleFor(a => a.DateAbsence, f => f.Date.Recent(100)) 
                .RuleFor(a => a.CreeLe, (f, a) => a.DateAbsence);

            var absences = absenceFaker.Generate(400);
            context.Absences.AddRange(absences);
            context.SaveChanges();
        }

        /// <summary>
        /// Academic year in "YYYY/YYYY" form. The school year flips on 1 September.
        /// Mirrors PredictionsController.CurrentAcademicYear so seeded years and
        /// freshly-stamped predictions land in the same dimension row.
        /// </summary>
        private static string CurrentAcademicYear()
        {
            var now   = DateTime.UtcNow;
            var start = now.Month >= 9 ? now.Year : now.Year - 1;
            return $"{start}/{start + 1}";
        }
    }
}