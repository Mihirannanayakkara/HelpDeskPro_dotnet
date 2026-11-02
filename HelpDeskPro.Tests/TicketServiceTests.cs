using HelpDeskPro.Dtos;
using HelpDeskPro.Middleware;
using HelpDeskPro.Models;
using HelpDeskPro.Services;
using Xunit;

namespace HelpDeskPro.Tests;

public class TicketServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly TicketService _service;
    private readonly User _ali, _bob, _sara, _tom, _admin;

    public TicketServiceTests()
    {
        _service = new TicketService(_t.Db);
        _ali = _t.AddUser("Ali", Role.Requester);
        _bob = _t.AddUser("Bob", Role.Requester);
        _sara = _t.AddUser("Sara", Role.Agent);
        _tom = _t.AddUser("Tom", Role.Agent);
        _admin = _t.AddUser("Admin", Role.Admin);
    }

    public void Dispose() => _t.Dispose();

    // ---------- Visibility ----------

    [Fact]
    public async Task Requester_SeesOnlyOwnTickets()
    {
        _t.AddTicket(_ali.Id);
        _t.AddTicket(_ali.Id);
        _t.AddTicket(_bob.Id);

        var result = await _service.GetAllAsync(new TicketQuery(), _ali.Id, Role.Requester);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Agent_SeesAllTickets()
    {
        _t.AddTicket(_ali.Id);
        _t.AddTicket(_bob.Id);

        var result = await _service.GetAllAsync(new TicketQuery(), _sara.Id, Role.Agent);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Requester_GetsNotFoundForSomeoneElsesTicket()
    {
        var bobsTicket = _t.AddTicket(_bob.Id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(bobsTicket.Id, _ali.Id, Role.Requester));
    }

    [Fact]
    public async Task Requester_CannotBypassVisibilityWithFilters()
    {
        _t.AddTicket(_bob.Id, priority: Priority.High);

        var result = await _service.GetAllAsync(
            new TicketQuery { Priority = Priority.High }, _ali.Id, Role.Requester);

        Assert.Equal(0, result.TotalCount);
    }

    // ---------- Paging ----------

    [Fact]
    public async Task Paging_ReturnsCorrectPageAndTotals()
    {
        for (var i = 0; i < 15; i++) _t.AddTicket(_ali.Id);

        var page2 = await _service.GetAllAsync(
            new TicketQuery { Page = 2, PageSize = 10 }, _sara.Id, Role.Agent);

        Assert.Equal(5, page2.Items.Count);
        Assert.Equal(15, page2.TotalCount);
        Assert.Equal(2, page2.TotalPages);
    }

    [Fact]
    public async Task SortByPriority_OrdersBySeverityNotAlphabetically()
    {
        _t.AddTicket(_ali.Id, priority: Priority.Low);
        _t.AddTicket(_ali.Id, priority: Priority.Critical);
        _t.AddTicket(_ali.Id, priority: Priority.Medium);
        _t.AddTicket(_ali.Id, priority: Priority.High);

        var result = await _service.GetAllAsync(
            new TicketQuery { SortBy = "priority", SortDir = "desc" }, _sara.Id, Role.Agent);

        Assert.Equal(
            new[] { Priority.Critical, Priority.High, Priority.Medium, Priority.Low },
            result.Items.Select(t => t.Priority).ToArray());
    }

    // ---------- Create ----------

    [Fact]
    public async Task Create_SetsOpenStatus_DueDate_AndHistory()
    {
        var dto = new CreateTicketDto
        {
            Title = "Laptop will not boot",
            Description = "Black screen after the logo.",
            CategoryId = 1,
            Priority = Priority.Critical
        };

        var created = await _service.CreateAsync(dto, _ali.Id, Role.Requester);

        Assert.Equal(TicketStatus.Open, created.Status);
        Assert.Equal(TimeSpan.FromHours(4), created.DueAt!.Value - created.CreatedAt);
        Assert.Single(_t.Db.TicketHistories.Where(h => h.TicketId == created.Id && h.Field == "Status"));
    }

    [Fact]
    public async Task Create_WithUnknownCategory_IsRejected()
    {
        var dto = new CreateTicketDto
        {
            Title = "Some title here",
            Description = "Some description here.",
            CategoryId = 999
        };

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.CreateAsync(dto, _ali.Id, Role.Requester));
    }

    // ---------- Update ----------

    [Fact]
    public async Task Requester_CannotChangePriority()
    {
        var ticket = _t.AddTicket(_ali.Id, priority: Priority.Low);
        var dto = new UpdateTicketDto
        {
            Title = ticket.Title, Description = ticket.Description,
            CategoryId = 1, Priority = Priority.Critical
        };

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.UpdateAsync(ticket.Id, dto, _ali.Id, Role.Requester));
    }

    [Fact]
    public async Task Agent_ChangingPriority_RecalculatesDueDate_AndWritesHistory()
    {
        var ticket = _t.AddTicket(_ali.Id, priority: Priority.Low);
        var dto = new UpdateTicketDto
        {
            Title = ticket.Title, Description = ticket.Description,
            CategoryId = 1, Priority = Priority.Critical
        };

        var updated = await _service.UpdateAsync(ticket.Id, dto, _sara.Id, Role.Agent);

        Assert.Equal(Priority.Critical, updated.Priority);
        Assert.Equal(TimeSpan.FromHours(4), updated.DueAt!.Value - updated.CreatedAt);
        Assert.Contains(_t.Db.TicketHistories, h => h.TicketId == ticket.Id && h.Field == "Priority"
            && h.OldValue == "Low" && h.NewValue == "Critical");
    }

    [Fact]
    public async Task ClosedTicket_CannotBeEdited()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.Closed, _sara.Id);
        var dto = new UpdateTicketDto
        {
            Title = "Changed title here", Description = "Changed description here.",
            CategoryId = 1, Priority = ticket.Priority
        };

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.UpdateAsync(ticket.Id, dto, _admin.Id, Role.Admin));
    }

    // ---------- Assignment ----------

    [Fact]
    public async Task Requester_CannotAssign()
    {
        var ticket = _t.AddTicket(_ali.Id);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.AssignAsync(ticket.Id, new AssignTicketDto { AgentId = _sara.Id }, _ali.Id, Role.Requester));
    }

    [Fact]
    public async Task Agent_CanAssignToSelf_AndStatusBecomesAssigned()
    {
        var ticket = _t.AddTicket(_ali.Id);

        var result = await _service.AssignAsync(ticket.Id, new AssignTicketDto { AgentId = _sara.Id }, _sara.Id, Role.Agent);

        Assert.Equal(TicketStatus.Assigned, result.Status);
        Assert.Equal("Sara", result.AssignedAgent);
    }

    [Fact]
    public async Task Agent_CannotAssignToSomeoneElse()
    {
        var ticket = _t.AddTicket(_ali.Id);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.AssignAsync(ticket.Id, new AssignTicketDto { AgentId = _tom.Id }, _sara.Id, Role.Agent));
    }

    [Fact]
    public async Task Admin_CanAssignToAnyAgent()
    {
        var ticket = _t.AddTicket(_ali.Id);

        var result = await _service.AssignAsync(ticket.Id, new AssignTicketDto { AgentId = _tom.Id }, _admin.Id, Role.Admin);

        Assert.Equal("Tom", result.AssignedAgent);
    }

    [Fact]
    public async Task Cannot_AssignToARequester()
    {
        var ticket = _t.AddTicket(_ali.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AssignAsync(ticket.Id, new AssignTicketDto { AgentId = _bob.Id }, _admin.Id, Role.Admin));
    }

    [Fact]
    public async Task Cannot_AssignToAnInactiveAgent()
    {
        var inactive = _t.AddUser("Gone Agent", Role.Agent, isActive: false);
        var ticket = _t.AddTicket(_ali.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AssignAsync(ticket.Id, new AssignTicketDto { AgentId = inactive.Id }, _admin.Id, Role.Admin));
    }

    [Fact]
    public async Task Assignment_WritesHistory()
    {
        var ticket = _t.AddTicket(_ali.Id);

        await _service.AssignAsync(ticket.Id, new AssignTicketDto { AgentId = _sara.Id }, _sara.Id, Role.Agent);

        Assert.Contains(_t.Db.TicketHistories, h => h.TicketId == ticket.Id && h.Field == "AssignedAgent" && h.NewValue == "Sara");
        Assert.Contains(_t.Db.TicketHistories, h => h.TicketId == ticket.Id && h.Field == "Status" && h.NewValue == "Assigned");
    }

    // ---------- Status changes ----------

    [Fact]
    public async Task InvalidTransition_IsRejected()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.Assigned, _sara.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.ChangeStatusAsync(ticket.Id, new ChangeStatusDto { Status = TicketStatus.Resolved }, _sara.Id, Role.Agent));
    }

    [Fact]
    public async Task AssignedAgent_CanMoveToInProgress()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.Assigned, _sara.Id);

        var result = await _service.ChangeStatusAsync(ticket.Id, new ChangeStatusDto { Status = TicketStatus.InProgress }, _sara.Id, Role.Agent);

        Assert.Equal(TicketStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task OtherAgent_CannotChangeStatus()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.Assigned, _sara.Id);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.ChangeStatusAsync(ticket.Id, new ChangeStatusDto { Status = TicketStatus.InProgress }, _tom.Id, Role.Agent));
    }

    [Fact]
    public async Task InProgress_RequiresAnAssignedAgent()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.Assigned, agentId: null);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.ChangeStatusAsync(ticket.Id, new ChangeStatusDto { Status = TicketStatus.InProgress }, _admin.Id, Role.Admin));
    }

    [Fact]
    public async Task Resolving_SetsResolvedAt_AndWritesHistory()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.InProgress, _sara.Id);

        await _service.ChangeStatusAsync(ticket.Id, new ChangeStatusDto { Status = TicketStatus.Resolved }, _sara.Id, Role.Agent);

        var saved = _t.Db.Tickets.Single(t => t.Id == ticket.Id);
        Assert.NotNull(saved.ResolvedAt);
        Assert.Contains(_t.Db.TicketHistories, h => h.TicketId == ticket.Id && h.Field == "Status"
            && h.OldValue == "InProgress" && h.NewValue == "Resolved");
    }

    [Fact]
    public async Task Requester_CanCloseOwnResolvedTicket()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.Resolved, _sara.Id);

        var result = await _service.ChangeStatusAsync(ticket.Id, new ChangeStatusDto { Status = TicketStatus.Closed }, _ali.Id, Role.Requester);

        Assert.Equal(TicketStatus.Closed, result.Status);
    }

    [Fact]
    public async Task Requester_CannotDoAnythingElseWithStatus()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.Resolved, _sara.Id);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.ChangeStatusAsync(ticket.Id, new ChangeStatusDto { Status = TicketStatus.InProgress }, _ali.Id, Role.Requester));
    }

    [Fact]
    public async Task Requester_CannotCloseSomeoneElsesTicket()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.Resolved, _sara.Id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ChangeStatusAsync(ticket.Id, new ChangeStatusDto { Status = TicketStatus.Closed }, _bob.Id, Role.Requester));
    }

    [Fact]
    public async Task Agent_CannotReopenClosedTicket()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.Closed, _sara.Id);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.ChangeStatusAsync(ticket.Id, new ChangeStatusDto { Status = TicketStatus.InProgress }, _sara.Id, Role.Agent));
    }

    [Fact]
    public async Task Admin_CanReopenClosedTicket_AndResolvedAtIsCleared()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.Closed, _sara.Id);
        ticket.ResolvedAt = DateTime.UtcNow;
        _t.Db.SaveChanges();

        var result = await _service.ChangeStatusAsync(ticket.Id, new ChangeStatusDto { Status = TicketStatus.InProgress }, _admin.Id, Role.Admin);

        Assert.Equal(TicketStatus.InProgress, result.Status);
        Assert.Null(_t.Db.Tickets.Single(t => t.Id == ticket.Id).ResolvedAt);
    }

    // ---------- History ----------

    [Fact]
    public async Task History_IsHiddenFromOtherRequesters()
    {
        var ticket = _t.AddTicket(_ali.Id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetHistoryAsync(ticket.Id, _bob.Id, Role.Requester));
    }
}