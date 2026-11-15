public interface IBrevoEmailSender
{
    Task SendVerificationEmailAsync(int userId, string email, string verificationToken, string? recipientName = null);
    Task SendPasswordResetEmailAsync(int userId, string email, string resetToken, string? recipientName = null);
    Task SendPasswordChangeOtpEmailAsync(int userId, string email, string code, string? recipientName = null);
}