using HelpDeskPro.Models;
using HelpDeskPro.Services;
using Xunit;

namespace HelpDeskPro.Tests;

public class SlaTests
{
    [Theory]
    [InlineData(Priority.Critical, 4)]
    [InlineData(Priority.High, 8)]
    [InlineData(Priority.Medium, 24)]
    [InlineData(Priority.Low, 72)]
    public void CalculateDueAt_AddsHoursBasedOnPriority(Priority priority, int expectedHours)
    {
        var start = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        var due = Sla.CalculateDueAt(priority, start);

        Assert.Equal(start.AddHours(expectedHours), due);
    }
}