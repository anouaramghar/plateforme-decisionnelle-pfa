using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Déclaration de toutes tes tables (DbSets)
        public DbSet<Utilisateur> Utilisateurs { get; set; }
        public DbSet<Filiere> Filieres { get; set; }
        public DbSet<Etudiant> Etudiants { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<Presence> Presences { get; set; }
        public DbSet<Alerte> Alertes { get; set; }
        public DbSet<PredictionML> PredictionsML { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ⚠️ Configuration cruciale pour SQL Server :
            // Désactive la suppression en cascade automatique pour éviter
            // l'erreur "multiple cascade paths" très fréquente avec les modèles complexes.
            foreach (var relationship in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }
            
            // Tu pourras ajouter ici des configurations spécifiques (Fluent API) si besoin plus tard
            // Exemple : modelBuilder.Entity<Utilisateur>().HasIndex(u => u.Email).IsUnique();
        }
    }
}