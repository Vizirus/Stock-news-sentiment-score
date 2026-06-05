using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DB;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IAppDBContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Ticker> Ticker { get; set; }
    public DbSet<ArticleScore> ArticleScores { get; set; }
    public DbSet<Article> Article { get; set; } = null!;
    public DbSet<ScoringJob> ScoringJobs { get; set; }
    public DbSet<TickerDailySummary> TickerDailySummaries { get; set; }
    public DbSet<SystemSettings> SystemSettings { get; set; }
    public DbSet<UserTicker> UserTickers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.ToTable("SystemSettings");

            entity.HasKey(s => s.Id);

            entity.Property(s => s.DailyLlmCallLimit)
                .IsRequired();

            entity.Property(s => s.BatchSize)
                .IsRequired();

            entity.Property(s => s.FetchIntervalHours)
                .IsRequired();

            entity.Property(s => s.UpdatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<Ticker>(entity =>
        {
            entity.ToTable("Tickers");

            entity.Property(t => t.Symbol)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(t => t.CompanyName)
                .HasMaxLength(255)
                .IsRequired();

            entity.HasIndex(t => t.Symbol)
                .IsUnique();
        });

        modelBuilder.Entity<Article>(entity =>
        {
            entity.ToTable("Articles");

            entity.Property(a => a.Title)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(a => a.Description)
                .IsRequired();

            entity.Property(a => a.Url)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(a => a.SourceName)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(a => a.PublishedAt)
                .IsRequired();

            entity.Property(a => a.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(a => a.Url)
                .IsUnique();

            entity.HasIndex(a => a.PublishedAt);
        });

        modelBuilder.Entity<ArticleScore>(entity =>
        {
            entity.ToTable("ArticleScores");

            entity.Property(s => s.Score)
                .HasPrecision(5, 4)
                .IsRequired();

            entity.Property(s => s.ScoreLabel)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(s => s.Confidence)
                .HasPrecision(5, 4)
                .IsRequired();

            entity.Property(s => s.ScoredAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(s => new { s.ArticleId, s.TickerId })
                .IsUnique();

            entity.HasIndex(s => new { s.TickerId, s.ScoredAt });

            entity.HasOne(s => s.Article)
                .WithMany(a => a.ArticleScores)
                .HasForeignKey(s => s.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(s => s.Ticker)
                .WithMany(t => t.ArticleScores)
                .HasForeignKey(s => s.TickerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TickerDailySummary>(entity =>
        {
            entity.ToTable("TickerDailySummaries");

            entity.Property(s => s.SummaryDate)
                .IsRequired();

            entity.Property(s => s.AverageScore)
                .HasPrecision(5, 4)
                .IsRequired();

            entity.Property(s => s.ArticleCount)
                .IsRequired();

            entity.Property(s => s.UpdatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(s => new { s.TickerId, s.SummaryDate })
                .IsUnique();

            entity.HasOne(s => s.Ticker)
                .WithMany(t => t.DailySummaries)
                .HasForeignKey(s => s.TickerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScoringJob>(entity =>
        {
            entity.ToTable("ScoringJobs");

            entity.Property(j => j.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(j => j.ErrorMessage)
                .IsRequired(false);

            entity.HasIndex(j => new { j.ArticleId, j.TickerId })
                .IsUnique();

            entity.HasIndex(j => new { j.StatusId, j.CreatedAt });

            entity.HasOne(j => j.Article)
                .WithMany(a => a.ScoringJobs)
                .HasForeignKey(j => j.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(j => j.Ticker)
                .WithMany(t => t.ScoringJobs)
                .HasForeignKey(j => j.TickerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(j => j.CompletdAt).HasColumnName("CompletedAt");
        });

        modelBuilder.Entity<UserTicker>(entity =>
        {
            entity.ToTable("UsersTickers");

            entity.HasKey(ut => ut.Id);

            entity.HasOne(ut => ut.User)
                .WithMany(u => u.UserTickers)
                .HasForeignKey(ut => ut.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ut => ut.Ticker)
                .WithMany(t => t.UserTickers)
                .HasForeignKey(ut => ut.TickerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(ut => new { ut.UserId, ut.TickerId })
                .IsUnique();
        });
    }
    public override async Task<int> SaveChangesAsync(CancellationToken token = default)
    {
        return await base.SaveChangesAsync(token);
    }
}
