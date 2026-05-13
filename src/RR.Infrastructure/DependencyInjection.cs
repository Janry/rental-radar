using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RR.Core.Abstractions;
using RR.Infrastructure.Ai;
using RR.Infrastructure.Persistence;
using RR.Infrastructure.Persistence.Repositories;
using RR.Infrastructure.Scraping;

namespace RR.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var connStr = cfg.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. " +
                "Add it to appsettings.json or via env var ConnectionStrings__Default.");

        services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlite(connStr).UseSnakeCaseNamingConvention());

        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IScrapeSourceRepository, ScrapeSourceRepository>();
        services.AddScoped<IListingRepository, ListingRepository>();
        services.AddScoped<IUserFilterRepository, UserFilterRepository>();

        // Phase 3 замінить реальною реалізацією на Playwright (auto-discovery).
        services.AddScoped<ISourceDiscoveryService, StubSourceDiscoveryService>();

        // Phase 4: scraping pipeline
        services.AddSingleton<IFacebookSession, FileBasedFacebookSession>();
        services.AddSingleton<IFacebookScraper, FacebookScraper>();   // тримає Chromium як singleton
        services.AddScoped<IAiListingExtractor, StubAiListingExtractor>();   // Phase 5 замінить на Claude

        // Phase 6+: IMatchingEngine, INotificationDispatcher

        return services;
    }
}
