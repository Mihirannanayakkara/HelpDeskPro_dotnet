using HelpDeskPro.Models;

namespace HelpDeskPro.Services;

public static class Sla
{
    public static DateTime CalculateDueAt(Priority priority, DateTime from) => priority switch
    {
        Priority.Critical => from.AddHours(4),
        Priority.High => from.AddHours(8),
        Priority.Medium => from.AddHours(24),
        _ => from.AddHours(72)
    };
}