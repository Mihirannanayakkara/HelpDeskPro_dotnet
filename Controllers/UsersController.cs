using System.ComponentModel.DataAnnotations;
using HelpDeskPro.Data;
using HelpDeskPro.Middleware;
using HelpDeskPro.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Controllers;

public class ChangeRoleDto { [Required] public Role? Role { get; set; } }

[ApiController]
[Authorize(Roles = "Admin")]       // Admin only
[Route("api/users")]
public class UsersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.Users.OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.Name, u.Email, Role = u.Role.ToString(), u.IsActive })
            .ToListAsync());

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> ChangeRole(int id, ChangeRoleDto dto)
    {
        var user = await db.Users.FindAsync(id)
            ?? throw new NotFoundException($"User {id} was not found.");
        user.Role = dto.Role!.Value;
        await db.SaveChangesAsync();
        return NoContent();
    }
}