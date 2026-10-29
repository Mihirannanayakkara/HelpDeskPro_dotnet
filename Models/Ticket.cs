namespace HelpDeskPro.Models;

public class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public Priority Priority { get; set; } = Priority.Medium;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public int RequesterId { get; set; }
    public User? Requester { get; set; }

    public int? AssignedAgentId { get; set; }      // nullable: unassigned at first
    public User? AssignedAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public List<Comment> Comments { get; set; } = new();
    public List<TicketHistory> History { get; set; } = new();
}