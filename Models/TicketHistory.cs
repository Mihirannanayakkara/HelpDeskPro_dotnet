namespace HelpDeskPro.Models;

public class TicketHistory
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }
    public int ChangedById { get; set; }
    public User? ChangedBy { get; set; }
    public string Field { get; set; } = "";      // "Status", "AssignedAgent", "Priority"
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}