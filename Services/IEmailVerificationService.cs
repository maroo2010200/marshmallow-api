public interface IEmailVerificationService
{
    Task<string> GenerateTokenAsync(int userId);
    Task<int?> VerifyTokenAsync(string token);
    Task<(bool Success, string? Token, string? Message)> ResendVerificationEmailAsync(int userId, string email);
}