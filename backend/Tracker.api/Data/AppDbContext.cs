using Microsoft.EntityFrameworkCore;
using Tracker.Api.Models;

namespace Tracker.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<TftMatch> Matches => Set<TftMatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Index for filtering match uniqueness
        modelBuilder.Entity<TftMatch>()
            .HasIndex(m => m.MatchId);

        // Index for quick player match history aggregations
        modelBuilder.Entity<TftMatch>()
            .HasIndex(m => m.PlayerPuuid);
    }
}