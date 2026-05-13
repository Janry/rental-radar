using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RR.Infrastructure;
using RR.Scraper.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<ScrapingOptions>(builder.Configuration.GetSection(ScrapingOptions.SectionName));
builder.Services.AddSingleton<ScrapingPass>();
builder.Services.AddHostedService<ScrapingBackgroundService>();

var host = builder.Build();
await host.RunAsync();
