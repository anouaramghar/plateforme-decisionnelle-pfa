using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlateformePFA.API.Data;
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
        public async Task<ActionResult<IEnumerable<Module>>> GetModules(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            pageSize = Math.Min(pageSize, 100);
            return await _context.Modules
                .Include(m => m.Filiere)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
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

            _context.Modules.Remove(module);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
