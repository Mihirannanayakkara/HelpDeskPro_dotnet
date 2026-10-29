using HelpDeskPro.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Data;

public static class DbSeeder
{
    public static async Task SeedAdminAsync(IServiceProvider services, IConfiguration config)
    {
        var db = services.GetRequiredService<AppDbContext>();

        if (await db.Users.AnyAsync(u => u.Role == Role.Admin)) return;

        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(password)) return;   // no password configured, skip

        db.Users.Add(new User
        {
            Name = "System Admin",
            Email = "admin@helpdesk.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = Role.Admin
        });
        await db.SaveChangesAsync();
    }
}