using InventarioApi.Data;
using InventarioApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// Carga overrides locales sin commitear (gitignored). En produccion usar variables de entorno.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Configurar DbContext con SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Agregar controladores
builder.Services.AddControllers();

// Configurar Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory API", Version = "v1" });
});

// Configurar Identity usando AppDbContext
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 10;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT
var jwtSigningKey = builder.Configuration["JWT:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey) || jwtSigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "JWT:SigningKey no configurada o demasiado corta (min 32 chars). " +
        "Configurala via variable de entorno JWT__SigningKey o appsettings.Local.json.");
}
var jwtIssuer = builder.Configuration["JWT:Issuer"] ?? "InventarioApi";
var jwtAudience = builder.Configuration["JWT:Audience"] ?? "InventarioApi";

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
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSigningKey))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

#region Database Migration and Seed
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
}

await CreateDefaultUsers(app.Services);

async Task CreateDefaultUsers(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    Console.WriteLine("========================================");
    Console.WriteLine("INICIANDO CREACION DE USUARIOS Y ROLES");
    Console.WriteLine("========================================");

    try
    {
        // Crear roles: Admin (SuperAdmin/SaaS owner), AdminTienda, User
        var roles = new[] { "Admin", "AdminTienda", "User" };
        foreach (var roleName in roles)
        {
            var roleExists = await roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
                Console.WriteLine($"Rol '{roleName}' creado");
            }
        }

        // Crear SuperAdmin (tu, el dueno del SaaS) - sin tenant, accede a todo
        var adminUser = new AppUser
        {
            UserName = "admin",
            Email = "admin@inventario.com",
            EmailConfirmed = true,
            TenantId = null
        };

        var existingAdmin = await userManager.FindByNameAsync(adminUser.UserName);
        if (existingAdmin == null)
        {
            // Generar password aleatorio seguro y mostrarlo UNA sola vez
            var password = GenerateRandomPassword();
            var adminResult = await userManager.CreateAsync(adminUser, password);
            if (adminResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Console.WriteLine("========================================");
                Console.WriteLine("SUPERADMIN CREADO (anotalo, no se muestra de nuevo):");
                Console.WriteLine($"  Usuario: admin");
                Console.WriteLine($"  Password: {password}");
                Console.WriteLine("========================================");
            }
            else
            {
                Console.WriteLine($"Error creando SuperAdmin: {string.Join(", ", adminResult.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            await userManager.AddToRoleAsync(existingAdmin, "Admin");
            Console.WriteLine("SuperAdmin 'admin' ya existe");
        }

        Console.WriteLine("========================================\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR CRITICO en CreateDefaultUsers: {ex.Message}");
    }
}

static string GenerateRandomPassword()
{
    const string lower = "abcdefghijklmnopqrstuvwxyz";
    const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string digit = "0123456789";
    const string special = "!@#$%^&*";
    var bytes = RandomNumberGenerator.GetBytes(16);
    var chars = new[]
    {
        lower[bytes[0] % lower.Length],
        upper[bytes[1] % upper.Length],
        digit[bytes[2] % digit.Length],
        special[bytes[3] % special.Length]
    };
    var all = lower + upper + digit + special;
    var rest = new char[8];
    for (int i = 0; i < 8; i++)
        rest[i] = all[bytes[4 + i] % all.Length];
    return new string(chars.Concat(rest).ToArray());
}
#endregion

// Middleware
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/login.html");
        return;
    }

    if (context.Request.Path == "/FormularioInventario/formularioDeInventario.html")
    {
        context.Response.Redirect("/login.html");
        return;
    }

    await next();
});

app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory API v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();

// Helper para resolver TenantId del usuario autenticado
static int? GetCurrentTenantId(ClaimsPrincipal user)
{
    var claim = user.FindFirst("TenantId");
    return int.TryParse(claim?.Value, out var id) ? id : null;
}

#region Categories
app.MapGet("/categories", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var tenantId = GetCurrentTenantId(user);
    return await db.Categories.Where(c => c.TenantId == tenantId).AsNoTracking().ToListAsync();
}).RequireAuthorization();

app.MapPost("/categories", async (Category dto, AppDbContext db, ClaimsPrincipal user) =>
{
    var tenantId = GetCurrentTenantId(user);
    if (tenantId == null) return Results.BadRequest(new { Message = "Usuario sin tenant asignado" });

    dto.TenantId = tenantId.Value;
    db.Categories.Add(dto);
    await db.SaveChangesAsync();
    return Results.Created($"/categories/{dto.Id}", dto);
}).RequireAuthorization();

app.MapDelete("/categories/{id}", async (int id, AppDbContext db, ClaimsPrincipal user) =>
{
    var tenantId = GetCurrentTenantId(user);
    if (tenantId == null) return Results.BadRequest(new { Message = "Usuario sin tenant asignado" });

    var cat = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId);
    if (cat == null) return Results.NotFound(new { Message = "Categoria no encontrada" });

    if (await db.Products.AnyAsync(p => p.CategoryId == id && p.TenantId == tenantId))
        return Results.BadRequest(new { Message = "No se puede eliminar: hay productos usando esta categoria" });

    db.Categories.Remove(cat);
    await db.SaveChangesAsync();
    return Results.Ok(new { Message = "Categoria eliminada" });
}).RequireAuthorization();
#endregion

#region Warehouses
app.MapGet("/warehouses", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var tenantId = GetCurrentTenantId(user);
    return await db.Warehouses.Where(w => w.TenantId == tenantId).AsNoTracking().ToListAsync();
}).RequireAuthorization();

app.MapPost("/warehouses", async (Warehouse dto, AppDbContext db, ClaimsPrincipal user) =>
{
    var tenantId = GetCurrentTenantId(user);
    if (tenantId == null) return Results.BadRequest(new { Message = "Usuario sin tenant asignado" });

    dto.TenantId = tenantId.Value;
    db.Warehouses.Add(dto);
    await db.SaveChangesAsync();
    return Results.Created($"/warehouses/{dto.Id}", dto);
}).RequireAuthorization();

app.MapDelete("/warehouses/{id}", async (int id, AppDbContext db, ClaimsPrincipal user) =>
{
    var tenantId = GetCurrentTenantId(user);
    if (tenantId == null) return Results.BadRequest(new { Message = "Usuario sin tenant asignado" });

    var wh = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tenantId);
    if (wh == null) return Results.NotFound(new { Message = "Almacen no encontrado" });

    if (await db.InventoryMovements.AnyAsync(m => m.WarehouseId == id && m.TenantId == tenantId))
        return Results.BadRequest(new { Message = "No se puede eliminar: hay movimientos asociados a este almacen" });

    db.Warehouses.Remove(wh);
    await db.SaveChangesAsync();
    return Results.Ok(new { Message = "Almacen eliminado" });
}).RequireAuthorization();
#endregion

#region Movements
app.MapGet("/movements", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var tenantId = GetCurrentTenantId(user);
    return await db.InventoryMovements
        .Where(m => m.TenantId == tenantId)
        .Include(m => m.Product)
        .Include(m => m.Warehouse)
        .AsNoTracking()
        .OrderByDescending(m => m.MovementDate)
        .ToListAsync();
}).RequireAuthorization();

app.MapPost("/movements", async (InventoryMovement dto, AppDbContext db, ClaimsPrincipal user) =>
{
    var tenantId = GetCurrentTenantId(user);
    if (tenantId == null) return Results.BadRequest(new { Message = "Usuario sin tenant asignado" });

    // Validar que el producto existe Y pertenece al mismo tenant
    if (!await db.Products.AnyAsync(p => p.Id == dto.ProductId && p.TenantId == tenantId))
        return Results.BadRequest(new { Message = "El producto no existe" });

    if (!await db.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && w.TenantId == tenantId))
        return Results.BadRequest(new { Message = "El almacen no existe" });

    var current = await db.InventoryMovements
        .Where(m => m.ProductId == dto.ProductId && m.TenantId == tenantId)
        .SumAsync(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity);

    var delta = dto.Type == MovementType.In ? dto.Quantity : -dto.Quantity;

    if (current + delta < 0)
        return Results.BadRequest(new { Message = "Stock insuficiente para realizar esta salida" });

    dto.TenantId = tenantId.Value;
    db.InventoryMovements.Add(dto);
    await db.SaveChangesAsync();

    return Results.Created($"/movements/{dto.Id}", dto);
}).RequireAuthorization();
#endregion

#region Authentication
app.MapPost("/login", async (UserLogin login, UserManager<AppUser> userManager, SignInManager<AppUser> signInManager) =>
{
    var user = await userManager.FindByNameAsync(login.Username);
    if (user == null) return Results.Unauthorized();

    var result = await signInManager.CheckPasswordSignInAsync(user, login.Password, false);
    if (!result.Succeeded) return Results.Unauthorized();

    var roles = await userManager.GetRolesAsync(user);
    var role = roles.FirstOrDefault() ?? "User";

    var dashboardUrl = role == "Admin"
        ? "/views/admin/dashboard-admin.html"
        : role == "AdminTienda"
            ? "/views/adminTienda/dashboard-admintienda.html"
            : "/views/user/dashboard-user.html";

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id),
        new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
        new(ClaimTypes.NameIdentifier, user.Id),
        new(ClaimTypes.Name, user.UserName ?? ""),
        new(ClaimTypes.Role, role),
        new("TenantId", user.TenantId?.ToString() ?? ""),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var creds = new SigningCredentials(
        new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSigningKey)),
        SecurityAlgorithms.HmacSha256);

    var tokenObj = new JwtSecurityToken(
        issuer: jwtIssuer,
        audience: jwtAudience,
        claims: claims,
        expires: DateTime.UtcNow.AddHours(8),
        signingCredentials: creds);

    var jwt = new JwtSecurityTokenHandler().WriteToken(tokenObj);

    return Results.Ok(new
    {
        Message = "Login exitoso",
        Username = user.UserName,
        Role = role,
        RedirectUrl = dashboardUrl,
        Token = jwt,
        ExpiresAt = tokenObj.ValidTo
    });
});

app.MapPost("/auth/forgot-password", async (ForgotPasswordDto dto, UserManager<AppUser> userManager) =>
{
    var user = await userManager.FindByEmailAsync(dto.Email);
    if (user != null)
    {
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        Console.WriteLine($"[DEV] Reset token para {dto.Email}: {resetToken}");
    }
    return Results.Ok(new { Message = "Si el correo existe, se ha enviado un enlace de recuperacion." });
});

app.MapPost("/auth/reset-password", async (ResetPasswordDto dto, UserManager<AppUser> userManager) =>
{
    var user = await userManager.FindByEmailAsync(dto.Email);
    if (user == null)
        return Results.BadRequest(new { Message = "Usuario no encontrado" });

    var result = await userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

    if (result.Succeeded)
        return Results.Ok(new { Message = "Contrasena actualizada correctamente" });

    return Results.BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
});
#endregion

#region Tenant Management (solo Admin/SuperAdmin)
// Listar todas las tiendas
app.MapGet("/api/tenants", async (AppDbContext db) =>
{
    var tenants = await db.Tenants.AsNoTracking().ToListAsync();
    return Results.Ok(tenants);
}).RequireAuthorization(p => p.RequireRole("Admin"));

// Crear nueva tienda
app.MapPost("/api/tenants", async (CreateTenantDto dto, AppDbContext db) =>
{
    var tenant = new Tenant
    {
        Name = dto.Name,
        Slug = dto.Slug,
        Active = true
    };
    db.Tenants.Add(tenant);
    await db.SaveChangesAsync();
    return Results.Created($"/api/tenants/{tenant.Id}", tenant);
}).RequireAuthorization(p => p.RequireRole("Admin"));

// Crear AdminTienda para una tienda (el cliente)
app.MapPost("/api/tenants/{tenantId}/admins", async (
    int tenantId,
    CreateTenantAdminDto dto,
    AppDbContext db,
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager) =>
{
    if (!await db.Tenants.AnyAsync(t => t.Id == tenantId))
        return Results.NotFound(new { Message = "La tienda no existe" });

    if (await userManager.FindByNameAsync(dto.Username) != null)
        return Results.BadRequest(new { Message = "El nombre de usuario ya esta en uso" });

    if (await userManager.FindByEmailAsync(dto.Email) != null)
        return Results.BadRequest(new { Message = "El email ya esta registrado" });

    var password = GenerateRandomPassword();
    var newAdmin = new AppUser
    {
        UserName = dto.Username,
        Email = dto.Email,
        EmailConfirmed = true,
        TenantId = tenantId
    };

    var result = await userManager.CreateAsync(newAdmin, password);
    if (!result.Succeeded)
        return Results.BadRequest(new
        {
            Message = "Error al crear AdminTienda",
            Errors = result.Errors.Select(e => e.Description)
        });

    await userManager.AddToRoleAsync(newAdmin, "AdminTienda");

    return Results.Created($"/api/tenants/{tenantId}/admins/{newAdmin.Id}", new
    {
        id = newAdmin.Id,
        userName = newAdmin.UserName,
        email = newAdmin.Email,
        tenantId = tenantId,
        initialPassword = password
    });
}).RequireAuthorization(p => p.RequireRole("Admin"));

// Listar AdminsTienda de una tienda (solo SuperAdmin)
app.MapGet("/api/tenants/{tenantId}/admins", async (
    int tenantId,
    AppDbContext db,
    UserManager<AppUser> userManager) =>
{
    if (!await db.Tenants.AnyAsync(t => t.Id == tenantId))
        return Results.NotFound(new { Message = "La tienda no existe" });

    var users = await userManager.Users
        .Where(u => u.TenantId == tenantId)
        .ToListAsync();

    var list = new List<object>();
    foreach (var u in users)
    {
        var roles = await userManager.GetRolesAsync(u);
        if (roles.Contains("AdminTienda"))
        {
            list.Add(new
            {
                id = u.Id,
                userName = u.UserName,
                email = u.Email,
                tenantId = tenantId,
                tenantName = (await db.Tenants.FindAsync(tenantId))?.Name
            });
        }
    }
    return Results.Ok(list);
}).RequireAuthorization(p => p.RequireRole("Admin"));
#endregion

#region Tenant User Management (AdminTienda gestiona sus propios empleados)
// Listar usuarios de su tienda
app.MapGet("/api/tenant/users", async (AppDbContext db, UserManager<AppUser> userManager, ClaimsPrincipal user) =>
{
    var tenantId = GetCurrentTenantId(user);
    var users = await userManager.Users
        .Where(u => u.TenantId == tenantId)
        .ToListAsync();

    var list = new List<object>();
    foreach (var u in users)
    {
        var roles = await userManager.GetRolesAsync(u);
        list.Add(new
        {
            id = u.Id,
            userName = u.UserName,
            email = u.Email,
            emailConfirmed = u.EmailConfirmed,
            roles = roles
        });
    }
    return Results.Ok(list);
}).RequireAuthorization(p => p.RequireRole("AdminTienda"));

// Crear empleado (User) dentro de su tienda
app.MapPost("/api/tenant/users", async (
    CreateUserDto dto,
    AppDbContext db,
    UserManager<AppUser> userManager,
    ClaimsPrincipal user) =>
{
    var tenantId = GetCurrentTenantId(user);
    if (tenantId == null) return Results.BadRequest(new { Message = "Sin tenant" });

    if (await userManager.FindByNameAsync(dto.Username) != null)
        return Results.BadRequest(new { Message = "El nombre de usuario ya esta en uso" });

    if (await userManager.FindByEmailAsync(dto.Email) != null)
        return Results.BadRequest(new { Message = "El email ya esta registrado" });

    var newUser = new AppUser
    {
        UserName = dto.Username,
        Email = dto.Email,
        EmailConfirmed = true,
        TenantId = tenantId
    };

    var result = await userManager.CreateAsync(newUser, dto.Password);
    if (!result.Succeeded)
        return Results.BadRequest(new
        {
            Message = "Error al crear usuario",
            Errors = result.Errors.Select(e => e.Description)
        });

    await userManager.AddToRoleAsync(newUser, "User");

    return Results.Created($"/api/tenant/users/{newUser.Id}", new
    {
        id = newUser.Id,
        userName = newUser.UserName,
        email = newUser.Email
    });
}).RequireAuthorization(p => p.RequireRole("AdminTienda"));

// Eliminar empleado de su tienda
app.MapDelete("/api/tenant/users/{id}", async (
    string id,
    UserManager<AppUser> userManager,
    ClaimsPrincipal user) =>
{
    var tenantId = GetCurrentTenantId(user);

    var target = await userManager.FindByIdAsync(id);
    if (target == null || target.TenantId != tenantId)
        return Results.NotFound(new { Message = "Usuario no encontrado en tu tienda" });

    if (await userManager.IsInRoleAsync(target, "AdminTienda"))
        return Results.BadRequest(new { Message = "No puedes eliminar a un AdminTienda" });

    var result = await userManager.DeleteAsync(target);
    if (!result.Succeeded)
        return Results.BadRequest(new
        {
            Message = "Error al eliminar usuario",
            Errors = result.Errors.Select(e => e.Description)
        });

    return Results.Ok(new { Message = "Usuario eliminado exitosamente" });
}).RequireAuthorization(p => p.RequireRole("AdminTienda"));
#endregion

app.Run();

public record UserLogin(string Username, string Password);
public record ForgotPasswordDto(string Email);
public record ResetPasswordDto(string Email, string Token, string NewPassword);
public record CreateUserDto(string Username, string Email, string Password, string Role);
public record UpdateUserDto(string Username, string Email, string? Password, string Role);
public record CreateTenantDto(string Name, string? Slug);
public record CreateTenantAdminDto(string Username, string Email);