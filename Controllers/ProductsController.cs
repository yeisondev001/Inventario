using InventarioApi.Data;
using InventarioApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventarioApi.Controllers;

[ApiController]
[Route("products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    /// <summary>
    /// Obtiene todos los productos con su stock actual
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _db.Products
            .AsNoTracking()
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
                // Calcular stock desde InventoryMovements
                Stock = _db.InventoryMovements
                    .Where(m => m.ProductId == p.Id)
                    .Sum(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity)
            })
            .ToListAsync();

        return Ok(products);
    }

    /// <summary>
    /// Obtiene un producto por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Id == id)
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
                    .Where(m => m.ProductId == p.Id)
                    .Sum(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity)
            })
            .FirstOrDefaultAsync();

        if (product == null)
            return NotFound(new { message = $"Producto con ID {id} no encontrado." });

        return Ok(product);
    }

    /// <summary>
    /// Busca productos por nombre o SKU (codigo).
    /// </summary>
    /// <param name="q">Texto de busqueda (nombre o SKU)</param>
    /// <param name="page">Pagina (1 por defecto)</param>
    /// <param name="pageSize">Tamano de pagina (10 por defecto)</param>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Debe especificar el parametro 'q'." });

        q = q.Trim();

        var query = _db.Products
            .AsNoTracking()
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
                    .Where(m => m.ProductId == p.Id)
                    .Sum(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity)
            })
            .ToListAsync();

        return Ok(new
        {
            total,
            page,
            pageSize,
            items
        });
    }

    /// <summary>
    /// Crea un nuevo producto
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Verificar si el SKU ya existe
        var existingSku = await _db.Products.AnyAsync(p => p.SKU == dto.Sku);
        if (existingSku)
            return BadRequest(new { message = $"Ya existe un producto con el SKU '{dto.Sku}'." });

        // Verificar que la categoria existe
        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId);
        if (!categoryExists)
            return BadRequest(new { message = $"La categoria con ID {dto.CategoryId} no existe." });

        var product = new Product
        {
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

    /// <summary>
    /// Actualiza un producto existente
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ProductUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound(new { message = $"Producto con ID {id} no encontrado." });

        // Verificar si el nuevo SKU ya existe en otro producto
        if (dto.Sku != product.SKU)
        {
            var existingSku = await _db.Products.AnyAsync(p => p.SKU == dto.Sku && p.Id != id);
            if (existingSku)
                return BadRequest(new { message = $"Ya existe otro producto con el SKU '{dto.Sku}'." });
        }

        // Verificar que la categoria existe
        if (dto.CategoryId != product.CategoryId)
        {
            var categoryExists = await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId);
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

    /// <summary>
    /// Elimina un producto (solo si no tiene movimientos)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound(new { message = $"Producto con ID {id} no encontrado." });

        // Verificar si tiene movimientos de inventario
        var hasMovements = await _db.InventoryMovements.AnyAsync(m => m.ProductId == id);
        if (hasMovements)
            return BadRequest(new { message = "No se puede eliminar el producto porque tiene movimientos de inventario asociados." });

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Producto eliminado exitosamente" });
    }

    /// <summary>
    /// Elimina un producto y todos sus movimientos de inventario (eliminacion forzada)
    /// </summary>
    [HttpDelete("{id}/force")]
    public async Task<IActionResult> ForceDelete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
            return NotFound(new { message = $"Producto con ID {id} no encontrado." });

        // Eliminar todos los movimientos asociados
        var movements = await _db.InventoryMovements
            .Where(m => m.ProductId == id)
            .ToListAsync();

        _db.InventoryMovements.RemoveRange(movements);

        // Eliminar el producto
        _db.Products.Remove(product);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Producto y todos sus movimientos eliminados exitosamente",
            movimientosEliminados = movements.Count
        });
    }
}

// DTOs para crear y actualizar productos
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