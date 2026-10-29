using HelpDeskPro.Data;
using HelpDeskPro.Dtos;
using HelpDeskPro.Middleware;
using HelpDeskPro.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Services;

public interface ICommentService
{
    Task<List<CommentDto>> GetAsync(int ticketId, int userId, Role role);
    Task<CommentDto> AddAsync(int ticketId, CreateCommentDto dto, int userId, Role role);
}

public class CommentService(AppDbContext db) : ICommentService
{
    // Returns the ticket if the user may see it, otherwise throws 404
    private async Task<Ticket> GetVisibleTicketAsync(int ticketId, int userId, Role role)
    {
        var query = role == Role.Requester
            ? db.Tickets.Where(t => t.RequesterId == userId)
            : db.Tickets;

        return await query.FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new NotFoundException($"Ticket {ticketId} was not found.");
    }

    public async Task<List<CommentDto>> GetAsync(int ticketId, int userId, Role role)
    {
        await GetVisibleTicketAsync(ticketId, userId, role);

        var query = db.Comments.Where(c => c.TicketId == ticketId);

        // Rule: requesters never see internal notes
        if (role == Role.Requester)
            query = query.Where(c => !c.IsInternal);

        return await query
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Select(c => new CommentDto(c.Id, c.Text, c.IsInternal, c.Author!.Name, c.Author.Role, c.CreatedAt))
            .ToListAsync();
    }

    public async Task<CommentDto> AddAsync(int ticketId, CreateCommentDto dto, int userId, Role role)
    {
        var ticket = await GetVisibleTicketAsync(ticketId, userId, role);

        if (ticket.Status == TicketStatus.Closed)
            throw new BusinessRuleException("Closed tickets cannot receive new comments.");

        if (dto.IsInternal && role == Role.Requester)
            throw new ForbiddenException("Only agents and admins can add internal notes.");

        var text = dto.Text.Trim();
        if (text.Length == 0)
            throw new BusinessRuleException("Comment text cannot be empty.");

        var comment = new Comment
        {
            TicketId = ticketId,
            AuthorId = userId,
            Text = text,
            IsInternal = dto.IsInternal
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        var author = await db.Users.FirstAsync(u => u.Id == userId);
        return new CommentDto(comment.Id, comment.Text, comment.IsInternal, author.Name, author.Role, comment.CreatedAt);
    }
}