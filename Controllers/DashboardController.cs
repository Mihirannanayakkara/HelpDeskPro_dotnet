using HelpDeskPro.Data;
using HelpDeskPro.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Controllers;

[ApiController]
[Authorize(Roles = "Agent,Admin")]
[Route("api/dashboard")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var now = DateTime.UtcNow;

        var statusCounts = await db.Tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync();

        var priorityCounts = await db.Tickets
            .GroupBy(t => t.Priority)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync();

        var overdue = await db.Tickets.CountAsync(t =>
            t.DueAt != null && t.DueAt < now
            && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);

        var unassignedOpen = await db.Tickets.CountAsync(t =>
            t.Status == TicketStatus.Open && t.AssignedAgentId == null);

        // Average resolution time, calculated in memory (date maths is awkward in SQLite)
        var resolved = await db.Tickets
            .Where(t => t.ResolvedAt != null)
            .Select(t => new { t.CreatedAt, t.ResolvedAt })
            .ToListAsync();
        double? avgHours = resolved.Count == 0
            ? null
            : Math.Round(resolved.Average(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalHours), 1);

        var workload = await db.Tickets
            .Where(t => t.AssignedAgentId != null
                && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)
            .GroupBy(t => t.AssignedAgent!.Name)
            .Select(g => new { Agent = g.Key, OpenTickets = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            Total = statusCounts.Sum(s => s.Count),
            ByStatus = Enum.GetValues<TicketStatus>()
                .ToDictionary(s => s.ToString(), s => statusCounts.FirstOrDefault(x => x.Key == s)?.Count ?? 0),
            ByPriority = Enum.GetValues<Priority>()
                .ToDictionary(p => p.ToString(), p => priorityCounts.FirstOrDefault(x => x.Key == p)?.Count ?? 0),
            Overdue = overdue,
            UnassignedOpen = unassignedOpen,
            AvgResolutionHours = avgHours,
            AgentWorkload = workload
        });
    }
}