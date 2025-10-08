using Microsoft.EntityFrameworkCore;
using InventarioApi.Data;
using InventarioApi.Models;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configurar WebRootPath para que apunte a la carpeta wwwroot del proyecto (no del bin)
var projectRoot = Directory.GetParent(builder.Environment.ContentRootPath)?.Parent?.Parent?.FullName;
if (projectRoot != null)
{
    builder.Environment.WebRootPath = Path.Combine(projectRoot, "wwwroot");
}

// Configurar DbContext con SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

// Configurar Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory API", Version = "v1" });
});

var app = builder.Build();

// Middleware
app.UseHttpsRedirection();
app.UseStaticFiles();

// Middleware personalizado para redirigir la raíz ANTES de Swagger
app.Use(async (context, next) =>
{
    // Si es la raíz, redirigir a login.html
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/login.html");
        return;
    }
    await next();
});

// Configurar Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory API v1");
    c.RoutePrefix = "swagger"; // Swagger estará en /swagger
});

// --- Endpoints ---

// Listado de productos con categoría
app.MapGet("/products", async (AppDbContext db) =>
    await db.Products.Include(p => p.Category).AsNoTracking().ToListAsync()
);

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
    var current = await db.InventoryMovements
        .Where(m => m.ProductId == dto.ProductId)
        .SumAsync(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity);

    var delta = dto.Type == MovementType.In ? dto.Quantity : -dto.Quantity;

    if (current + delta < 0)
        return Results.BadRequest("Stock insuficiente.");

    db.InventoryMovements.Add(dto);
    await db.SaveChangesAsync();

    return Results.Created($"/movements/{dto.Id}", dto);
});

// Login básico
app.MapPost("/login", (UserLogin login) =>
{
    if (login.Username == "admin" && login.Password == "1234")
        return Results.Ok(new { Message = "Login exitoso", User = login.Username });

    return Results.Unauthorized();
});

app.Run();

public record UserLogin(string Username, string Password);