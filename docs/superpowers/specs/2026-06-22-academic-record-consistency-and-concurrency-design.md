# Academic Record Consistency and Optimistic Concurrency Design Spec

This specification outlines the architecture, data structures, and behaviors required to implement Task 3 of the ENIAD BI & analytics platform remediation plan.

## 1. Goal
Ensure academic data integrity and prevent race conditions when editing student notes and absences.

## 2. Approach: EF Core Attributes + Controller Validation

### 2.1 Models Changes
The `Note` and `Absence` entities will be decorated with `[Timestamp]` columns mapped to `byte[] RowVersion`.
- **File**: [Note.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/Models/Note.cs)
- **File**: [Presence.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/Models/Presence.cs)

### 2.2 DTO Changes
- `NoteDto`, `CreateNoteDto` (propagated to `UpsertNoteDto`), and `UpdateNoteDto` will receive `RowVersion` as a `string` (Base64).
- `UpdateAbsenceDto` will receive `RowVersion` as a `string` (Base64).
- **File**: [NoteDto.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/DTOs/Notes/NoteDto.cs)
- **File**: [AbsenceDto.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/DTOs/Absences/AbsenceDto.cs)

### 2.3 Compatibility Validation
In `NotesController.cs` and `AbsencesController.cs`, requests to write notes or absences (POST, PUT, upsert) must verify that the student belongs to the same *Filière* and *Niveau* as the module.
- Query:
  ```csharp
  var compatible = await _context.Etudiants
      .Where(e => e.Id == dto.EtudiantId && e.DesinscritLe == null)
      .Join(_context.Modules.Where(m => m.Id == dto.ModuleId),
            e => e.FiliereId, m => m.FiliereId, (e, m) => new { e, m })
      .AnyAsync(x => x.e.Niveau == x.m.Niveau, ct);
  ```
- Response on failure: `400 Bad Request` with:
  `{ "message": "L'étudiant et le module doivent appartenir à la même filière et au même niveau." }`

### 2.4 Concurrency Control
Before calling `SaveChangesAsync()` during PUT/upsert operations:
- Check if `dto.RowVersion` is set.
- Assign the original value to the EF Core tracker:
  ```csharp
  _context.Entry(entity).Property(n => n.RowVersion).OriginalValue = Convert.FromBase64String(dto.RowVersion);
  ```
- Catch `DbUpdateConcurrencyException` and return:
  `409 Conflict` with `{ "message": "La ressource a été modifiée par un autre utilisateur." }`

## 3. Database Modifications
- `init.sql`: Specify `RowVersion ROWVERSION NOT NULL` on `Notes` and `Absences` tables.
- **File**: [init.sql](file:///D:/PFA/plateforme-decisionnelle-pfa/database/init.sql)
- `RuntimeMigrations.cs`: Add idempotent `ALTER TABLE` statements to backport the column to existing databases.
- **File**: [RuntimeMigrations.cs](file:///D:/PFA/plateforme-decisionnelle-pfa/backend/PlateformePFA.API/Data/RuntimeMigrations.cs)

## 4. Tests Spec
- **Filière/Niveau validation tests**: Verify attempts to save data with mismatched levels/filières return `400 Bad Request`.
- **Concurrency tests**: Verify updating a stale resource yields `409 Conflict`.
