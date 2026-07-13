using InventarioApi.Data;
using InventarioApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InventarioApi.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class MovementsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MovementsController(AppDbContext db)
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
        var movements = await _db.InventoryMovements
            .Where(m => m.TenantId == tenantId)
            .Include(m => m.Product)
            .Include(m => m.Warehouse)
            .Include(m => m.User)
            .AsNoTracking()
            .OrderByDescending(m => m.MovementDate)
            .ToListAsync();
        return Ok(movements);
    }

    [HttpPost]
    [Authorize(Roles = "AdminTienda")]
    public async Task<IActionResult> Create([FromBody] InventoryMovement dto)
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return BadRequest(new { Message = "Usuario sin tenant asignado" });

        if (!await _db.Products.AnyAsync(p => p.Id == dto.ProductId && p.TenantId == tenantId))
            return BadRequest(new { Message = "El producto no existe" });

        if (!await _db.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && w.TenantId == tenantId))
            return BadRequest(new { Message = "El almacen no existe" });

        var current = await _db.InventoryMovements
            .Where(m => m.ProductId == dto.ProductId && m.TenantId == tenantId)
            .SumAsync(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity);

        var delta = dto.Type == MovementType.In ? dto.Quantity : -dto.Quantity;

        if (current + delta < 0)
            return BadRequest(new { Message = "Stock insuficiente para realizar esta salida" });

        dto.TenantId = tenantId.Value;
        dto.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _db.InventoryMovements.Add(dto);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), new { id = dto.Id }, dto);
    }
}
