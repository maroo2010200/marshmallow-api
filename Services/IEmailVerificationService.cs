public interface IEmailVerificationService
{
    Task<string> GenerateTokenAsync(int userId);
    Task SendVerificationEmailAsync(int userId, string email, string token, string? recipientName = null);
    Task<int?> VerifyTokenAsync(string token);
    Task<int?> VerifyCodeAsync(string email, string code);
    Task<(bool Success, string? Token, string? Message)> ResendVerificationEmailAsync(int userId, string email);
}