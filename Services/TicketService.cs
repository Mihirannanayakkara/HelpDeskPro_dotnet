using HelpDeskPro.Data;
using HelpDeskPro.Dtos;
using HelpDeskPro.Middleware;
using HelpDeskPro.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Services;

public interface ITicketService
{
    Task<List<TicketDto>> GetAllAsync(int userId, Role role);
    Task<TicketDto> GetByIdAsync(int id, int userId, Role role);
    Task<TicketDto> CreateAsync(CreateTicketDto dto, int userId, Role role);
    Task<TicketDto> UpdateAsync(int id, UpdateTicketDto dto, int userId, Role role);
}

public class TicketService(AppDbContext db) : ITicketService
{
    // Rule: requesters only see their own tickets
    private IQueryable<Ticket> Visible(int userId, Role role) =>
        role == Role.Requester ? db.Tickets.Where(t => t.RequesterId == userId) : db.Tickets;

    private static IQueryable<TicketDto> Project(IQueryable<Ticket> query)
    {
        var now = DateTime.UtcNow;
        return query.Select(t => new TicketDto(
            t.Id, t.Title, t.Description, t.Status, t.Priority,
            t.Category!.Name, t.Requester!.Name,
            t.AssignedAgent != null ? t.AssignedAgent.Name : null,
            t.CreatedAt, t.DueAt,
            t.DueAt != null && t.DueAt < now
                && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed));
    }

    public async Task<List<TicketDto>> GetAllAsync(int userId, Role role) =>
        await Project(Visible(userId, role).OrderByDescending(t => t.CreatedAt)).ToListAsync();

    public async Task<TicketDto> GetByIdAsync(int id, int userId, Role role) =>
        await Project(Visible(userId, role).Where(t => t.Id == id)).FirstOrDefaultAsync()
        ?? throw new NotFoundException($"Ticket {id} was not found.");
        // Same 404 for "missing" and "not yours", so we don't reveal which ticket ids exist.

    public async Task<TicketDto> CreateAsync(CreateTicketDto dto, int userId, Role role)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == dto.CategoryId))
            throw new BusinessRuleException("Category does not exist.");

        var now = DateTime.UtcNow;
        var ticket = new Ticket
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            CategoryId = dto.CategoryId,
            Priority = dto.Priority,
            RequesterId = userId,
            CreatedAt = now,
            DueAt = Sla.CalculateDueAt(dto.Priority, now)
        };

        db.Tickets.Add(ticket);
        db.TicketHistories.Add(new TicketHistory
        {
            Ticket = ticket, ChangedById = userId,
            Field = "Status", OldValue = null, NewValue = TicketStatus.Open.ToString()
        });
        await db.SaveChangesAsync();

        return await GetByIdAsync(ticket.Id, userId, role);
    }

    public async Task<TicketDto> UpdateAsync(int id, UpdateTicketDto dto, int userId, Role role)
    {
        var ticket = await Visible(userId, role).FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new NotFoundException($"Ticket {id} was not found.");

        if (ticket.Status == TicketStatus.Closed)
            throw new BusinessRuleException("Closed tickets cannot be edited.");

        if (role == Role.Requester)
        {
            if (ticket.Status != TicketStatus.Open)
                throw new BusinessRuleException("You can only edit a ticket before it is assigned.");
            if (dto.Priority != ticket.Priority)
                throw new ForbiddenException("Only agents and admins can change priority.");
        }

        if (!await db.Categories.AnyAsync(c => c.Id == dto.CategoryId))
            throw new BusinessRuleException("Category does not exist.");

        if (dto.Priority != ticket.Priority)
        {
            db.TicketHistories.Add(new TicketHistory
            {
                TicketId = ticket.Id, ChangedById = userId, Field = "Priority",
                OldValue = ticket.Priority.ToString(), NewValue = dto.Priority.ToString()
            });
            ticket.Priority = dto.Priority;
            ticket.DueAt = Sla.CalculateDueAt(dto.Priority, ticket.CreatedAt);
        }

        ticket.Title = dto.Title.Trim();
        ticket.Description = dto.Description.Trim();
        ticket.CategoryId = dto.CategoryId;
        ticket.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return await GetByIdAsync(ticket.Id, userId, role);
    }
}