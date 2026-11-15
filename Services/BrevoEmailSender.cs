using System.Text;
using System.Text.Json;

public class BrevoEmailSender : IBrevoEmailSender
{
    private const string BrevoEndpoint = "https://api.brevo.com/v3/smtp/email";

    private static string? _cachedAccessToken;
    private static DateTime _tokenExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);

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
                       $"Enter this code on the verification screen to activate your account. It will expire in 15 minutes.\n\n" +
                       $"If you didn't request this code, you can safely ignore this email.\n";

        var htmlContent = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
</head>
<body style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #222222; font-size: 15px; line-height: 1.6; padding: 16px;"">
    <p>Hi {greetingName},</p>
    <p>Your Marshmallow verification code is:</p>
    <p style=""font-size: 32px; font-weight: bold; letter-spacing: 6px; font-family: monospace; color: #111111; margin: 20px 0;"">{code}</p>
    <p>Enter this code on the verification screen to activate your account. It will expire in 15 minutes.</p>
    <p style=""color: #777777; font-size: 13px; margin-top: 32px;"">If you didn't request this code, you can safely ignore this email.</p>
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

    public async Task SendPasswordChangeOtpEmailAsync(int userId, string email, string code, string? recipientName = null)
    {
        var cleanCode = code.Trim();
        var greetingName = !string.IsNullOrWhiteSpace(recipientName) ? recipientName.Trim() : "there";
        var subject = $"{cleanCode} is your Marshmallow security code";

        var bodyText = $"Hi {greetingName},\n\n" +
                       $"We received a request to change the password for your Marshmallow account.\n\n" +
                       $"Your verification code is:\n\n" +
                       $"{cleanCode}\n\n" +
                       $"Enter this code on the screen to confirm your new password. It will expire in 15 minutes.\n\n" +
                       $"If you did not request this change, please change your password immediately or contact support.\n";

        var htmlContent = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
</head>
<body style=""font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #222222; font-size: 15px; line-height: 1.6; padding: 16px;"">
    <p>Hi {greetingName},</p>
    <p>We received a request to change your Marshmallow account password.</p>
    <p>Your verification code is:</p>
    <p style=""font-size: 32px; font-weight: bold; letter-spacing: 6px; font-family: monospace; color: #111111; margin: 20px 0;"">{cleanCode}</p>
    <p>Enter this code on the screen to confirm your new password. It will expire in 15 minutes.</p>
    <p style=""color: #777777; font-size: 13px; margin-top: 32px;"">If you didn't request this change, please secure your account immediately.</p>
</body>
</html>";

        await SendEmailAsync(email, subject, htmlContent, bodyText, recipientName, ["security-otp"]);
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

    private async Task<string> GetGmailAccessTokenAsync(string clientId, string clientSecret, string refreshToken)
    {
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _cachedAccessToken;
        }

        await _tokenLock.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _cachedAccessToken;
            }

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["refresh_token"] = refreshToken,
                    ["grant_type"] = "refresh_token"
                })
            };

            using var response = await _httpClient.SendAsync(tokenRequest);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to refresh Gmail access token: {responseJson}");
            }

            using var doc = JsonDocument.Parse(responseJson);
            var accessToken = doc.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("access_token was null in Google OAuth response.");

            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expProp) ? expProp.GetInt32() : 3600;

            _cachedAccessToken = accessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 120));

            return accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string EncodeHeaderValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.All(c => c <= 127)) return value;
        var bytes = Encoding.UTF8.GetBytes(value);
        return $"=?utf-8?B?{Convert.ToBase64String(bytes)}?=";
    }

    private async Task SendViaGmailApiAsync(
        string toEmail,
        string subject,
        string htmlContent,
        string plainTextContent,
        string clientId,
        string clientSecret,
        string refreshToken,
        string senderEmail,
        string? recipientName = null)
    {
        var accessToken = await GetGmailAccessTokenAsync(clientId, clientSecret, refreshToken);

        var boundary = "----=_Part_" + Guid.NewGuid().ToString("N");
        var dateHeader = DateTime.UtcNow.ToString("r");
        var messageId = $"<{Guid.NewGuid():N}.{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}@{senderEmail.Split('@')[1]}>";

        var formattedTo = !string.IsNullOrWhiteSpace(recipientName)
            ? $"{EncodeHeaderValue(recipientName.Trim())} <{toEmail}>"
            : toEmail;

        var sb = new StringBuilder();
        sb.Append($"From: Marshmallow <{senderEmail}>\r\n");
        sb.Append($"To: {formattedTo}\r\n");
        sb.Append($"Subject: {EncodeHeaderValue(subject)}\r\n");
        sb.Append($"Date: {dateHeader}\r\n");
        sb.Append($"Message-ID: {messageId}\r\n");
        sb.Append("MIME-Version: 1.0\r\n");
        sb.Append($"Content-Type: multipart/alternative; boundary=\"{boundary}\"\r\n\r\n");

        // Part 1: Plain-Text
        sb.Append($"--{boundary}\r\n");
        sb.Append("Content-Type: text/plain; charset=utf-8\r\n");
        sb.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
        sb.Append(plainTextContent + "\r\n\r\n");

        // Part 2: HTML
        sb.Append($"--{boundary}\r\n");
        sb.Append("Content-Type: text/html; charset=utf-8\r\n");
        sb.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
        sb.Append(htmlContent + "\r\n\r\n");

        sb.Append($"--{boundary}--\r\n");

        var rawBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var base64Url = Convert.ToBase64String(rawBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var payload = JsonSerializer.Serialize(new { raw = base64Url });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Gmail API error ({response.StatusCode}): {err}");
        }

        _logger.LogInformation("Email successfully delivered to {Email} via Google Gmail REST API.", toEmail);
    }

    private async Task SendViaBrevoApiAsync(
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
            throw new InvalidOperationException("Brevo API key is not configured.");
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

    private async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlContent,
        string plainTextContent,
        string? recipientName = null,
        string[]? tags = null)
    {
        var gmailClientId = _configuration["Gmail:ClientId"]
            ?? _configuration["Gmail__ClientId"]
            ?? Environment.GetEnvironmentVariable("Gmail__ClientId")
            ?? Environment.GetEnvironmentVariable("Gmail:ClientId");

        var gmailClientSecret = _configuration["Gmail:ClientSecret"]
            ?? _configuration["Gmail__ClientSecret"]
            ?? Environment.GetEnvironmentVariable("Gmail__ClientSecret")
            ?? Environment.GetEnvironmentVariable("Gmail:ClientSecret");

        var gmailRefreshToken = _configuration["Gmail:RefreshToken"]
            ?? _configuration["Gmail__RefreshToken"]
            ?? Environment.GetEnvironmentVariable("Gmail__RefreshToken")
            ?? Environment.GetEnvironmentVariable("Gmail:RefreshToken");

        var gmailSenderEmail = _configuration["Gmail:SenderEmail"]
            ?? _configuration["Gmail__SenderEmail"]
            ?? Environment.GetEnvironmentVariable("Gmail__SenderEmail")
            ?? "maroo30303030@gmail.com";

        // Priority 1: Google Gmail REST API (Direct Google DKIM/SPF from authentic @gmail.com)
        if (!string.IsNullOrWhiteSpace(gmailClientId) &&
            !string.IsNullOrWhiteSpace(gmailClientSecret) &&
            !string.IsNullOrWhiteSpace(gmailRefreshToken))
        {
            try
            {
                _logger.LogInformation("Dispatching email to {Email} via Google Gmail REST API (Port 443)...", toEmail);
                await SendViaGmailApiAsync(toEmail, subject, htmlContent, plainTextContent, gmailClientId, gmailClientSecret, gmailRefreshToken, gmailSenderEmail, recipientName);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gmail REST API dispatch failed for {Email}. Falling back to Brevo HTTP API...", toEmail);
            }
        }

        // Priority 2: Brevo HTTP API (Fallback)
        _logger.LogInformation("Dispatching email to {Email} via Brevo HTTP API (Port 443)...", toEmail);
        await SendViaBrevoApiAsync(toEmail, subject, htmlContent, plainTextContent, recipientName, tags);
    }
}