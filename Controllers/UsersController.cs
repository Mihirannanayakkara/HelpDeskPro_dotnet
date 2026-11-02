using System.ComponentModel.DataAnnotations;
using HelpDeskPro.Data;
using HelpDeskPro.Middleware;
using HelpDeskPro.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpDeskPro.Services;

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
    if (id == User.GetUserId())
        throw new BusinessRuleException("You cannot change your own role.");

    var user = await db.Users.FindAsync(id)
        ?? throw new NotFoundException($"User {id} was not found.");
    user.Role = dto.Role!.Value;
    await db.SaveChangesAsync();
    return NoContent();
}

[HttpPut("{id:int}/deactivate")]
public async Task<IActionResult> Deactivate(int id)
{
    if (id == User.GetUserId())
        throw new BusinessRuleException("You cannot deactivate your own account.");

    var user = await db.Users.FindAsync(id)
        ?? throw new NotFoundException($"User {id} was not found.");

    var hasActiveTickets = await db.Tickets.AnyAsync(t => t.AssignedAgentId == id
        && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);
    if (hasActiveTickets)
        throw new BusinessRuleException("Reassign this user's active tickets before deactivating them.");

    user.IsActive = false;
    await db.SaveChangesAsync();
    return NoContent();
}

[HttpPut("{id:int}/activate")]
public async Task<IActionResult> Activate(int id)
{
    var user = await db.Users.FindAsync(id)
        ?? throw new NotFoundException($"User {id} was not found.");
    user.IsActive = true;
    await db.SaveChangesAsync();
    return NoContent();
}
}