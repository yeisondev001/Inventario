using InventarioApi.Models;
using InventarioApi.Models.Dtos;
using InventarioApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace InventarioApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IEmailSender emailSender,
        IConfiguration config,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _config = config;
        _logger = logger;
    }

    [HttpPost("/login")]
    public async Task<IActionResult> Login([FromBody] UserLoginDto login)
    {
        var user = await _userManager.FindByNameAsync(login.Username);
        if (user == null) return Unauthorized();

        var result = await _signInManager.CheckPasswordSignInAsync(user, login.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return StatusCode(423, new { Message = "Cuenta bloqueada por demasiados intentos fallidos. Intenta en 15 minutos." });

        if (!result.Succeeded) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "User";

        var dashboardUrl = role == "Admin"
            ? "/views/admin/dashboard-admin.html"
            : role == "AdminTienda"
                ? "/views/adminTienda/dashboard-admintienda.html"
                : "/views/user/dashboard-user.html";

        var jwtSigningKey = _config["JWT:SigningKey"]!;
        var jwtIssuer = _config["JWT:Issuer"] ?? "InventarioApi";
        var jwtAudience = _config["JWT:Audience"] ?? "InventarioApi";

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

        return Ok(new
        {
            Message = "Login exitoso",
            Username = user.UserName,
            Role = role,
            RedirectUrl = dashboardUrl,
            Token = jwt,
            ExpiresAt = tokenObj.ValidTo
        });
    }

    [HttpPost("/auth/forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user != null)
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = Uri.EscapeDataString(resetToken);
            var appUrl = _config["AppUrl"] ?? "http://localhost:5213";
            var resetLink = $"{appUrl}/reset-password.html?email={Uri.EscapeDataString(dto.Email)}&token={encodedToken}";

            try
            {
                await _emailSender.SendPasswordResetEmailAsync(dto.Email, resetLink);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar email de recuperación a {Email}", dto.Email);
                Console.WriteLine($"[DEV] Reset token para {dto.Email}: {resetToken}");
            }
        }
        return Ok(new { Message = "Si el correo existe, se ha enviado un enlace de recuperacion." });
    }

    [HttpPost("/auth/reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return BadRequest(new { Message = "Usuario no encontrado" });

        var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

        if (result.Succeeded)
            return Ok(new { Message = "Contrasena actualizada correctamente" });

        return BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}
