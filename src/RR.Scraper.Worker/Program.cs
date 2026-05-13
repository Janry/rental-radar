using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RR.Infrastructure;
using RR.Scraper.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Logging: Console завжди; Seq — лише якщо SEQ_URL заданий (в Docker = http://seq:5341).
var seqUrl = builder.Configuration["SEQ_URL"];
builder.Services.AddSerilog(lc =>
{
    lc.MinimumLevel.Information()
      .Enrich.FromLogContext()
      .Enrich.WithProperty("Service", "scraper")
      .WriteTo.Console();
    if (!string.IsNullOrEmpty(seqUrl))
        lc.WriteTo.Seq(seqUrl);
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<ScrapingOptions>(builder.Configuration.GetSection(ScrapingOptions.SectionName));
builder.Services.AddSingleton<ScrapingPass>();
builder.Services.AddHostedService<ScrapingBackgroundService>();

var host = builder.Build();
await host.RunAsync();
