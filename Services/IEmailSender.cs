namespace InventarioApi.Services;

public interface IEmailSender
{
    Task SendPasswordResetEmailAsync(string email, string resetLink);
}
