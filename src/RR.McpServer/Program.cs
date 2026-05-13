using RR.Infrastructure;
using RR.McpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// MCP сервери спілкуються через stdio — тому всі логи мусять йти в stderr,
// інакше зламається JSON-RPC протокол.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
    .CreateLogger();

// Host.CreateApplicationBuilder уже сам додає appsettings.json,
// appsettings.{Environment}.json та environment variables.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog();

// Підключаємо інфраструктуру (БД, репозиторії, AI клієнти)
builder.Services.AddInfrastructure(builder.Configuration);

// Реєструємо MCP сервер зі stdio транспортом і автодискавері tools
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<LocationTools>()
    .WithTools<RentalSearchTools>()
    .WithTools<FilterManagementTools>();

var app = builder.Build();
await app.RunAsync();
