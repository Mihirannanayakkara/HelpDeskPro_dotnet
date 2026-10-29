using System.Security.Claims;
using HelpDeskPro.Models;

namespace HelpDeskPro.Services;

public static class ClaimsExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static Role GetRole(this ClaimsPrincipal user) =>
        Enum.Parse<Role>(user.FindFirstValue(ClaimTypes.Role)!);
}