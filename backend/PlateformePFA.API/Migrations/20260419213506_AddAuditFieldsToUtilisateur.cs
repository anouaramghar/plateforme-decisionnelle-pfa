using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlateformePFA.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFieldsToUtilisateur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Modules_Utilisateurs_EnseignantId",
                table: "Modules");

            migrationBuilder.DropForeignKey(
                name: "FK_PredictionsML_Etudiants_EtudiantId",
                table: "PredictionsML");

            migrationBuilder.DropTable(
                name: "Presences");

            migrationBuilder.DropIndex(
                name: "IX_Modules_EnseignantId",
                table: "Modules");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PredictionsML",
                table: "PredictionsML");

            migrationBuilder.DropColumn(
                name: "Valeur",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "EnseignantId",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "NbEtudiants",
                table: "Filieres");

            migrationBuilder.DropColumn(
                name: "NbModules",
                table: "Filieres");

            migrationBuilder.DropColumn(
                name: "NbSemestres",
                table: "Filieres");

            migrationBuilder.DropColumn(
                name: "Age",
                table: "Etudiants");

            migrationBuilder.DropColumn(
                name: "CNE",
                table: "Etudiants");

            migrationBuilder.DropColumn(
                name: "ScoreRisque",
                table: "Alertes");

            migrationBuilder.DropColumn(
                name: "Probabilite",
                table: "PredictionsML");

            migrationBuilder.RenameTable(
                name: "PredictionsML",
                newName: "Predictions");

            migrationBuilder.RenameColumn(
                name: "Session",
                table: "Notes",
                newName: "Semestre");

            migrationBuilder.RenameColumn(
                name: "AnneeAcad",
                table: "Notes",
                newName: "Annee");

            migrationBuilder.RenameColumn(
                name: "Intitule",
                table: "Modules",
                newName: "Nom");

            migrationBuilder.RenameColumn(
                name: "Ville",
                table: "Etudiants",
                newName: "Niveau");

            migrationBuilder.RenameColumn(
                name: "Statut",
                table: "Etudiants",
                newName: "Matricule");

            migrationBuilder.RenameColumn(
                name: "Promotion",
                table: "Etudiants",
                newName: "Annee");

            migrationBuilder.RenameColumn(
                name: "TypeAlerte",
                table: "Alertes",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "Statut",
                table: "Alertes",
                newName: "Niveau");

            migrationBuilder.RenameColumn(
                name: "DateGeneration",
                table: "Alertes",
                newName: "CreeLe");

            migrationBuilder.RenameColumn(
                name: "Modele",
                table: "Predictions",
                newName: "TypeModele");

            migrationBuilder.RenameColumn(
                name: "FeaturesJSON",
                table: "Predictions",
                newName: "Annee");

            migrationBuilder.RenameColumn(
                name: "DatePrediction",
                table: "Predictions",
                newName: "CreeLe");

            migrationBuilder.RenameIndex(
                name: "IX_PredictionsML_EtudiantId",
                table: "Predictions",
                newName: "IX_Predictions_EtudiantId");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreeLe",
                table: "Utilisateurs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "EstActif",
                table: "Utilisateurs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreeLe",
                table: "Notes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "NoteExamen",
                table: "Notes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NoteFinal",
                table: "Notes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NoteTD",
                table: "Notes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NoteTP",
                table: "Notes",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Semestre",
                table: "Modules",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<decimal>(
                name: "Coefficient",
                table: "Modules",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(float),
                oldType: "real");

            migrationBuilder.AddColumn<string>(
                name: "Niveau",
                table: "Modules",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "ResponsableId",
                table: "Filieres",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreeLe",
                table: "Etudiants",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Etudiants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Alertes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Resolue",
                table: "Alertes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolueeLe",
                table: "Alertes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Cluster",
                table: "Predictions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Confiance",
                table: "Predictions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NotePredite",
                table: "Predictions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ScoreRisque",
                table: "Predictions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Predictions",
                table: "Predictions",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Absences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EtudiantId = table.Column<int>(type: "int", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    NombreHeures = table.Column<int>(type: "int", nullable: false),
                    Justifiee = table.Column<bool>(type: "bit", nullable: false),
                    DateAbsence = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreeLe = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Absences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Absences_Etudiants_EtudiantId",
                        column: x => x.EtudiantId,
                        principalTable: "Etudiants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Absences_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Absences_EtudiantId",
                table: "Absences",
                column: "EtudiantId");

            migrationBuilder.CreateIndex(
                name: "IX_Absences_ModuleId",
                table: "Absences",
                column: "ModuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Predictions_Etudiants_EtudiantId",
                table: "Predictions",
                column: "EtudiantId",
                principalTable: "Etudiants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Predictions_Etudiants_EtudiantId",
                table: "Predictions");

            migrationBuilder.DropTable(
                name: "Absences");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Predictions",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "CreeLe",
                table: "Utilisateurs");

            migrationBuilder.DropColumn(
                name: "EstActif",
                table: "Utilisateurs");

            migrationBuilder.DropColumn(
                name: "CreeLe",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "NoteExamen",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "NoteFinal",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "NoteTD",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "NoteTP",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "Niveau",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "CreeLe",
                table: "Etudiants");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Etudiants");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Alertes");

            migrationBuilder.DropColumn(
                name: "Resolue",
                table: "Alertes");

            migrationBuilder.DropColumn(
                name: "ResolueeLe",
                table: "Alertes");

            migrationBuilder.DropColumn(
                name: "Cluster",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "Confiance",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "NotePredite",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "ScoreRisque",
                table: "Predictions");

            migrationBuilder.RenameTable(
                name: "Predictions",
                newName: "PredictionsML");

            migrationBuilder.RenameColumn(
                name: "Semestre",
                table: "Notes",
                newName: "Session");

            migrationBuilder.RenameColumn(
                name: "Annee",
                table: "Notes",
                newName: "AnneeAcad");

            migrationBuilder.RenameColumn(
                name: "Nom",
                table: "Modules",
                newName: "Intitule");

            migrationBuilder.RenameColumn(
                name: "Niveau",
                table: "Etudiants",
                newName: "Ville");

            migrationBuilder.RenameColumn(
                name: "Matricule",
                table: "Etudiants",
                newName: "Statut");

            migrationBuilder.RenameColumn(
                name: "Annee",
                table: "Etudiants",
                newName: "Promotion");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Alertes",
                newName: "TypeAlerte");

            migrationBuilder.RenameColumn(
                name: "Niveau",
                table: "Alertes",
                newName: "Statut");

            migrationBuilder.RenameColumn(
                name: "CreeLe",
                table: "Alertes",
                newName: "DateGeneration");

            migrationBuilder.RenameColumn(
                name: "TypeModele",
                table: "PredictionsML",
                newName: "Modele");

            migrationBuilder.RenameColumn(
                name: "CreeLe",
                table: "PredictionsML",
                newName: "DatePrediction");

            migrationBuilder.RenameColumn(
                name: "Annee",
                table: "PredictionsML",
                newName: "FeaturesJSON");

            migrationBuilder.RenameIndex(
                name: "IX_Predictions_EtudiantId",
                table: "PredictionsML",
                newName: "IX_PredictionsML_EtudiantId");

            migrationBuilder.AddColumn<float>(
                name: "Valeur",
                table: "Notes",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AlterColumn<int>(
                name: "Semestre",
                table: "Modules",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<float>(
                name: "Coefficient",
                table: "Modules",
                type: "real",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<int>(
                name: "EnseignantId",
                table: "Modules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "ResponsableId",
                table: "Filieres",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NbEtudiants",
                table: "Filieres",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NbModules",
                table: "Filieres",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NbSemestres",
                table: "Filieres",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Age",
                table: "Etudiants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CNE",
                table: "Etudiants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "ScoreRisque",
                table: "Alertes",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "Probabilite",
                table: "PredictionsML",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PredictionsML",
                table: "PredictionsML",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Presences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EtudiantId = table.Column<int>(type: "int", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Statut = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Presences_Etudiants_EtudiantId",
                        column: x => x.EtudiantId,
                        principalTable: "Etudiants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presences_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Modules_EnseignantId",
                table: "Modules",
                column: "EnseignantId");

            migrationBuilder.CreateIndex(
                name: "IX_Presences_EtudiantId",
                table: "Presences",
                column: "EtudiantId");

            migrationBuilder.CreateIndex(
                name: "IX_Presences_ModuleId",
                table: "Presences",
                column: "ModuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Modules_Utilisateurs_EnseignantId",
                table: "Modules",
                column: "EnseignantId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PredictionsML_Etudiants_EtudiantId",
                table: "PredictionsML",
                column: "EtudiantId",
                principalTable: "Etudiants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
