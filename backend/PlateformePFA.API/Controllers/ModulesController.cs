using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
using PlateformePFA.API.DTOs.Common;
using PlateformePFA.API.DTOs.Modules;
using PlateformePFA.API.Models;

namespace PlateformePFA.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ModulesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ModulesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Modules (paginated)
        [HttpGet]
        public async Task<ActionResult<PaginatedResult<Module>>> GetModules(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? filiereId = null)
        {
            page     = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Modules.AsNoTracking().AsQueryable();
            if (filiereId.HasValue) query = query.Where(m => m.FiliereId == filiereId.Value);

            var ordered = query.OrderBy(m => m.Id);
            var total   = await ordered.CountAsync();
            var items   = await ordered
                .Include(m => m.Filiere)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<Module>(items, total, page, pageSize);
        }

        // GET: api/Modules/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Module>> GetModule(int id)
        {
            var module = await _context.Modules
                .Include(m => m.Filiere)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (module == null)
            {
                return NotFound(new { message = "Module introuvable." });
            }

            return module;
        }

        // POST: api/Modules
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPost]
        public async Task<ActionResult<Module>> PostModule(CreateModuleDto dto)
        {
            var module = new Module
            {
                Code        = dto.Code,
                Nom         = dto.Nom,
                FiliereId   = dto.FiliereId,
                Niveau      = dto.Niveau,
                Coefficient = dto.Coefficient,
                Semestre    = dto.Semestre
            };

            _context.Modules.Add(module);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetModule), new { id = module.Id }, module);
        }

        // PUT: api/Modules/5
        [Authorize(Roles = "Admin,Responsable")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutModule(int id, UpdateModuleDto dto)
        {
            var module = await _context.Modules.FindAsync(id);
            if (module == null) return NotFound(new { message = "Module introuvable." });

            module.Code        = dto.Code;
            module.Nom         = dto.Nom;
            module.FiliereId   = dto.FiliereId;
            module.Niveau      = dto.Niveau;
            module.Coefficient = dto.Coefficient;
            module.Semestre    = dto.Semestre;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Modules.AnyAsync(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // DELETE: api/Modules/5
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteModule(int id)
        {
            var module = await _context.Modules.FindAsync(id);
            if (module == null) return NotFound();

            var nbNotes    = await _context.Notes.CountAsync(n => n.ModuleId == id);
            var nbAbsences = await _context.Absences.CountAsync(a => a.ModuleId == id);
            if (nbNotes > 0 || nbAbsences > 0)
            {
                return Conflict(new
                {
                    message = "Module référencé par des notes ou absences existantes.",
                    notes    = nbNotes,
                    absences = nbAbsences,
                });
            }

            _context.Modules.Remove(module);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
