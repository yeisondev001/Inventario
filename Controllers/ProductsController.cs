using InventarioApi.Data;
using InventarioApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InventarioApi.Controllers;

[ApiController]
[Route("products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    private int? GetCurrentTenantId()
    {
        var claim = User.FindFirst("TenantId");
        return int.TryParse(claim?.Value, out var id) ? id : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return BadRequest(new { message = "Usuario sin tienda asignada" });

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Category)
            .Select(p => new
            {
                p.Id,
                Sku = p.SKU,
                p.Name,
                p.Description,
                PurchasePrice = p.PurchasePrice,
                UnitPrice = p.UnitPrice,
                p.CategoryId,
                Category = p.Category != null ? p.Category.Name : null,
                Stock = _db.InventoryMovements
                    .Where(m => m.ProductId == p.Id && m.TenantId == tenantId)
                    .Sum(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity)
            })
            .ToListAsync();

        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return BadRequest(new { message = "Usuario sin tienda asignada" });

        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .Include(p => p.Category)
            .Select(p => new
            {
                p.Id,
                Sku = p.SKU,
                p.Name,
                p.Description,
                PurchasePrice = p.PurchasePrice,
                UnitPrice = p.UnitPrice,
                p.CategoryId,
                Category = p.Category != null ? p.Category.Name : null,
                Stock = _db.InventoryMovements
                    .Where(m => m.ProductId == p.Id && m.TenantId == tenantId)
                    .Sum(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity)
            })
            .FirstOrDefaultAsync();

        if (product == null)
            return NotFound(new { message = $"Producto con ID {id} no encontrado." });

        return Ok(product);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return BadRequest(new { message = "Usuario sin tienda asignada" });

        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Debe especificar el parametro 'q'." });

        q = q.Trim();

        var query = _db.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Category)
            .Where(p =>
                EF.Functions.Like(p.Name, $"%{q}%") ||
                EF.Functions.Like(p.SKU, $"%{q}%"));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((Math.Max(page, 1) - 1) * Math.Max(pageSize, 1))
            .Take(Math.Max(pageSize, 1))
            .Select(p => new
            {
                p.Id,
                Sku = p.SKU,
                p.Name,
                p.Description,
                PurchasePrice = p.PurchasePrice,
                UnitPrice = p.UnitPrice,
                Category = p.Category != null ? p.Category.Name : null,
                Stock = _db.InventoryMovements
                    .Where(m => m.ProductId == p.Id && m.TenantId == tenantId)
                    .Sum(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity)
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpPost]
    [Authorize(Roles = "AdminTienda")]
    public async Task<IActionResult> Create([FromBody] ProductCreateDto dto)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return BadRequest(new { message = "Usuario sin tienda asignada" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existingSku = await _db.Products.AnyAsync(p => p.SKU == dto.Sku && p.TenantId == tenantId);
        if (existingSku)
            return BadRequest(new { message = $"Ya existe un producto con el SKU '{dto.Sku}'." });

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId && c.TenantId == tenantId);
        if (!categoryExists)
            return BadRequest(new { message = $"La categoria con ID {dto.CategoryId} no existe." });

        var product = new Product
        {
            TenantId = tenantId.Value,
            SKU = dto.Sku,
            Name = dto.Name,
            Description = dto.Description,
            PurchasePrice = dto.PurchasePrice,
            UnitPrice = dto.UnitPrice,
            CategoryId = dto.CategoryId
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            new
            {
                product.Id,
                Sku = product.SKU,
                product.Name,
                product.Description,
                PurchasePrice = product.PurchasePrice,
                UnitPrice = product.UnitPrice,
                product.CategoryId
            });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "AdminTienda")]
    public async Task<IActionResult> Update(int id, [FromBody] ProductUpdateDto dto)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return BadRequest(new { message = "Usuario sin tienda asignada" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        if (product == null)
            return NotFound(new { message = $"Producto con ID {id} no encontrado." });

        if (dto.Sku != product.SKU)
        {
            var existingSku = await _db.Products.AnyAsync(p => p.SKU == dto.Sku && p.Id != id && p.TenantId == tenantId);
            if (existingSku)
                return BadRequest(new { message = $"Ya existe otro producto con el SKU '{dto.Sku}'." });
        }

        if (dto.CategoryId != product.CategoryId)
        {
            var categoryExists = await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId && c.TenantId == tenantId);
            if (!categoryExists)
                return BadRequest(new { message = $"La categoria con ID {dto.CategoryId} no existe." });
        }

        product.SKU = dto.Sku;
        product.Name = dto.Name;
        product.Description = dto.Description;
        product.PurchasePrice = dto.PurchasePrice;
        product.UnitPrice = dto.UnitPrice;
        product.CategoryId = dto.CategoryId;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Producto actualizado exitosamente",
            product = new
            {
                product.Id,
                Sku = product.SKU,
                product.Name,
                product.Description,
                PurchasePrice = product.PurchasePrice,
                UnitPrice = product.UnitPrice,
                product.CategoryId
            }
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "AdminTienda")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return BadRequest(new { message = "Usuario sin tienda asignada" });

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        if (product == null)
            return NotFound(new { message = $"Producto con ID {id} no encontrado." });

        var hasMovements = await _db.InventoryMovements.AnyAsync(m => m.ProductId == id && m.TenantId == tenantId);
        if (hasMovements)
            return BadRequest(new { message = "No se puede eliminar el producto porque tiene movimientos de inventario asociados." });

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Producto eliminado exitosamente" });
    }

    [HttpDelete("{id}/force")]
    [Authorize(Roles = "AdminTienda")]
    public async Task<IActionResult> ForceDelete(int id)
    {
        var tenantId = GetCurrentTenantId();
        if (tenantId == null) return BadRequest(new { message = "Usuario sin tienda asignada" });

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        if (product == null)
            return NotFound(new { message = $"Producto con ID {id} no encontrado." });

        var movements = await _db.InventoryMovements
            .Where(m => m.ProductId == id && m.TenantId == tenantId)
            .ToListAsync();

        _db.InventoryMovements.RemoveRange(movements);
        _db.Products.Remove(product);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Producto y todos sus movimientos eliminados exitosamente",
            movimientosEliminados = movements.Count
        });
    }
}

public record ProductCreateDto(
    string Sku,
    string Name,
    string? Description,
    decimal PurchasePrice,
    decimal UnitPrice,
    int CategoryId
);

public record ProductUpdateDto(
    string Sku,
    string Name,
    string? Description,
    decimal PurchasePrice,
    decimal UnitPrice,
    int CategoryId
);