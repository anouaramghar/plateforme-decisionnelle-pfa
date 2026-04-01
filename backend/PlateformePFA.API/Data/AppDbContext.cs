using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Utilisateur> Utilisateurs { get; set; }
        public DbSet<Filiere>     Filieres     { get; set; }
        public DbSet<Etudiant>    Etudiants    { get; set; }
        public DbSet<Module>      Modules      { get; set; }
        public DbSet<Note>        Notes        { get; set; }
        public DbSet<Absence>     Absences     { get; set; }  // was Presences/Presence
        public DbSet<Alerte>      Alertes      { get; set; }
        public DbSet<PredictionML> PredictionsML { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Désactive la suppression en cascade automatique (évite "multiple cascade paths")
            foreach (var relationship in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}