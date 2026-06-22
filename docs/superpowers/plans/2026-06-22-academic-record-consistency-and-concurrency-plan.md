# Academic Record Consistency and Optimistic Concurrency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement student/module compatibility validation and optimistic concurrency control for notes and absences.

**Architecture:** Add `RowVersion` column to `Notes` and `Absences` SQL tables and EF Core models. Implement checks and concurrency exception handling in controllers.

**Tech Stack:** ASP.NET Core 8, EF Core (InMemory for tests, SQL Server for prod), xUnit, FluentAssertions.

---

### Task 1: Write Failing Integration Tests (Red Stage)

**Files:**
- Modify: [NotesControllerTests.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.Tests/Controllers/NotesControllerTests.cs)
- Modify: [AbsencesControllerTests.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.Tests/Controllers/AbsencesControllerTests.cs)

- [ ] **Step 1: Write failing tests in `NotesControllerTests.cs`**
  Add tests for mismatched student/module filiere/level and optimistic concurrency.
  Code to add:
  ```csharp
  [Fact]
  public async Task Note_creation_fails_if_student_and_module_mismatched()
  {
      int studentId;
      int mismatchedModuleId;
      using (var ctx = _factory.CreateContext())
      {
          var seeded = SampleData.SeedOne(ctx);
          studentId = seeded.Etudiant.Id;
          
          var otherFil = new Filiere { Code = "IA", Intitule = "Intelligence Artificielle" };
          ctx.Filieres.Add(otherFil);
          ctx.SaveChanges();

          var otherMod = new Module
          {
              Code = "IA01", Nom = "Machine Learning",
              FiliereId = otherFil.Id, Niveau = "CI1",
              Coefficient = 4m, Semestre = "S1"
          };
          ctx.Modules.Add(otherMod);
          ctx.SaveChanges();
          mismatchedModuleId = otherMod.Id;
      }

      var client = await CreateAdminClientAsync();
      var response = await client.PostAsJsonAsync("/api/notes", new
      {
          etudiantId = studentId,
          moduleId = mismatchedModuleId,
          noteFinal = 12m,
          annee = "2025/2026",
          semestre = "S1"
      });

      response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
      var content = await response.Content.ReadAsStringAsync();
      content.Should().Contain("filière").And.Contain("niveau");
  }

  [Fact]
  public async Task Note_update_fails_if_concurrency_token_stale()
  {
      var client = await CreateAdminClientAsync();
      int noteId;
      string base64RowVersion;
      using (var ctx = _factory.CreateContext())
      {
          var seeded = SampleData.SeedOne(ctx);
          var note = ctx.Notes.First();
          noteId = note.Id;
          
          // Seed initial rowversion
          note.RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
          ctx.SaveChanges();
          base64RowVersion = Convert.ToBase64String(note.RowVersion);
      }

      // Simulate a concurrent DB edit modifying the row version
      using (var ctx2 = _factory.CreateContext())
      {
          var noteInDb = ctx2.Notes.Find(noteId);
          noteInDb!.NoteFinal = 18m;
          noteInDb.RowVersion = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 };
          ctx2.SaveChanges();
      }

      var response = await client.PutAsJsonAsync($"/api/notes/{noteId}", new
      {
          noteFinal = 15m,
          annee = "2025/2026",
          semestre = "S2",
          rowVersion = base64RowVersion // Stale row version
      });

      response.StatusCode.Should().Be(HttpStatusCode.Conflict);
  }
  ```

- [ ] **Step 2: Write failing tests in `AbsencesControllerTests.cs`**
  Add tests for mismatched student/module filiere/level and optimistic concurrency.
  Code to add:
  ```csharp
  [Fact]
  public async Task Absence_creation_fails_if_student_and_module_mismatched()
  {
      int studentId;
      int mismatchedModuleId;
      using (var ctx = _factory.CreateContext())
      {
          var seeded = SampleData.SeedOne(ctx);
          studentId = seeded.Etudiant.Id;
          
          var otherFil = new Filiere { Code = "IA", Intitule = "Intelligence Artificielle" };
          ctx.Filieres.Add(otherFil);
          ctx.SaveChanges();

          var otherMod = new Module
          {
              Code = "IA01", Nom = "Machine Learning",
              FiliereId = otherFil.Id, Niveau = "CI1",
              Coefficient = 4m, Semestre = "S1"
          };
          ctx.Modules.Add(otherMod);
          ctx.SaveChanges();
          mismatchedModuleId = otherMod.Id;
      }

      var client = await CreateTeacherClientAsync("teacherA_abs1@eniad.ma", "TeacherPass123!");
      var response = await client.PostAsJsonAsync("/api/absences", new
      {
          etudiantId = studentId,
          moduleId = mismatchedModuleId,
          nombreHeures = 2,
          justifiee = false,
          dateAbsence = DateTime.UtcNow.AddDays(-2)
      });

      response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
      var content = await response.Content.ReadAsStringAsync();
      content.Should().Contain("filière").And.Contain("niveau");
  }

  [Fact]
  public async Task Absence_update_fails_if_concurrency_token_stale()
  {
      int absenceId;
      string base64RowVersion;
      using (var ctx = _factory.CreateContext())
      {
          var seeded = SampleData.SeedOne(ctx);
          SeedTeacher("teacherA_abs_concur@eniad.ma", "TeacherPass123!", seeded.Module.Id);

          var absence = new Absence
          {
              EtudiantId = seeded.Etudiant.Id,
              ModuleId = seeded.Module.Id,
              NombreHeures = 2,
              DateAbsence = DateTime.UtcNow.AddDays(-1),
              RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
          };
          ctx.Absences.Add(absence);
          ctx.SaveChanges();
          absenceId = absence.Id;
          base64RowVersion = Convert.ToBase64String(absence.RowVersion);
      }

      // Simulate concurrent edit in DB
      using (var ctx2 = _factory.CreateContext())
      {
          var absInDb = ctx2.Absences.Find(absenceId);
          absInDb!.NombreHeures = 5;
          absInDb.RowVersion = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 };
          ctx2.SaveChanges();
      }

      var client = await CreateTeacherClientAsync("teacherA_abs_concur@eniad.ma", "TeacherPass123!");
      var response = await client.PutAsJsonAsync($"/api/absences/{absenceId}", new
      {
          nombreHeures = 4,
          justifiee = true,
          dateAbsence = DateTime.UtcNow.AddDays(-1),
          rowVersion = base64RowVersion // Stale
      });

      response.StatusCode.Should().Be(HttpStatusCode.Conflict);
  }
  ```

- [ ] **Step 3: Run the tests to confirm they fail (RED)**
  Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj`
  Expected: Failures due to compilation error (missing properties) or return code failures.

---

### Task 2: Update Models, DTOs, and Database Migrations

**Files:**
- Modify: [Note.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/Models/Note.cs)
- Modify: [Presence.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/Models/Presence.cs)
- Modify: [NoteDto.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/DTOs/Notes/NoteDto.cs)
- Modify: [AbsenceDto.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/DTOs/Absences/AbsenceDto.cs)
- Modify: [init.sql](file:///D:/PFA/plateforme-decisionnelle-pfa/database/init.sql)
- Modify: [RuntimeMigrations.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/Data/RuntimeMigrations.cs)

- [ ] **Step 1: Add `RowVersion` to Note and Absence entities**
  Add using `System.ComponentModel.DataAnnotations` (if needed) and the `RowVersion` property with `[Timestamp]`.
  In `Note.cs`:
  ```csharp
  [Timestamp]
  public byte[] RowVersion { get; set; } = Array.Empty<byte>();
  ```
  In `Presence.cs` (class `Absence`):
  ```csharp
  [Timestamp]
  public byte[] RowVersion { get; set; } = Array.Empty<byte>();
  ```

- [ ] **Step 2: Add `RowVersion` to Note and Absence DTOs**
  In `NoteDto.cs`:
  - Add to `NoteDto`:
    ```csharp
    public string RowVersion { get; set; } = string.Empty;
    ```
  - Add to `CreateNoteDto`:
    ```csharp
    public string RowVersion { get; set; } = string.Empty;
    ```
  - Add to `UpdateNoteDto`:
    ```csharp
    public string RowVersion { get; set; } = string.Empty;
    ```
  In `AbsenceDto.cs`:
  - Add to `UpdateAbsenceDto`:
    ```csharp
    public string RowVersion { get; set; } = string.Empty;
    ```

- [ ] **Step 3: Update `database/init.sql`**
  Modify tables `Notes` and `Absences` schema definitions.
  In `Notes`:
  ```sql
  RowVersion ROWVERSION NOT NULL
  ```
  In `Absences`:
  ```sql
  RowVersion ROWVERSION NOT NULL
  ```

- [ ] **Step 4: Update `backend/PlateformePFA.API/Data/RuntimeMigrations.cs`**
  Add idempotent `ALTER TABLE` statements to append `RowVersion` column to `Notes` and `Absences` tables.
  Code to add at the end of `Apply` method:
  ```csharp
  context.Database.ExecuteSqlRaw(@"
      IF OBJECT_ID('dbo.Notes', 'U') IS NOT NULL
         AND NOT EXISTS (SELECT 1 FROM sys.columns
                         WHERE object_id = OBJECT_ID('dbo.Notes') AND name = 'RowVersion')
          ALTER TABLE Notes ADD RowVersion ROWVERSION NOT NULL;
  ");
  context.Database.ExecuteSqlRaw(@"
      IF OBJECT_ID('dbo.Absences', 'U') IS NOT NULL
         AND NOT EXISTS (SELECT 1 FROM sys.columns
                         WHERE object_id = OBJECT_ID('dbo.Absences') AND name = 'RowVersion')
          ALTER TABLE Absences ADD RowVersion ROWVERSION NOT NULL;
  ");
  ```

- [ ] **Step 5: Run tests to verify they build (and now fail specifically on logical validation/400/409 rather than compilation)**
  Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj`
  Expected: Compile successfully, but tests fail on StatusCode expectations.

---

### Task 3: Implement Logic in Controllers

**Files:**
- Modify: [NotesController.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/Controllers/NotesController.cs)
- Modify: [AbsencesController.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/Controllers/AbsencesController.cs)

- [ ] **Step 1: Implement checks and concurrency logic in `NotesController.cs`**
  - **In `PostNote`**:
    Add student/module compatibility check:
    ```csharp
    var compatible = await _context.Etudiants
        .Where(e => e.Id == dto.EtudiantId && e.DesinscritLe == null)
        .Join(_context.Modules.Where(m => m.Id == dto.ModuleId),
              e => e.FiliereId, m => m.FiliereId, (e, m) => new { e, m })
        .AnyAsync(x => x.e.Niveau == x.m.Niveau);
    if (!compatible)
        return BadRequest(new { message = "L'étudiant et le module doivent appartenir à la même filière et au même niveau." });
    ```
  - **In `UpsertNote`**:
    Add the same compatibility check.
    For optimistic concurrency, check if `dto.RowVersion` is provided. If it is an update (i.e. `!created` or when we query and find it exists), set original value and wrap save in concurrency catch:
    ```csharp
    if (!created && !string.IsNullOrEmpty(dto.RowVersion))
    {
        _context.Entry(note).Property(n => n.RowVersion).OriginalValue = Convert.FromBase64String(dto.RowVersion);
    }
    // and wrap SaveChangesAsync in a try-catch for DbUpdateConcurrencyException:
    try
    {
        await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Conflict(new { message = "La ressource a été modifiée par un autre utilisateur." });
    }
    ```
  - **In `PutNote`**:
    Retrieve the original note, and update compatibility if student/module were modifiable (they aren't modifiable in `UpdateNoteDto`, but verify `dto.RowVersion`).
    Set original value for `RowVersion`:
    ```csharp
    if (!string.IsNullOrEmpty(dto.RowVersion))
    {
        _context.Entry(note).Property(n => n.RowVersion).OriginalValue = Convert.FromBase64String(dto.RowVersion);
    }
    ```
    Catch `DbUpdateConcurrencyException` and return `Conflict` response:
    ```csharp
    try
    {
        await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Conflict(new { message = "La ressource a été modifiée par un autre utilisateur." });
    }
    ```

- [ ] **Step 2: Implement checks and concurrency logic in `AbsencesController.cs`**
  - **In `PostAbsence`**:
    Add compatibility check:
    ```csharp
    var compatible = await _context.Etudiants
        .Where(e => e.Id == dto.EtudiantId && e.DesinscritLe == null)
        .Join(_context.Modules.Where(m => m.Id == dto.ModuleId),
              e => e.FiliereId, m => m.FiliereId, (e, m) => new { e, m })
        .AnyAsync(x => x.e.Niveau == x.m.Niveau);
    if (!compatible)
        return BadRequest(new { message = "L'étudiant et le module doivent appartenir à la même filière et au même niveau." });
    ```
  - **In `PutAbsence`**:
    Check if `dto.RowVersion` is not empty, set original value:
    ```csharp
    if (!string.IsNullOrEmpty(dto.RowVersion))
    {
        _context.Entry(absence).Property(a => a.RowVersion).OriginalValue = Convert.FromBase64String(dto.RowVersion);
    }
    ```
    Wrap `SaveChangesAsync()` in concurrency catch:
    ```csharp
    try
    {
        await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Conflict(new { message = "La ressource a été modifiée par un autre utilisateur." });
    }
    ```

---

### Task 4: Verify Tests Pass (Green Stage) and Commit

- [ ] **Step 1: Run all tests**
  Run: `dotnet test backend/PlateformePFA.Tests/PlateformePFA.Tests.csproj`
  Expected: All 59 tests pass.

- [ ] **Step 2: Commit the changes**
  Run:
  ```bash
  git add .
  git commit -m "fix: protect academic record integrity"
  ```
