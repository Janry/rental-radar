using Microsoft.EntityFrameworkCore;
using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.Infrastructure.Persistence.Repositories;

public sealed class ScrapeSourceRepository(AppDbContext db) : IScrapeSourceRepository
{
    public Task<ScrapeSource?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ScrapeSources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<ScrapeSource>> GetByLocationAsync(Guid locationId, CancellationToken ct = default) =>
        await db.ScrapeSources.AsNoTracking()
            .Where(s => s.LocationId == locationId)
            .OrderByDescending(s => s.RelevanceScore)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ScrapeSource>> GetEnabledAsync(CancellationToken ct = default) =>
        await db.ScrapeSources.AsNoTracking()
            .Where(s => s.IsEnabled)
            .ToListAsync(ct);

    public async Task AddAsync(ScrapeSource source, CancellationToken ct = default)
    {
        await db.ScrapeSources.AddAsync(source, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<ScrapeSource> sources, CancellationToken ct = default)
    {
        await db.ScrapeSources.AddRangeAsync(sources, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ScrapeSource source, CancellationToken ct = default)
    {
        db.ScrapeSources.Update(source);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.ScrapeSources.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
    }
}
