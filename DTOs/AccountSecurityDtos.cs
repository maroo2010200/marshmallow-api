using System.ComponentModel.DataAnnotations;

namespace MarbookApi.DTOs;

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(6, ErrorMessage = "New password must be at least 6 characters long.")]
    public string NewPassword { get; set; } = string.Empty;

    [RegularExpression(@"^\d{6}$", ErrorMessage = "Verification code must be exactly 6 digits.")]
    public string? Code { get; set; }
}

public class DeleteAccountDto
{
    [Required]
    public string Password { get; set; } = string.Empty;
}

