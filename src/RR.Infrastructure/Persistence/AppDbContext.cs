using Microsoft.EntityFrameworkCore;
using RR.Core.Domain;

namespace RR.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<ScrapeSource> ScrapeSources => Set<ScrapeSource>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<UserFilter> UserFilters => Set<UserFilter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
