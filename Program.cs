using InventarioApi.Data;
using InventarioApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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
    Console.WriteLine("INICIANDO CREACIÓN DE USUARIOS Y ROLES");
    Console.WriteLine("========================================");

    try
    {
        // Crear roles si no existen
        var roles = new[] { "Admin", "User" };
        foreach (var roleName in roles)
        {
            var roleExists = await roleManager.RoleExistsAsync(roleName);
            if (!roleExists)
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (roleResult.Succeeded)
                {
                    Console.WriteLine($"✅ Rol '{roleName}' creado exitosamente");
                }
                else
                {
                    Console.WriteLine($"❌ Error creando rol '{roleName}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                Console.WriteLine($"ℹ️  Rol '{roleName}' ya existe");
            }
        }

        // Crear usuario ADMIN
        Console.WriteLine("\n--- Creando usuario ADMIN ---");
        var adminUser = new AppUser
        {
            UserName = "admin",
            Email = "admin@inventario.com",
            EmailConfirmed = true
        };

        var existingAdmin = await userManager.FindByNameAsync(adminUser.UserName);
        if (existingAdmin == null)
        {
            var adminResult = await userManager.CreateAsync(adminUser, "Admin1234!");
            if (adminResult.Succeeded)
            {
                var addRoleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
                if (addRoleResult.Succeeded)
                {
                    Console.WriteLine("✅ Usuario 'admin' creado exitosamente con rol Admin");
                    Console.WriteLine($"   📧 Email: {adminUser.Email}");
                    Console.WriteLine($"   🔑 Password: Admin1234!");
                }
                else
                {
                    Console.WriteLine($"⚠️  Usuario 'admin' creado pero error al asignar rol: {string.Join(", ", addRoleResult.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                var errors = string.Join(", ", adminResult.Errors.Select(e => e.Description));
                Console.WriteLine($"❌ Error creando usuario admin: {errors}");
            }
        }
        else
        {
            Console.WriteLine("ℹ️  El usuario 'admin' ya existe");
            // Verificar que tenga el rol Admin
            var isInRole = await userManager.IsInRoleAsync(existingAdmin, "Admin");
            if (!isInRole)
            {
                await userManager.AddToRoleAsync(existingAdmin, "Admin");
                Console.WriteLine("✅ Rol 'Admin' asignado al usuario existente");
            }
        }

        // Crear usuario normal (USER)
        Console.WriteLine("\n--- Creando usuario USUARIO ---");
        var normalUser = new AppUser
        {
            UserName = "usuario",
            Email = "usuario@inventario.com",
            EmailConfirmed = true
        };

        var existingUser = await userManager.FindByNameAsync(normalUser.UserName);
        if (existingUser == null)
        {
            var userResult = await userManager.CreateAsync(normalUser, "User1234!");
            if (userResult.Succeeded)
            {
                var addRoleResult = await userManager.AddToRoleAsync(normalUser, "User");
                if (addRoleResult.Succeeded)
                {
                    Console.WriteLine("✅ Usuario 'usuario' creado exitosamente con rol User");
                    Console.WriteLine($"   📧 Email: {normalUser.Email}");
                    Console.WriteLine($"   🔑 Password: User1234!");
                }
                else
                {
                    Console.WriteLine($"⚠️  Usuario 'usuario' creado pero error al asignar rol: {string.Join(", ", addRoleResult.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                var errors = string.Join(", ", userResult.Errors.Select(e => e.Description));
                Console.WriteLine($"❌ Error creando usuario normal: {errors}");
                Console.WriteLine("💡 Detalles de los errores:");
                foreach (var error in userResult.Errors)
                {
                    Console.WriteLine($"   - {error.Code}: {error.Description}");
                }
            }
        }
        else
        {
            Console.WriteLine("ℹ️  El usuario 'usuario' ya existe");
            // Verificar que tenga el rol User
            var isInRole = await userManager.IsInRoleAsync(existingUser, "User");
            if (!isInRole)
            {
                await userManager.AddToRoleAsync(existingUser, "User");
                Console.WriteLine("✅ Rol 'User' asignado al usuario existente");
            }
        }

        Console.WriteLine("\n========================================");
        Console.WriteLine("RESUMEN DE USUARIOS DISPONIBLES:");
        Console.WriteLine("========================================");

        var allUsers = userManager.Users.ToList();
        foreach (var user in allUsers)
        {
            var userRoles = await userManager.GetRolesAsync(user);
            Console.WriteLine($"👤 {user.UserName} - Roles: {string.Join(", ", userRoles)}");
        }

        Console.WriteLine("========================================\n");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n❌ ERROR CRÍTICO en CreateDefaultUsers: {ex.Message}");
        Console.WriteLine($"   Stack Trace: {ex.StackTrace}");
    }
}
#endregion

// Middleware
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// ⚠️ IMPORTANTE: Middleware de redirección ANTES de UseStaticFiles
app.Use(async (context, next) =>
{
    // Redirigir raíz al login
    if (context.Request.Path == "/")
    {
        context.Response.Redirect("/login.html");
        return;
    }

    // Redirigir el formulario viejo al login (para que elija rol)
    if (context.Request.Path == "/FormularioInventario/formularioDeInventario.html")
    {
        context.Response.Redirect("/login.html");
        return;
    }

    await next();
});

app.UseStaticFiles();

// Configurar Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory API v1");
    c.RoutePrefix = "swagger";
});

// Mapear controladores (ProductsController se manejará automáticamente)
app.MapControllers();

// --- Minimal API Endpoints (solo los que NO están en controladores) ---

#region Categories
app.MapGet("/categories", async (AppDbContext db) =>
    await db.Categories.AsNoTracking().ToListAsync()
);

app.MapPost("/categories", async (Category dto, AppDbContext db) =>
{
    db.Categories.Add(dto);
    await db.SaveChangesAsync();
    return Results.Created($"/categories/{dto.Id}", dto);
});
#endregion

#region Warehouses
app.MapGet("/warehouses", async (AppDbContext db) =>
    await db.Warehouses.AsNoTracking().ToListAsync()
);

app.MapPost("/warehouses", async (Warehouse dto, AppDbContext db) =>
{
    db.Warehouses.Add(dto);
    await db.SaveChangesAsync();
    return Results.Created($"/warehouses/{dto.Id}", dto);
});
#endregion

#region Movements
app.MapGet("/movements", async (AppDbContext db) =>
    await db.InventoryMovements
        .Include(m => m.Product)
        .Include(m => m.Warehouse)
        .AsNoTracking()
        .OrderByDescending(m => m.MovementDate)
        .ToListAsync()
);

app.MapPost("/movements", async (InventoryMovement dto, AppDbContext db) =>
{
    // Validar que el producto existe
    if (!await db.Products.AnyAsync(p => p.Id == dto.ProductId))
        return Results.BadRequest(new { Message = "El producto no existe" });

    // Validar que el almacén existe
    if (!await db.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId))
        return Results.BadRequest(new { Message = "El almacén no existe" });

    var current = await db.InventoryMovements
        .Where(m => m.ProductId == dto.ProductId)
        .SumAsync(m => m.Type == MovementType.In ? m.Quantity : -m.Quantity);

    var delta = dto.Type == MovementType.In ? dto.Quantity : -dto.Quantity;

    if (current + delta < 0)
        return Results.BadRequest(new { Message = "Stock insuficiente para realizar esta salida" });

    db.InventoryMovements.Add(dto);
    await db.SaveChangesAsync();

    return Results.Created($"/movements/{dto.Id}", dto);
});
#endregion

#region Authentication
app.MapPost("/login", async (UserLogin login, UserManager<AppUser> userManager, SignInManager<AppUser> signInManager) =>
{
    var user = await userManager.FindByNameAsync(login.Username);
    if (user == null) return Results.Unauthorized();

    var result = await signInManager.CheckPasswordSignInAsync(user, login.Password, false);
    if (!result.Succeeded) return Results.Unauthorized();

    // Obtener el rol del usuario
    var roles = await userManager.GetRolesAsync(user);
    var role = roles.FirstOrDefault() ?? "User";

    // Determinar la ruta del dashboard según el rol
    var dashboardUrl = role == "Admin"
        ? "/views/admin/dashboard-admin.html"     // Dashboard Admin
        : "/views/user/dashboard-user.html";       // Dashboard User

    return Results.Ok(new
    {
        Message = "Login exitoso",
        Username = user.UserName,
        Role = role,
        RedirectUrl = dashboardUrl
    });
});

app.MapPost("/auth/forgot-password", async (ForgotPasswordDto dto, UserManager<AppUser> userManager) =>
{
    var user = await userManager.FindByEmailAsync(dto.Email);
    if (user == null)
        return Results.BadRequest(new { Message = "Correo no registrado" });

    var token = await userManager.GeneratePasswordResetTokenAsync(user);
    return Results.Ok(new { token });
});

app.MapPost("/auth/reset-password", async (ResetPasswordDto dto, UserManager<AppUser> userManager) =>
{
    var user = await userManager.FindByEmailAsync(dto.Email);
    if (user == null)
        return Results.BadRequest(new { Message = "Usuario no encontrado" });

    var result = await userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

    if (result.Succeeded)
        return Results.Ok(new { Message = "Contraseña actualizada correctamente" });

    return Results.BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
});
#endregion

// ENDPOINT TEMPORAL PARA FORZAR CREACIÓN DE USUARIO
app.MapPost("/admin/force-create-user", async (UserManager<AppUser> userManager) =>
{
    var normalUser = new AppUser
    {
        UserName = "usuario",
        Email = "usuario@inventario.com",
        EmailConfirmed = true
    };

    var existingUser = await userManager.FindByNameAsync(normalUser.UserName);
    if (existingUser != null)
    {
        return Results.Ok(new { Message = "El usuario 'usuario' ya existe", User = existingUser.UserName });
    }

    var result = await userManager.CreateAsync(normalUser, "User1234!");
    if (result.Succeeded)
    {
        await userManager.AddToRoleAsync(normalUser, "User");
        return Results.Ok(new { Message = "✅ Usuario 'usuario' creado exitosamente", Username = "usuario", Password = "User1234!" });
    }

    return Results.BadRequest(new { Message = "Error al crear usuario", Errors = result.Errors.Select(e => e.Description) });
});

#region User Management
// Obtener todos los usuarios
app.MapGet("/api/users", async (UserManager<AppUser> userManager) =>
{
    try
    {
        var users = userManager.Users.ToList();
        var usersWithRoles = new List<object>();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            usersWithRoles.Add(new
            {
                id = user.Id,
                userName = user.UserName,
                email = user.Email,
                emailConfirmed = user.EmailConfirmed,
                roles = roles
            });
        }

        return Results.Ok(usersWithRoles);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al obtener usuarios: {ex.Message}");
    }
});

// Crear nuevo usuario
app.MapPost("/api/users", async (CreateUserDto dto, UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager) =>
{
    try
    {
        // Validar que el rol existe
        if (!await roleManager.RoleExistsAsync(dto.Role))
        {
            return Results.BadRequest(new { Message = $"El rol '{dto.Role}' no existe" });
        }

        // Verificar si el usuario ya existe
        var existingUser = await userManager.FindByNameAsync(dto.Username);
        if (existingUser != null)
        {
            return Results.BadRequest(new { Message = "El nombre de usuario ya está en uso" });
        }

        var existingEmail = await userManager.FindByEmailAsync(dto.Email);
        if (existingEmail != null)
        {
            return Results.BadRequest(new { Message = "El email ya está registrado" });
        }

        // Crear el usuario
        var newUser = new AppUser
        {
            UserName = dto.Username,
            Email = dto.Email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(newUser, dto.Password);

        if (!result.Succeeded)
        {
            return Results.BadRequest(new
            {
                Message = "Error al crear usuario",
                Errors = result.Errors.Select(e => e.Description)
            });
        }

        // Asignar rol
        var roleResult = await userManager.AddToRoleAsync(newUser, dto.Role);
        if (!roleResult.Succeeded)
        {
            // Si falla asignar el rol, eliminar el usuario creado
            await userManager.DeleteAsync(newUser);
            return Results.BadRequest(new
            {
                Message = "Error al asignar rol al usuario",
                Errors = roleResult.Errors.Select(e => e.Description)
            });
        }

        var roles = await userManager.GetRolesAsync(newUser);

        return Results.Created($"/api/users/{newUser.Id}", new
        {
            id = newUser.Id,
            userName = newUser.UserName,
            email = newUser.Email,
            emailConfirmed = newUser.EmailConfirmed,
            roles = roles
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al crear usuario: {ex.Message}");
    }
});

// Actualizar usuario
app.MapPut("/api/users/{id}", async (string id, UpdateUserDto dto, UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager) =>
{
    try
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return Results.NotFound(new { Message = "Usuario no encontrado" });
        }

        // Actualizar datos básicos
        user.Email = dto.Email;
        user.UserName = dto.Username;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Results.BadRequest(new
            {
                Message = "Error al actualizar usuario",
                Errors = updateResult.Errors.Select(e => e.Description)
            });
        }

        // Actualizar contraseña si se proporcionó
        if (!string.IsNullOrEmpty(dto.Password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await userManager.ResetPasswordAsync(user, token, dto.Password);

            if (!passwordResult.Succeeded)
            {
                return Results.BadRequest(new
                {
                    Message = "Error al actualizar contraseña",
                    Errors = passwordResult.Errors.Select(e => e.Description)
                });
            }
        }

        // Actualizar rol si cambió
        var currentRoles = await userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(dto.Role))
        {
            // Validar que el nuevo rol existe
            if (!await roleManager.RoleExistsAsync(dto.Role))
            {
                return Results.BadRequest(new { Message = $"El rol '{dto.Role}' no existe" });
            }

            // Remover roles actuales
            if (currentRoles.Any())
            {
                await userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            // Agregar nuevo rol
            var roleResult = await userManager.AddToRoleAsync(user, dto.Role);
            if (!roleResult.Succeeded)
            {
                return Results.BadRequest(new
                {
                    Message = "Error al actualizar rol",
                    Errors = roleResult.Errors.Select(e => e.Description)
                });
            }
        }

        var roles = await userManager.GetRolesAsync(user);

        return Results.Ok(new
        {
            id = user.Id,
            userName = user.UserName,
            email = user.Email,
            emailConfirmed = user.EmailConfirmed,
            roles = roles
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al actualizar usuario: {ex.Message}");
    }
});

// Eliminar usuario
app.MapDelete("/api/users/{id}", async (string id, UserManager<AppUser> userManager) =>
{
    try
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return Results.NotFound(new { Message = "Usuario no encontrado" });
        }

        // Prevenir eliminar el último admin
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains("Admin"))
        {
            var allAdmins = await userManager.GetUsersInRoleAsync("Admin");
            if (allAdmins.Count <= 1)
            {
                return Results.BadRequest(new { Message = "No se puede eliminar el último administrador del sistema" });
            }
        }

        var result = await userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            return Results.BadRequest(new
            {
                Message = "Error al eliminar usuario",
                Errors = result.Errors.Select(e => e.Description)
            });
        }

        return Results.Ok(new { Message = "Usuario eliminado exitosamente" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al eliminar usuario: {ex.Message}");
    }
});

// Obtener usuario por ID
app.MapGet("/api/users/{id}", async (string id, UserManager<AppUser> userManager) =>
{
    try
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return Results.NotFound(new { Message = "Usuario no encontrado" });
        }

        var roles = await userManager.GetRolesAsync(user);

        return Results.Ok(new
        {
            id = user.Id,
            userName = user.UserName,
            email = user.Email,
            emailConfirmed = user.EmailConfirmed,
            roles = roles
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error al obtener usuario: {ex.Message}");
    }
});
#endregion

app.Run();

public record UserLogin(string Username, string Password);
public record ForgotPasswordDto(string Email);
public record ResetPasswordDto(string Email, string Token, string NewPassword);
// DTOs para User Management
public record CreateUserDto(string Username, string Email, string Password, string Role);
public record UpdateUserDto(string Username, string Email, string? Password, string Role);