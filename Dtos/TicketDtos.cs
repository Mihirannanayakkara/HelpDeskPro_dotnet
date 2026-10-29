using System.ComponentModel.DataAnnotations;
using HelpDeskPro.Models;

namespace HelpDeskPro.Dtos;

public class CreateTicketDto
{
    [Required, StringLength(150, MinimumLength = 5)]
    public string Title { get; set; } = "";

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    public Priority Priority { get; set; } = Priority.Medium;
}

public class UpdateTicketDto
{
    [Required, StringLength(150, MinimumLength = 5)]
    public string Title { get; set; } = "";

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    public Priority Priority { get; set; }
}

public class AssignTicketDto
{
    public int? AgentId { get; set; }   // null = unassign (Admin only)
}

public class ChangeStatusDto
{
    [Required] public TicketStatus? Status { get; set; }
}

public record HistoryDto(string Field, string? OldValue, string? NewValue, string ChangedBy, DateTime ChangedAt);

public record TicketDto(
    int Id, string Title, string Description,
    TicketStatus Status, Priority Priority,
    string Category, string Requester, string? AssignedAgent,
    DateTime CreatedAt, DateTime? DueAt, bool IsOverdue);