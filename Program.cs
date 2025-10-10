using Microsoft.EntityFrameworkCore;
using InventarioApi.Data;
using InventarioApi.Models;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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

#region Products
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

    // En producción enviar este token por email
    return Results.Ok(new { token });
});

//Reset
// Resetear contraseña
app.MapPost("/auth/reset-password", async (ResetPasswordDto dto, UserManager<AppUser> userManager) =>
{
    var user = await userManager.FindByEmailAsync(dto.Email);
    if (user == null)
        return Results.BadRequest("Usuario no encontrado");

    var result = await userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

    if (result.Succeeded)
        return Results.Ok(new { Message = "Contraseña actualizada correctamente" });

    return Results.BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
});


#endregion

app.Run();

public record UserLogin(string Username, string Password);
public record ForgotPasswordDto(string Email);
public record ResetPasswordDto(string Email, string Token, string NewPassword);