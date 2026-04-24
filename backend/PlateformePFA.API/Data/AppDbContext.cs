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

            // ── Decimal precision (matches SQL schema) ───────────────────────────
            modelBuilder.Entity<Module>()
                .Property(m => m.Coefficient).HasPrecision(4, 2);

            modelBuilder.Entity<Note>(e =>
            {
                e.Property(n => n.NoteExamen).HasPrecision(5, 2);
                e.Property(n => n.NoteTD).HasPrecision(5, 2);
                e.Property(n => n.NoteTP).HasPrecision(5, 2);
                e.Property(n => n.NoteFinal).HasPrecision(5, 2);
            });

            modelBuilder.Entity<PredictionML>(e =>
            {
                e.Property(p => p.ScoreRisque).HasPrecision(5, 4);
                e.Property(p => p.NotePredite).HasPrecision(5, 2);
                e.Property(p => p.Confiance).HasPrecision(5, 4);
            });
        }
    }
}