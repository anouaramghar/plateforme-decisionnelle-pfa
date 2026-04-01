using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Data
{
    /// <summary>
    /// Injecte les données initiales au démarrage si elles n'existent pas encore.
    /// Remplace l'INSERT SQL avec hash placeholder de init.sql.
    /// </summary>
    public static class DataSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

            try
            {
                // Attendre que la BDD soit disponible (max 30s)
                var retries = 0;
                while (retries < 6)
                {
                    try { await context.Database.CanConnectAsync(); break; }
                    catch { retries++; await Task.Delay(5000); }
                }

                // ── Admin système ─────────────────────────────────────────
                if (!await context.Utilisateurs.AnyAsync(u => u.Email == "admin@eniad.dz"))
                {
                    context.Utilisateurs.Add(new Utilisateur
                    {
                        Nom           = "Admin",
                        Prenom        = "System",
                        Email         = "admin@eniad.dz",
                        MotDePasseHash = BCrypt.Net.BCrypt.HashPassword("Admin@ENIAD2025"),
                        Role          = "Admin"
                    });

                    await context.SaveChangesAsync();
                    logger.LogInformation("[DataSeeder] Admin créé → admin@eniad.dz / Admin@ENIAD2025");
                }
                else
                {
                    logger.LogInformation("[DataSeeder] Données déjà présentes — aucun seed nécessaire.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[DataSeeder] Erreur lors du seed — le backend continue quand même.");
            }
        }
    }
}
