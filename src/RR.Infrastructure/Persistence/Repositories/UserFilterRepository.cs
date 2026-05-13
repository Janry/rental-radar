using Microsoft.EntityFrameworkCore;
using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.Infrastructure.Persistence.Repositories;

public sealed class UserFilterRepository(AppDbContext db) : IUserFilterRepository
{
    public Task<UserFilter?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.UserFilters.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<UserFilter>> GetActiveAsync(CancellationToken ct = default) =>
        await db.UserFilters.AsNoTracking().Where(f => f.IsActive).ToListAsync(ct);

    public async Task<IReadOnlyList<UserFilter>> GetActiveByLocationAsync(Guid locationId, CancellationToken ct = default) =>
        await db.UserFilters.AsNoTracking()
            .Where(f => f.LocationId == locationId && f.IsActive)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserFilter>> GetByChatIdAsync(long telegramChatId, CancellationToken ct = default) =>
        await db.UserFilters.AsNoTracking()
            .Where(f => f.TelegramChatId == telegramChatId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(UserFilter filter, CancellationToken ct = default)
    {
        await db.UserFilters.AddAsync(filter, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserFilter filter, CancellationToken ct = default)
    {
        db.UserFilters.Update(filter);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.UserFilters.Where(f => f.Id == id).ExecuteDeleteAsync(ct);
    }
}
