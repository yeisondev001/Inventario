using InventarioApi.Data;
using InventarioApi.Models;
using InventarioApi.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace InventarioApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class TenantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public TenantsController(
        AppDbContext db,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenants = await _db.Tenants.AsNoTracking().ToListAsync();
        return Ok(tenants);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantDto dto)
    {
        var tenant = new Tenant
        {
            Name = dto.Name,
            Slug = dto.Slug,
            Active = true
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { id = tenant.Id }, tenant);
    }

    [HttpGet("{tenantId}/admins")]
    public async Task<IActionResult> GetAdmins(int tenantId)
    {
        if (!await _db.Tenants.AnyAsync(t => t.Id == tenantId))
            return NotFound(new { Message = "La tienda no existe" });

        var users = await _userManager.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();

        var list = new List<object>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            if (roles.Contains("AdminTienda"))
            {
                list.Add(new
                {
                    id = u.Id,
                    userName = u.UserName,
                    email = u.Email,
                    tenantId = tenantId,
                    tenantName = (await _db.Tenants.FindAsync(tenantId))?.Name
                });
            }
        }
        return Ok(list);
    }

    [HttpPost("{tenantId}/admins")]
    public async Task<IActionResult> CreateAdmin(int tenantId, [FromBody] CreateTenantAdminDto dto)
    {
        if (!await _db.Tenants.AnyAsync(t => t.Id == tenantId))
            return NotFound(new { Message = "La tienda no existe" });

        if (await _userManager.FindByNameAsync(dto.Username) != null)
            return BadRequest(new { Message = "El nombre de usuario ya esta en uso" });

        if (await _userManager.FindByEmailAsync(dto.Email) != null)
            return BadRequest(new { Message = "El email ya esta registrado" });

        var password = GenerateRandomPassword();
        var newAdmin = new AppUser
        {
            UserName = dto.Username,
            Email = dto.Email,
            EmailConfirmed = true,
            TenantId = tenantId
        };

        var result = await _userManager.CreateAsync(newAdmin, password);
        if (!result.Succeeded)
            return BadRequest(new
            {
                Message = "Error al crear AdminTienda",
                Errors = result.Errors.Select(e => e.Description)
            });

        await _userManager.AddToRoleAsync(newAdmin, "AdminTienda");

        return CreatedAtAction(nameof(GetAdmins), new { tenantId, id = newAdmin.Id }, new
        {
            id = newAdmin.Id,
            userName = newAdmin.UserName,
            email = newAdmin.Email,
            tenantId = tenantId,
            initialPassword = password
        });
    }

    private static string GenerateRandomPassword()
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
}
