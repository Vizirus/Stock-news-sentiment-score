using Application.Interfaces;
using Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Application.Tests.TestInfrastructure;

public sealed class TestAppDbContext : DbContext, IAppDBContext
{
    private readonly SqliteConnection _connection;

    public DbSet<Ticker> Ticker { get; set; } = null!;
    public DbSet<ArticleScore> ArticleScores { get; set; } = null!;
    public DbSet<Article> Article { get; set; } = null!;
    public DbSet<ScoringJob> ScoringJobs { get; set; } = null!;
    public DbSet<TickerDailySummary> TickerDailySummaries { get; set; } = null!;
    public DbSet<SystemSettings> SystemSettings { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public TestAppDbContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Database.EnsureCreated();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(_connection);
    }

    async Task<int> IAppDBContext.SaveChangesAsync(CancellationToken token)
    {
        return await base.SaveChangesAsync(token);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>()
            .HasMany(a => a.ArticleScores)
            .WithOne(s => s.Article)
            .HasForeignKey(s => s.ArticleId);

        modelBuilder.Entity<Article>()
            .HasMany(a => a.ScoringJobs)
            .WithOne(j => j.Article)
            .HasForeignKey(j => j.ArticleId);

        modelBuilder.Entity<Ticker>()
            .HasMany(t => t.ArticleScores)
            .WithOne(s => s.Ticker)
            .HasForeignKey(s => s.TickerId);

        modelBuilder.Entity<Ticker>()
            .HasMany(t => t.ScoringJobs)
            .WithOne(j => j.Ticker)
            .HasForeignKey(j => j.TickerId);

        modelBuilder.Entity<Ticker>()
            .HasMany(t => t.DailySummaries)
            .WithOne(s => s.Ticker)
            .HasForeignKey(s => s.TickerId);
    }

    public override void Dispose()
    {
        base.Dispose();
        _connection.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
