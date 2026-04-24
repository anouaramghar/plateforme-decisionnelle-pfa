using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Utilisateur>  Utilisateurs  { get; set; }
        public DbSet<Filiere>      Filieres      { get; set; }
        public DbSet<Etudiant>     Etudiants     { get; set; }
        public DbSet<Module>       Modules       { get; set; }
        public DbSet<Note>         Notes         { get; set; }
        public DbSet<Absence>      Absences      { get; set; }  // was Presences/Presence
        public DbSet<Alerte>       Alertes       { get; set; }
        public DbSet<PredictionML> PredictionsML { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Désactive la suppression en cascade automatique (évite "multiple cascade paths")
            foreach (var relationship in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // ── UNIQUE indexes matching init.sql ──────────────────────────────────

            // Utilisateurs.Email UNIQUE
            modelBuilder.Entity<Utilisateur>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Filieres.Code UNIQUE
            modelBuilder.Entity<Filiere>()
                .HasIndex(f => f.Code)
                .IsUnique();

            // Etudiants.Matricule UNIQUE
            modelBuilder.Entity<Etudiant>()
                .HasIndex(e => e.Matricule)
                .IsUnique();

            // Modules.Code UNIQUE
            modelBuilder.Entity<Module>()
                .HasIndex(m => m.Code)
                .IsUnique();

            // Notes: UNIQUE (EtudiantId, ModuleId, Annee, Semestre)
            modelBuilder.Entity<Note>()
                .HasIndex(n => new { n.EtudiantId, n.ModuleId, n.Annee, n.Semestre })
                .IsUnique();

            // RefreshTokens.Token UNIQUE
            modelBuilder.Entity<RefreshToken>()
                .HasIndex(r => r.Token)
                .IsUnique();
        }
    }
}