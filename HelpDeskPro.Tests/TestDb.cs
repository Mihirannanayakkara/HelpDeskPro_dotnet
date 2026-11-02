using HelpDeskPro.Data;
using HelpDeskPro.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Tests;

/// <summary>A fresh in-memory SQLite database for each test.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public AppDbContext Db { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();   // the database lives only while this connection is open

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();   // also creates the seeded categories (Id 1-5)
    }

    public User AddUser(string name, Role role, bool isActive = true)
    {
        var user = new User
        {
            Name = name,
            Email = $"{name.ToLowerInvariant().Replace(" ", "")}@test.local",
            PasswordHash = "not-a-real-hash",
            Role = role,
            IsActive = isActive
        };
        Db.Users.Add(user);
        Db.SaveChanges();
        return user;
    }

    public Ticket AddTicket(int requesterId, TicketStatus status = TicketStatus.Open,
        int? agentId = null, Priority priority = Priority.Medium, string title = "Test ticket title")
    {
        var ticket = new Ticket
        {
            Title = title,
            Description = "A description that is long enough.",
            CategoryId = 1,
            Status = status,
            Priority = priority,
            RequesterId = requesterId,
            AssignedAgentId = agentId,
            DueAt = DateTime.UtcNow.AddHours(24)
        };
        Db.Tickets.Add(ticket);
        Db.SaveChanges();
        return ticket;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}