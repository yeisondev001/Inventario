using InventarioApi.Data;
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
    /// Busca productos por nombre o SKU (código).
    /// </summary>
    /// <param name="q">Texto de búsqueda (nombre o SKU)</param>
    /// <param name="page">Página (1 por defecto)</param>
    /// <param name="pageSize">Tamaño de página (10 por defecto)</param>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Debe especificar el parámetro 'q'.");

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
            .Select(p => new {
                p.Id,
                p.SKU,
                p.Name,
                p.UnitPrice,
                Category = p.Category != null ? p.Category.Name : null
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
}
