using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RR.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        // TODO Phase 2: register AppDbContext (Npgsql), EF Core
        // TODO Phase 2: register IListingRepository, IUserFilterRepository
        // TODO Phase 3: register IFacebookScraper (Playwright)
        // TODO Phase 3: register IAiListingExtractor (Claude API)
        return services;
    }
}
