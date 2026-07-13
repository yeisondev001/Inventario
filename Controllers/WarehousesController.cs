using InventarioApi.Data;
using InventarioApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventarioApi.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class WarehousesController : ControllerBase
{
    private readonly AppDbContext _db;

    public WarehousesController(AppDbContext db)
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
        var warehouses = await _db.Warehouses.Where(w => w.TenantId == tenantId).AsNoTracking().ToListAsync();
        return Ok(warehouses);
    }

    [HttpPost]
    [Authorize(Roles = "AdminTienda")]
    public async Task<IActionResult> Create([FromBody] Warehouse dto)
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return BadRequest(new { Message = "Usuario sin tenant asignado" });

        dto.TenantId = tenantId.Value;
        _db.Warehouses.Add(dto);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = dto.Id }, dto);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "AdminTienda")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return BadRequest(new { Message = "Usuario sin tenant asignado" });

        var wh = await _db.Warehouses.FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tenantId);
        if (wh == null) return NotFound(new { Message = "Almacen no encontrado" });

        if (await _db.InventoryMovements.AnyAsync(m => m.WarehouseId == id && m.TenantId == tenantId))
            return BadRequest(new { Message = "No se puede eliminar: hay movimientos asociados a este almacen" });

        _db.Warehouses.Remove(wh);
        await _db.SaveChangesAsync();
        return Ok(new { Message = "Almacen eliminado" });
    }
}
