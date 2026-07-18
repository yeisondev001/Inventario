using InventarioApi.Data;
using InventarioApi.Models;
using InventarioApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = false;
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

builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "sqlserver",
        tags: new[] { "db", "sql", "sqlserver" });

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

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
            // Password fijo para desarrollo/pruebas
            // IMPORTANTE: Cambiar despues del primer login en produccion
            var password = "Yeison123!";
            var adminResult = await userManager.CreateAsync(adminUser, password);
            if (adminResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                Console.WriteLine("========================================");
                Console.WriteLine("SUPERADMIN CREADO:");
                Console.WriteLine($"  Usuario: admin");
                Console.WriteLine($"  Password: {password}");
                Console.WriteLine("  (CAMBIA ESTA CONTRASENA DESPUES DEL PRIMER LOGIN)");
                Console.WriteLine("========================================");
            }
            else
            {
                Console.WriteLine($"Error creando SuperAdmin: {string.Join(", ", adminResult.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            // Eliminar el admin existente y recrearlo con la contraseña correcta
            var deleteResult = await userManager.DeleteAsync(existingAdmin);
            
            if (deleteResult.Succeeded)
            {
                var password = "Yeison123!";
                var newAdmin = new AppUser
                {
                    UserName = "admin",
                    Email = "admin@inventario.com",
                    EmailConfirmed = true,
                    TenantId = null
                };
                
                var createResult = await userManager.CreateAsync(newAdmin, password);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                    Console.WriteLine("========================================");
                    Console.WriteLine("SUPERADMIN RECREADO:");
                    Console.WriteLine($"  Usuario: admin");
                    Console.WriteLine($"  Password: {password}");
                    Console.WriteLine("  (CAMBIA ESTA CONTRASENA DESPUES DEL PRIMER LOGIN)");
                    Console.WriteLine("========================================");
                }
                else
                {
                    Console.WriteLine($"Error recreando SuperAdmin: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                Console.WriteLine($"Error eliminando SuperAdmin anterior: {string.Join(", ", deleteResult.Errors.Select(e => e.Description))}");
            }
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
app.UseMiddleware<InventarioApi.Middleware.GlobalExceptionMiddleware>();
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

app.MapHealthChecks("/health");

app.Run();