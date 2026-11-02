using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HelpDeskPro.Data;
using HelpDeskPro.Dtos;
using HelpDeskPro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace HelpDeskPro.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> RegisterAsync(RegisterDto dto);   // null = email already used
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);         // null = wrong credentials
}

public class AuthService(AppDbContext db, IConfiguration config, ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponseDto?> RegisterAsync(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == email))
            return null;

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = Role.Requester          // everyone who registers is a Requester
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreateResponse(user);
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
{
    logger.LogWarning("Failed login attempt for {Email}", email);
    return null;
}

        return CreateResponse(user);
    }

    private AuthResponseDto CreateResponse(User user)
    {
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var expires = DateTime.UtcNow.AddHours(2);
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new AuthResponseDto(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires, user.Id, user.Name, user.Email, user.Role.ToString());
    }
}