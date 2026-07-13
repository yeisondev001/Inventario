using InventarioApi.Data;
using InventarioApi.Models;
using InventarioApi.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventarioApi.Controllers;

[ApiController]
[Route("api/tenant/users")]
[Authorize(Roles = "AdminTienda")]
public class TenantUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public TenantUsersController(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private int? GetTenantId()
    {
        var claim = User.FindFirst("TenantId");
        return int.TryParse(claim?.Value, out var id) ? id : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = GetTenantId();
        var users = await _userManager.Users
            .Where(u => u.TenantId == tenantId)
            .ToListAsync();

        var list = new List<object>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            list.Add(new
            {
                id = u.Id,
                userName = u.UserName,
                email = u.Email,
                emailConfirmed = u.EmailConfirmed,
                roles = roles
            });
        }
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        var tenantId = GetTenantId();
        if (tenantId == null) return BadRequest(new { Message = "Sin tenant" });

        if (await _userManager.FindByNameAsync(dto.Username) != null)
            return BadRequest(new { Message = "El nombre de usuario ya esta en uso" });

        if (await _userManager.FindByEmailAsync(dto.Email) != null)
            return BadRequest(new { Message = "El email ya esta registrado" });

        var newUser = new AppUser
        {
            UserName = dto.Username,
            Email = dto.Email,
            EmailConfirmed = true,
            TenantId = tenantId
        };

        var result = await _userManager.CreateAsync(newUser, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new
            {
                Message = "Error al crear usuario",
                Errors = result.Errors.Select(e => e.Description)
            });

        await _userManager.AddToRoleAsync(newUser, "User");

        return CreatedAtAction(nameof(GetAll), new { id = newUser.Id }, new
        {
            id = newUser.Id,
            userName = newUser.UserName,
            email = newUser.Email
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var tenantId = GetTenantId();

        var target = await _userManager.FindByIdAsync(id);
        if (target == null || target.TenantId != tenantId)
            return NotFound(new { Message = "Usuario no encontrado en tu tienda" });

        if (await _userManager.IsInRoleAsync(target, "AdminTienda"))
        {
            var adminTiendaCount = 0;
            foreach (var u in await _userManager.Users.Where(u => u.TenantId == tenantId).ToListAsync())
            {
                if (await _userManager.IsInRoleAsync(u, "AdminTienda"))
                    adminTiendaCount++;
            }

            if (adminTiendaCount <= 1)
                return BadRequest(new { Message = "No se puede eliminar el ultimo AdminTienda de la tienda" });
        }

        var result = await _userManager.DeleteAsync(target);
        if (!result.Succeeded)
            return BadRequest(new
            {
                Message = "Error al eliminar usuario",
                Errors = result.Errors.Select(e => e.Description)
            });

        return Ok(new { Message = "Usuario eliminado exitosamente" });
    }
}
