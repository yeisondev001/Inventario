using InventarioApi.Data;
using InventarioApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

builder.Services.AddDbContext<IdentityContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityConnection")));

// Configurar Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory API", Version = "v1" });
});

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 10;
})
.AddEntityFrameworkStores<IdentityContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = 
    options.DefaultChallengeScheme =
    options.DefaultForbidScheme =
    options.DefaultScheme = 
    options.DefaultSignInScheme =
    options.DefaultSignOutScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["JWT:Issuer"], 
        ValidateAudience = true,
        ValidAudience = builder.Configuration["JWT:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(builder.Configuration["JWT:SigningKey"]!)
        )
    };

});
builder.Services.AddAuthorization();


var app = builder.Build();

#region Role Admin
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<IdentityContext>();
    await context.Database.MigrateAsync(); 
}


await CreateDefaultUser(app.Services);

async Task CreateDefaultUser(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    var adminRole = "Admin";
    if (!await roleManager.RoleExistsAsync(adminRole))
    {
        await roleManager.CreateAsync(new IdentityRole(adminRole));
    }

    var defaultUser = new AppUser
    {
        UserName = "admin",
        Email = "inventariosoport122@gmail.com",
        EmailConfirmed = true
    };

    var user = await userManager.FindByNameAsync(defaultUser.UserName);
    if (user == null)
    {
        var result = await userManager.CreateAsync(defaultUser, "Admin1234!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(defaultUser, adminRole);
        }
        else
        {
    
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            Console.WriteLine($"Error creando usuario: {errors}");
        }
    }
    else
    {
        Console.WriteLine("El usuario 'admin' ya existe");
    }
}
#endregion

// Middleware
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

// Middleware personalizado para redirigir la raÃ­z ANTES de Swagger
app.Use(async (context, next) =>
{
    // Si es la raÃ­z, redirigir a login.html
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
    c.RoutePrefix = "swagger"; // Swagger estarÃ¡ en /swagger
});

// --- Endpoints ---

#region Products

// GET /products  
app.MapGet("/products", async (AppDbContext db) =>
    await db.Products.Include(p => p.Category).AsNoTracking().ToListAsync()
);

// GET /products/{id}/stock  
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

// GET /products/search
app.MapGet("/products/search", async (
    [FromQuery] string q,
    [FromQuery] int? page,
    [FromQuery] int? pageSize,
    AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest("Debe especificar el parámetro 'q'.");

    var pg = Math.Max(page ?? 1, 1);
    var ps = Math.Max(pageSize ?? 10, 1);
    q = q.Trim();

    var query = db.Products
        .AsNoTracking()
        .Include(p => p.Category)
        .Where(p =>
            EF.Functions.Like(p.Name, $"%{q}%") ||
            EF.Functions.Like(p.SKU, $"%{q}%"));

    var total = await query.CountAsync();

    var items = await query
        .OrderBy(p => p.Name)
        .Skip((pg - 1) * ps)
        .Take(ps)
        .Select(p => new {
            p.Id,
            p.SKU,
            p.Name,
            p.UnitPrice,
            Category = p.Category != null ? p.Category.Name : null
        })
        .ToListAsync();

    return Results.Ok(new { total, page = pg, pageSize = ps, items });
});


// ✅✅✅ AQUI pegamos el nuevo POST /products
// Crear producto (y registrar stock de entrada)
app.MapPost("/products", async ([FromBody] CreateProductRequest req, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.SKU) || string.IsNullOrWhiteSpace(req.Name))
        return Results.BadRequest("SKU y Name son obligatorios.");

    if (req.UnitPrice < 0)
        return Results.BadRequest("UnitPrice no puede ser negativo.");

    var product = await db.Products.FirstOrDefaultAsync(p => p.SKU == req.SKU);

    if (product is null)
    {
        product = new Product
        {
            SKU = req.SKU.Trim(),
            Name = req.Name.Trim(),
            UnitPrice = req.UnitPrice,
            CategoryId = req.CategoryId
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();
    }
    else
    {
        product.UnitPrice = req.UnitPrice;
        if (req.CategoryId.HasValue) product.CategoryId = req.CategoryId;
        await db.SaveChangesAsync();
    }

    if (req.Quantity > 0)
    {
        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            Quantity = req.Quantity,
            Type = MovementType.In,
        });

        await db.SaveChangesAsync();
    }

    var stock = await db.InventoryMovements
        .Where(m => m.ProductId == product.Id)
        .SumAsync(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity);

    return Results.Created($"/products/{product.Id}", new
    {
        product.Id,
        product.SKU,
        product.Name,
        product.UnitPrice,
        product.CategoryId,
        Stock = stock
    });
});

#endregion


#region movimiento (entrada/salida)
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
#endregion

#region Login
app.MapPost("/login", async (UserLogin login, UserManager<AppUser> userManager, SignInManager<AppUser> signInManager) =>
{
    var user = await userManager.FindByNameAsync(login.Username);
    if (user == null) return Results.Unauthorized();

    var result = await signInManager.CheckPasswordSignInAsync(user, login.Password, false);
    if (result.Succeeded) return Results.Ok(new { Message = "Login exitoso", Username = user.UserName });

    return Results.Unauthorized();
});

//Password
app.MapPost("/auth/forgot-password", async (ForgotPasswordDto dto, UserManager<AppUser> userManager) =>
{
    var user = await userManager.FindByEmailAsync(dto.Email);
    if (user == null)
        return Results.BadRequest(new { Message = "Correo no registrado" });

    var token = await userManager.GeneratePasswordResetTokenAsync(user);

    // En producciÃ³n enviar este token por email
    return Results.Ok(new { token });
});

//Reset
// Resetear contraseÃ±a
app.MapPost("/auth/reset-password", async (ResetPasswordDto dto, UserManager<AppUser> userManager) =>
{
    var user = await userManager.FindByEmailAsync(dto.Email);
    if (user == null)
        return Results.BadRequest("Usuario no encontrado");

    var result = await userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

    if (result.Succeeded)
        return Results.Ok(new { Message = "ContraseÃ±a actualizada correctamente" });

    return Results.BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
});

#endregion


app.Run();

public record UserLogin(string Username, string Password);
public record ForgotPasswordDto(string Email);
public record ResetPasswordDto(string Email, string Token, string NewPassword);

public record CreateProductRequest(
    string SKU,
    string Name,
    decimal UnitPrice,
    int? CategoryId,
    int Quantity
);
