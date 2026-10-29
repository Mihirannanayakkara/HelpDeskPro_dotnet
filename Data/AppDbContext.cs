using HelpDeskPro.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Unique email
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<User>().Property(u => u.Email).HasMaxLength(200);
        b.Entity<User>().Property(u => u.Name).HasMaxLength(100);

        // Store enums as readable text instead of numbers
        b.Entity<User>().Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        b.Entity<Ticket>().Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        b.Entity<Ticket>().Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);

        b.Entity<Ticket>().Property(t => t.Title).HasMaxLength(150);
        b.Entity<Category>().Property(c => c.Name).HasMaxLength(50);

        // Two foreign keys to User on Ticket: SQL Server rejects multiple cascade
        // paths, so we use Restrict (block delete) instead.
        b.Entity<Ticket>()
            .HasOne(t => t.Requester).WithMany()
            .HasForeignKey(t => t.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Ticket>()
            .HasOne(t => t.AssignedAgent).WithMany()
            .HasForeignKey(t => t.AssignedAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<Comment>()
            .HasOne(c => c.Author).WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<TicketHistory>()
            .HasOne(h => h.ChangedBy).WithMany()
            .HasForeignKey(h => h.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for the filters we'll use later
        b.Entity<Ticket>().HasIndex(t => t.Status);
        b.Entity<Ticket>().HasIndex(t => t.RequesterId);
        b.Entity<Ticket>().HasIndex(t => t.AssignedAgentId);

        // Seed categories
        b.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Hardware" },
            new Category { Id = 2, Name = "Software" },
            new Category { Id = 3, Name = "Network" },
            new Category { Id = 4, Name = "Access & Accounts" },
            new Category { Id = 5, Name = "Other" });
    }
}