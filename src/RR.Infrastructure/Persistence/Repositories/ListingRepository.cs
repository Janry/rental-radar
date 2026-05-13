using Microsoft.EntityFrameworkCore;
using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.Infrastructure.Persistence.Repositories;

public sealed class ListingRepository(AppDbContext db) : IListingRepository
{
    public Task<bool> ExistsByExternalIdAsync(Guid sourceId, string externalId, CancellationToken ct = default) =>
        db.Listings.AsNoTracking().AnyAsync(l => l.SourceId == sourceId && l.ExternalId == externalId, ct);

    public async Task AddAsync(Listing listing, CancellationToken ct = default)
    {
        await db.Listings.AddAsync(listing, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Listing listing, CancellationToken ct = default)
    {
        db.Listings.Update(listing);
        await db.SaveChangesAsync(ct);
    }

    public Task<Listing?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Listings.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<Listing>> SearchAsync(ListingSearchCriteria criteria, CancellationToken ct = default)
    {
        var q = db.Listings.AsNoTracking().AsQueryable();

        if (criteria.LocationId is { } locId) q = q.Where(l => l.LocationId == locId);
        if (criteria.MaxPrice is { } maxP) q = q.Where(l => l.PricePerMonth != null && l.PricePerMonth <= maxP);
        if (criteria.MinPrice is { } minP) q = q.Where(l => l.PricePerMonth != null && l.PricePerMonth >= minP);
        if (criteria.Areas is { Count: > 0 } areas) q = q.Where(l => l.Area != null && areas.Contains(l.Area));
        if (criteria.PropertyTypes is { Count: > 0 } types) q = q.Where(l => l.PropertyType != null && types.Contains(l.PropertyType.Value));
        if (criteria.PetsAllowed is { } pets) q = q.Where(l => l.PetsAllowed == pets);
        if (criteria.PostedAfter is { } after) q = q.Where(l => l.PostedAt >= after);

        return await q.OrderByDescending(l => l.PostedAt)
            .Take(criteria.Limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Listing>> GetUnprocessedAsync(int limit, CancellationToken ct = default) =>
        await db.Listings.AsNoTracking()
            .Where(l => l.ProcessedAt == null)
            .OrderBy(l => l.ScrapedAt)
            .Take(limit)
            .ToListAsync(ct);

    public Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default) =>
        db.Listings.Where(l => l.ScrapedAt < cutoff).ExecuteDeleteAsync(ct);
}
