using System.ComponentModel.DataAnnotations;
using HelpDeskPro.Models;

namespace HelpDeskPro.Dtos;

public class TicketQuery
{
    public TicketStatus? Status { get; set; }
    public Priority? Priority { get; set; }
    public int? AssignedTo { get; set; }          // agent id
    public bool? Unassigned { get; set; }         // the agent "queue"
    public bool? Overdue { get; set; }
    public string? Search { get; set; }           // matches title or description

    public string SortBy { get; set; } = "createdAt";   // createdAt | dueAt | priority | updatedAt
    public string SortDir { get; set; } = "desc";       // asc | desc

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;
}

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}