using HelpDeskPro.Models;

namespace HelpDeskPro.Services;

public static class TicketWorkflow
{
    private static readonly Dictionary<TicketStatus, TicketStatus[]> Allowed = new()
    {
        [TicketStatus.Open]       = [TicketStatus.Assigned],
        [TicketStatus.Assigned]   = [TicketStatus.InProgress, TicketStatus.Open],
        [TicketStatus.InProgress] = [TicketStatus.Resolved, TicketStatus.Assigned],
        [TicketStatus.Resolved]   = [TicketStatus.Closed, TicketStatus.InProgress],
        [TicketStatus.Closed]     = [TicketStatus.InProgress]   // Admin only, checked in the service
    };

    public static bool CanTransition(TicketStatus from, TicketStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}