namespace InventarioApi.Models.Dtos;

public record UserLoginDto(string Username, string Password);
public record ForgotPasswordDto(string Email);
public record ResetPasswordDto(string Email, string Token, string NewPassword);
public record CreateUserDto(string Username, string Email, string Password, string Role);
public record UpdateUserDto(string Username, string Email, string? Password, string Role);
public record CreateTenantDto(string Name, string? Slug);
public record CreateTenantAdminDto(string Username, string Email);
