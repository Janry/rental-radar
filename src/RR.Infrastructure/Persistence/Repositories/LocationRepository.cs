using Microsoft.EntityFrameworkCore;
using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.Infrastructure.Persistence.Repositories;

public sealed class LocationRepository(AppDbContext db) : ILocationRepository
{
    public Task<Location?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Locations.AsNoTracking().Include(l => l.Sources).FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<Location?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        db.Locations.AsNoTracking().Include(l => l.Sources).FirstOrDefaultAsync(l => l.Slug == slug, ct);

    public async Task<IReadOnlyList<Location>> GetActiveAsync(CancellationToken ct = default) =>
        await db.Locations.AsNoTracking().Include(l => l.Sources)
            .Where(l => l.IsActive)
            .OrderBy(l => l.Name)
            .ToListAsync(ct);

    public async Task AddAsync(Location location, CancellationToken ct = default)
    {
        await db.Locations.AddAsync(location, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Location location, CancellationToken ct = default)
    {
        db.Locations.Update(location);
        await db.SaveChangesAsync(ct);
    }
}
