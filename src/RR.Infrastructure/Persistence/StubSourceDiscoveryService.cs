using RR.Core.Abstractions;
using RR.Core.Domain;

namespace RR.Infrastructure.Persistence;

/// <summary>
/// Заглушка до Phase 3, де буде реальний Playwright-based пошук FB-груп.
/// Зараз просто повертає порожній список, щоб DI для LocationTools.ctor не падав
/// і add_location/discover_sources MCP-tools відповідали без помилки.
/// </summary>
public sealed class StubSourceDiscoveryService : ISourceDiscoveryService
{
    public Task<IReadOnlyList<ScrapeSource>> DiscoverAsync(
        Location location,
        int maxCandidates = 20,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ScrapeSource>>(Array.Empty<ScrapeSource>());
    }
}
