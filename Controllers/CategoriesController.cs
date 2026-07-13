using InventarioApi.Data;
using InventarioApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventarioApi.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db)
    {
        _db = db;
    }

    private int? GetTenantId()
    {
        var claim = User.FindFirst("TenantId");
        return int.TryParse(claim?.Value, out var id) ? id : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = GetTenantId();
        var categories = await _db.Categories.Where(c => c.TenantId == tenantId).AsNoTracking().ToListAsync();
        return Ok(categories);
    }

    [HttpPost]
    [Authorize(Roles = "AdminTienda")]
    public async Task<IActionResult> Create([FromBody] Category dto)
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return BadRequest(new { Message = "Usuario sin tenant asignado" });

        dto.TenantId = tenantId.Value;
        _db.Categories.Add(dto);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = dto.Id }, dto);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "AdminTienda")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return BadRequest(new { Message = "Usuario sin tenant asignado" });

        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
        if (cat == null) return NotFound(new { Message = "Categoria no encontrada" });

        if (await _db.Products.AnyAsync(p => p.CategoryId == id && p.TenantId == tenantId))
            return BadRequest(new { Message = "No se puede eliminar: hay productos usando esta categoria" });

        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return Ok(new { Message = "Categoria eliminada" });
    }
}
