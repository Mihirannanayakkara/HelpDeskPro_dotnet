using HelpDeskPro.Models;
using HelpDeskPro.Services;
using Xunit;

namespace HelpDeskPro.Tests;

public class TicketWorkflowTests
{
    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.Assigned)]
    [InlineData(TicketStatus.Assigned, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Assigned, TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Assigned)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress)]
    public void CanTransition_AllowsValidMoves(TicketStatus from, TicketStatus to) =>
        Assert.True(TicketWorkflow.CanTransition(from, to));

    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Open, TicketStatus.Closed)]
    [InlineData(TicketStatus.Assigned, TicketStatus.Resolved)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Closed)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Open)]
    [InlineData(TicketStatus.Closed, TicketStatus.Open)]
    [InlineData(TicketStatus.Closed, TicketStatus.Resolved)]
    public void CanTransition_RejectsInvalidMoves(TicketStatus from, TicketStatus to) =>
        Assert.False(TicketWorkflow.CanTransition(from, to));

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void CanTransition_RejectsMovingToTheSameStatus(TicketStatus status) =>
        Assert.False(TicketWorkflow.CanTransition(status, status));
}