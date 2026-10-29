using HelpDeskPro.Data;
using HelpDeskPro.Dtos;
using HelpDeskPro.Middleware;
using HelpDeskPro.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;


namespace HelpDeskPro.Services;

public interface ITicketService
{
    Task<PagedResult<TicketDto>> GetAllAsync(TicketQuery q, int userId, Role role);
    Task<TicketDto> GetByIdAsync(int id, int userId, Role role);
    Task<TicketDto> CreateAsync(CreateTicketDto dto, int userId, Role role);
    Task<TicketDto> UpdateAsync(int id, UpdateTicketDto dto, int userId, Role role);
    Task<TicketDto> AssignAsync(int id, AssignTicketDto dto, int userId, Role role);
Task<TicketDto> ChangeStatusAsync(int id, ChangeStatusDto dto, int userId, Role role);
Task<List<HistoryDto>> GetHistoryAsync(int id, int userId, Role role);
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

    public async Task<PagedResult<TicketDto>> GetAllAsync(TicketQuery q, int userId, Role role)
{
    var query = Visible(userId, role);          // role rule is applied FIRST, always
    var now = DateTime.UtcNow;

    // Filters
    if (q.Status is not null)   query = query.Where(t => t.Status == q.Status);
    if (q.Priority is not null) query = query.Where(t => t.Priority == q.Priority);
    if (q.AssignedTo is not null) query = query.Where(t => t.AssignedAgentId == q.AssignedTo);
    if (q.Unassigned == true)   query = query.Where(t => t.AssignedAgentId == null);

    if (q.Overdue == true)
        query = query.Where(t => t.DueAt != null && t.DueAt < now
            && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);

    if (!string.IsNullOrWhiteSpace(q.Search))
    {
        var pattern = $"%{q.Search.Trim()}%";
        query = query.Where(t => EF.Functions.Like(t.Title, pattern)
                              || EF.Functions.Like(t.Description, pattern));
    }

    // Count BEFORE paging
    var total = await query.CountAsync();

    // Sorting (a whitelist, never trust raw column names from the client)
    var desc = !string.Equals(q.SortDir, "asc", StringComparison.OrdinalIgnoreCase);

    // Priority is stored as text, so sorting by it would be alphabetical.
    // This CASE expression sorts by real severity instead.
    var severity = (Expression<Func<Ticket, int>>)(t =>
        t.Priority == Priority.Critical ? 4 :
        t.Priority == Priority.High ? 3 :
        t.Priority == Priority.Medium ? 2 : 1);

    query = q.SortBy.ToLowerInvariant() switch
    {
        "duedat"    => desc ? query.OrderByDescending(t => t.DueAt) : query.OrderBy(t => t.DueAt),
        "priority"  => desc ? query.OrderByDescending(severity) : query.OrderBy(severity),
        "updatedat" => desc ? query.OrderByDescending(t => t.UpdatedAt) : query.OrderBy(t => t.UpdatedAt),
        _           => desc ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt)
    };

    // Tie-breaker so pages never overlap or skip rows
    var ordered = ((IOrderedQueryable<Ticket>)query).ThenBy(t => t.Id);

    var items = await Project(ordered.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)).ToListAsync();
    return new PagedResult<TicketDto>(items, q.Page, q.PageSize, total);
}
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
    public async Task<TicketDto> AssignAsync(int id, AssignTicketDto dto, int userId, Role role)
{
    if (role == Role.Requester)
        throw new ForbiddenException("Only agents and admins can assign tickets.");

    var ticket = await db.Tickets.Include(t => t.AssignedAgent).FirstOrDefaultAsync(t => t.Id == id)
        ?? throw new NotFoundException($"Ticket {id} was not found.");

    if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        throw new BusinessRuleException("Resolved or closed tickets cannot be reassigned.");

    // Agents can only assign to themselves; admins can assign anyone or unassign
    if (role == Role.Agent && dto.AgentId != userId)
        throw new ForbiddenException("Agents can only assign tickets to themselves.");

    User? newAgent = null;
    if (dto.AgentId is not null)
    {
        newAgent = await db.Users.FirstOrDefaultAsync(u => u.Id == dto.AgentId)
            ?? throw new BusinessRuleException("Agent does not exist.");
        if (!newAgent.IsActive || newAgent.Role == Role.Requester)
            throw new BusinessRuleException("The assignee must be an active agent or admin.");
    }
    else if (ticket.Status != TicketStatus.Open && ticket.Status != TicketStatus.Assigned)
    {
        throw new BusinessRuleException("Move the ticket back to Assigned before unassigning it.");
    }

    var oldName = ticket.AssignedAgent?.Name;
    if (ticket.AssignedAgentId == dto.AgentId)
        return await GetByIdAsync(id, userId, role);   // nothing changed

    ticket.AssignedAgentId = dto.AgentId;
    ticket.UpdatedAt = DateTime.UtcNow;
    AddHistory(ticket.Id, userId, "AssignedAgent", oldName, newAgent?.Name);

    // Assigning an open ticket moves it to Assigned; unassigning returns it to Open
    if (newAgent is not null && ticket.Status == TicketStatus.Open)
    {
        AddHistory(ticket.Id, userId, "Status", ticket.Status.ToString(), TicketStatus.Assigned.ToString());
        ticket.Status = TicketStatus.Assigned;
    }
    else if (newAgent is null && ticket.Status == TicketStatus.Assigned)
    {
        AddHistory(ticket.Id, userId, "Status", ticket.Status.ToString(), TicketStatus.Open.ToString());
        ticket.Status = TicketStatus.Open;
    }

    await db.SaveChangesAsync();
    return await GetByIdAsync(id, userId, role);
}

public async Task<TicketDto> ChangeStatusAsync(int id, ChangeStatusDto dto, int userId, Role role)
{
    var ticket = await Visible(userId, role).FirstOrDefaultAsync(t => t.Id == id)
        ?? throw new NotFoundException($"Ticket {id} was not found.");

    var newStatus = dto.Status!.Value;
    var oldStatus = ticket.Status;

    if (!TicketWorkflow.CanTransition(oldStatus, newStatus))
        throw new BusinessRuleException($"A ticket cannot move from {oldStatus} to {newStatus}.");

    // Permission rules
    if (role == Role.Requester)
    {
        // The only thing a requester may do: close their own resolved ticket
        if (!(oldStatus == TicketStatus.Resolved && newStatus == TicketStatus.Closed))
            throw new ForbiddenException("Requesters can only close their own resolved tickets.");
    }
    if (oldStatus == TicketStatus.Closed && role != Role.Admin)
        throw new ForbiddenException("Only an admin can reopen a closed ticket.");

    // Agent rules
    if (newStatus is TicketStatus.InProgress or TicketStatus.Resolved && ticket.AssignedAgentId is null)
        throw new BusinessRuleException("Assign the ticket to an agent first.");
    if (role == Role.Agent && ticket.AssignedAgentId != userId && newStatus != TicketStatus.Closed)
        throw new ForbiddenException("Only the assigned agent or an admin can change this ticket's status.");

    // Moving back to Open must also clear the assignee
    if (newStatus == TicketStatus.Open)
    {
        AddHistory(ticket.Id, userId, "AssignedAgent", ticket.AssignedAgentId?.ToString(), null);
        ticket.AssignedAgentId = null;
    }

    ticket.Status = newStatus;
    ticket.UpdatedAt = DateTime.UtcNow;
    if (newStatus == TicketStatus.Resolved) ticket.ResolvedAt = DateTime.UtcNow;
    if (oldStatus is TicketStatus.Resolved or TicketStatus.Closed && newStatus == TicketStatus.InProgress)
        ticket.ResolvedAt = null;

    AddHistory(ticket.Id, userId, "Status", oldStatus.ToString(), newStatus.ToString());
    await db.SaveChangesAsync();
    return await GetByIdAsync(id, userId, role);
}

public async Task<List<HistoryDto>> GetHistoryAsync(int id, int userId, Role role)
{
    if (!await Visible(userId, role).AnyAsync(t => t.Id == id))
        throw new NotFoundException($"Ticket {id} was not found.");

    return await db.TicketHistories
        .Where(h => h.TicketId == id)
        .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
        .Select(h => new HistoryDto(h.Field, h.OldValue, h.NewValue, h.ChangedBy!.Name, h.ChangedAt))
        .ToListAsync();
}

private void AddHistory(int ticketId, int userId, string field, string? oldValue, string? newValue) =>
    db.TicketHistories.Add(new TicketHistory
    {
        TicketId = ticketId, ChangedById = userId,
        Field = field, OldValue = oldValue, NewValue = newValue
    });
}