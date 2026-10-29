using System.ComponentModel.DataAnnotations;

namespace HelpDeskPro.Dtos;

public class RegisterDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";
}

public class LoginDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public record AuthResponseDto(string Token, DateTime ExpiresAt, int UserId, string Name, string Email, string Role);