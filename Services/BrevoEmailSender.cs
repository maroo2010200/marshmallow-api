using System.Text;
using System.Text.Json;

public class BrevoEmailSender : IBrevoEmailSender
{
    private const string BrevoEndpoint = "https://api.brevo.com/v3/smtp/email";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BrevoEmailSender> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(int userId, string email, string verificationToken, string? recipientName = null)
    {
        var code = verificationToken.Trim();
        var greetingName = !string.IsNullOrWhiteSpace(recipientName) ? recipientName.Trim() : "there";
        var subject = $"{code} is your Marshmallow verification code";

        var bodyText = $"Hi {greetingName},\n\n" +
                       $"Your Marshmallow verification code is:\n\n" +
                       $"{code}\n\n" +
                       $"Enter this 6-digit code on the verification screen to activate your account.\n" +
                       $"This code will expire in 15 minutes.\n\n" +
                       $"If you didn't request this code, you can safely ignore this email.\n";

        var htmlContent = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{code} is your Marshmallow verification code</title>
</head>
<body style=""margin: 0; padding: 24px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #FAFAFA; color: #111827;"">
    <div style=""max-width: 460px; margin: 0 auto; background-color: #FFFFFF; border: 1px solid #E5E7EB; border-radius: 12px; padding: 32px 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05);"">
        <h2 style=""margin: 0 0 16px; font-size: 20px; font-weight: 600; color: #111827;"">Confirm your email address</h2>
        <p style=""margin: 0 0 24px; font-size: 15px; line-height: 1.5; color: #374151;"">
            Hi {greetingName},<br><br>
            Please enter the following 6-digit verification code to complete your Marshmallow registration:
        </p>
        <div style=""background-color: #F3F4F6; border: 1px solid #E5E7EB; border-radius: 8px; padding: 20px; text-align: center; margin: 0 0 24px;"">
            <span style=""font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; font-size: 34px; font-weight: 700; letter-spacing: 8px; color: #111827;"">{code}</span>
        </div>
        <p style=""margin: 0 0 16px; font-size: 13px; line-height: 1.5; color: #6B7280;"">
            This code expires in 15 minutes. Never share this code with anyone.
        </p>
        <hr style=""border: none; border-top: 1px solid #E5E7EB; margin: 24px 0 16px;"" />
        <p style=""margin: 0; font-size: 12px; line-height: 1.4; color: #9CA3AF;"">
            If you didn't request a verification code, you can safely ignore this email.
        </p>
    </div>
</body>
</html>";

        await SendEmailAsync(email, subject, htmlContent, bodyText, recipientName, ["otp-verification"]);
    }

    public async Task SendPasswordResetEmailAsync(int userId, string email, string resetToken, string? recipientName = null)
    {
        var encodedToken = Uri.EscapeDataString(resetToken);
        var frontendResetUrl = $"{GetFrontendBaseUrl()}/reset-password?token={encodedToken}";

        var greetingName = !string.IsNullOrWhiteSpace(recipientName) ? recipientName.Trim() : null;
        var subject = greetingName != null
            ? $"{greetingName}, reset your Marshmallow password"
            : "Reset your Marshmallow password";

        var bodyHtml = greetingName != null
            ? $"Hi {greetingName},<br><br>Click the button below to choose a new password for your Marshmallow account."
            : "Click the button below to choose a new password for your Marshmallow account.";

        var bodyText = greetingName != null
            ? $"Hi {greetingName},\n\nClick the link below to choose a new password for your Marshmallow account."
            : "Click the link below to choose a new password for your Marshmallow account.";

        var htmlContent = BuildBrandedEmailHtml(
            heading: "Reset Your Password 🔑",
            subtitle: "We received a password reset request for your account.",
            bodyHtml: bodyHtml,
            ctaLabel: "Reset Password",
            ctaUrl: frontendResetUrl,
            footerText: "If you didn't request a password reset, you can safely ignore this email.");

        var plainTextContent = BuildPlainText(
            heading: "Reset Your Password",
            subtitle: "We received a password reset request for your account.",
            bodyText: bodyText,
            ctaLabel: "Reset Password",
            ctaUrl: frontendResetUrl,
            footerText: "If you didn't request a password reset, you can safely ignore this email.");

        await SendEmailAsync(email, subject, htmlContent, plainTextContent, greetingName, ["password-reset"]);
    }

    private string GetFrontendBaseUrl()
    {
        var configured = _configuration["Frontend:BaseUrl"]
            ?? _configuration["Frontend__BaseUrl"]
            ?? Environment.GetEnvironmentVariable("Frontend__BaseUrl")
            ?? Environment.GetEnvironmentVariable("Frontend:BaseUrl");

        if (!string.IsNullOrWhiteSpace(configured) && !configured.Contains("localhost"))
        {
            return configured.TrimEnd('/');
        }

        return "https://marshmallow-react.vercel.app";
    }

    private static string BuildPlainText(
        string heading,
        string subtitle,
        string bodyText,
        string ctaLabel,
        string ctaUrl,
        string footerText)
    {
        return $"{heading}\n" +
               $"{subtitle}\n\n" +
               $"{bodyText}\n\n" +
               $"{ctaLabel}: {ctaUrl}\n\n" +
               $"{footerText}\n";
    }

    private static string BuildBrandedEmailHtml(
        string heading,
        string subtitle,
        string bodyHtml,
        string ctaLabel,
        string ctaUrl,
        string footerText)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
    <title>{heading}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #FAF4EB; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;"">
    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color: #FAF4EB; padding: 24px 12px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width: 520px; background-color: #FAF4EB; border-radius: 16px; border: 1px solid #E9B97C; padding: 24px;"">
                    <!-- Header -->
                    <tr>
                        <td align=""center"" style=""padding-bottom: 20px;"">
                            <h1 style=""color: #451A03; font-size: 24px; margin: 0 0 8px 0; font-weight: 700;"">{heading}</h1>
                            <p style=""color: #78350F; font-size: 14px; margin: 0;"">{subtitle}</p>
                        </td>
                    </tr>
                    <!-- Card Body -->
                    <tr>
                        <td style=""background-color: #FFFFFF; padding: 28px 24px; border-radius: 12px; border: 1px solid #E9B97C; text-align: center;"">
                            <p style=""color: #451A03; font-size: 15px; line-height: 1.6; margin: 0 0 24px 0;"">
                                {bodyHtml}
                            </p>
                            <!-- Bulletproof Button -->
                            <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"" align=""center"">
                                <tr>
                                    <td align=""center"" style=""border-radius: 9999px; background-color: #C96B32;"">
                                        <a href=""{ctaUrl}"" target=""_blank"" style=""display: inline-block; padding: 14px 32px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; font-size: 15px; font-weight: bold; color: #FFFFFF; text-decoration: none; border-radius: 9999px; background-color: #C96B32; border: 1px solid #C96B32;"">
                                            {ctaLabel}
                                        </a>
                                    </td>
                                </tr>
                            </table>
                            <!-- Fallback Link without raw naked query tokens -->
                            <div style=""margin-top: 24px; padding-top: 18px; border-top: 1px dashed #E9B97C; text-align: center;"">
                                <p style=""color: #78350F; font-size: 12px; line-height: 1.5; margin: 0;"">
                                    Button not loading? <a href=""{ctaUrl}"" target=""_blank"" style=""color: #C96B32; font-weight: 600; text-decoration: underline;"">Open confirmation link in browser</a>
                                </p>
                            </div>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td align=""center"" style=""padding-top: 20px;"">
                            <p style=""color: #92400E; font-size: 12px; margin: 0; line-height: 1.4;"">
                                {footerText}
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlContent,
        string plainTextContent,
        string? recipientName = null,
        string[]? tags = null)
    {
        var apiKey = _configuration["Brevo:ApiKey"]
            ?? _configuration["Brevo__ApiKey"]
            ?? Environment.GetEnvironmentVariable("Brevo__ApiKey")
            ?? Environment.GetEnvironmentVariable("Brevo:ApiKey");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Brevo API key is not configured. Please set Brevo:ApiKey or Brevo__ApiKey.");
        }

        var senderEmail = _configuration["Brevo:SenderEmail"]
            ?? _configuration["Brevo__SenderEmail"]
            ?? Environment.GetEnvironmentVariable("Brevo__SenderEmail")
            ?? Environment.GetEnvironmentVariable("Brevo:SenderEmail")
            ?? "maroo30303030@gmail.com";

        var senderName = _configuration["Brevo:SenderName"]
            ?? _configuration["Brevo__SenderName"]
            ?? Environment.GetEnvironmentVariable("Brevo__SenderName")
            ?? Environment.GetEnvironmentVariable("Brevo:SenderName")
            ?? "Marshmallow";

        var emailData = new
        {
            sender = new
            {
                name = senderName,
                email = senderEmail
            },
            to = new[]
            {
                new
                {
                    email = toEmail,
                    name = !string.IsNullOrWhiteSpace(recipientName) ? recipientName.Trim() : "Marshmallow User"
                }
            },
            replyTo = new
            {
                email = senderEmail,
                name = senderName
            },
            subject,
            htmlContent,
            textContent = plainTextContent,
            tags = tags ?? ["transactional"]
        };

        var json = JsonSerializer.Serialize(emailData);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, BrevoEndpoint);
        request.Headers.Add("api-key", apiKey);
        request.Content = content;

        using var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Brevo API delivery failed for {Email}: {StatusCode} - {Error}", toEmail, response.StatusCode, error);
            throw new InvalidOperationException($"Brevo API error ({response.StatusCode}): {error}");
        }

        _logger.LogInformation("Email successfully delivered to {Email} via Brevo HTTP API (Tags: {Tags}).", toEmail, string.Join(", ", tags ?? ["transactional"]));
    }
}