using HelpDeskPro.Dtos;
using HelpDeskPro.Middleware;
using HelpDeskPro.Models;
using HelpDeskPro.Services;
using Xunit;

namespace HelpDeskPro.Tests;

public class CommentServiceTests : IDisposable
{
    private readonly TestDb _t = new();
    private readonly CommentService _service;
    private readonly User _ali, _bob, _sara;

    public CommentServiceTests()
    {
        _service = new CommentService(_t.Db);
        _ali = _t.AddUser("Ali", Role.Requester);
        _bob = _t.AddUser("Bob", Role.Requester);
        _sara = _t.AddUser("Sara", Role.Agent);
    }

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Requester_NeverSeesInternalNotes()
    {
        var ticket = _t.AddTicket(_ali.Id);
        await _service.AddAsync(ticket.Id, new CreateCommentDto { Text = "Public reply", IsInternal = false }, _sara.Id, Role.Agent);
        await _service.AddAsync(ticket.Id, new CreateCommentDto { Text = "Secret note", IsInternal = true }, _sara.Id, Role.Agent);

        var seenByAli = await _service.GetAsync(ticket.Id, _ali.Id, Role.Requester);

        Assert.Single(seenByAli);
        Assert.DoesNotContain(seenByAli, c => c.IsInternal);
    }

    [Fact]
    public async Task Agent_SeesInternalNotes()
    {
        var ticket = _t.AddTicket(_ali.Id);
        await _service.AddAsync(ticket.Id, new CreateCommentDto { Text = "Public reply", IsInternal = false }, _sara.Id, Role.Agent);
        await _service.AddAsync(ticket.Id, new CreateCommentDto { Text = "Secret note", IsInternal = true }, _sara.Id, Role.Agent);

        var seenBySara = await _service.GetAsync(ticket.Id, _sara.Id, Role.Agent);

        Assert.Equal(2, seenBySara.Count);
    }

    [Fact]
    public async Task Requester_CannotPostInternalNote()
    {
        var ticket = _t.AddTicket(_ali.Id);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.AddAsync(ticket.Id, new CreateCommentDto { Text = "Sneaky", IsInternal = true }, _ali.Id, Role.Requester));
    }

    [Fact]
    public async Task Requester_CannotCommentOnSomeoneElsesTicket()
    {
        var ticket = _t.AddTicket(_ali.Id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.AddAsync(ticket.Id, new CreateCommentDto { Text = "Hello" }, _bob.Id, Role.Requester));
    }

    [Fact]
    public async Task ClosedTicket_RejectsNewComments()
    {
        var ticket = _t.AddTicket(_ali.Id, TicketStatus.Closed, _sara.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AddAsync(ticket.Id, new CreateCommentDto { Text = "Too late" }, _ali.Id, Role.Requester));
    }

    [Fact]
    public async Task WhitespaceOnlyComment_IsRejected()
    {
        var ticket = _t.AddTicket(_ali.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AddAsync(ticket.Id, new CreateCommentDto { Text = "   " }, _ali.Id, Role.Requester));
    }
}