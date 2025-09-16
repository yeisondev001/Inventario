using Microsoft.EntityFrameworkCore;
using InventarioApi.Data;
using InventarioApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();

// --- Endpoints ---

// Listado de productos con categoría
app.MapGet("/products", async (AppDbContext db) =>
    await db.Products.Include(p => p.Category).AsNoTracking().ToListAsync());

// Stock total por producto (sum(In) - sum(Out))
app.MapGet("/products/{id:int}/stock", async (int id, AppDbContext db) =>
{
    var exists = await db.Products.AnyAsync(p => p.Id == id);
    if (!exists) return Results.NotFound();

    var stock = await db.InventoryMovements
        .Where(m => m.ProductId == id)
        .GroupBy(_ => 1)
        .Select(g => g.Sum(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity))
        .FirstOrDefaultAsync();

    return Results.Ok(new { ProductId = id, Stock = stock });
});

// Crear movimiento (entrada/salida)
app.MapPost("/movements", async (InventoryMovement dto, AppDbContext db) =>
{
    // Validar que no se vaya a negativo
    var current = await db.InventoryMovements
        .Where(m => m.ProductId == dto.ProductId)
        .SumAsync(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity);

    var delta = dto.Type == MovementType.In ? dto.Quantity : -dto.Quantity;
    if (current + delta < 0) return Results.BadRequest("Stock insuficiente.");

    db.InventoryMovements.Add(dto);
    await db.SaveChangesAsync();
    return Results.Created($"/movements/{dto.Id}", dto);
});

app.Run();
