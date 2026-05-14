using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using RR.Dashboard.Auth;
using RR.Dashboard.Components;
using RR.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var seqUrl = builder.Configuration["SEQ_URL"];
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.MinimumLevel.Information()
      .Enrich.FromLogContext()
      .Enrich.WithProperty("Service", "dashboard")
      .WriteTo.Console();
    if (!string.IsNullOrEmpty(seqUrl))
        lc.WriteTo.Seq(seqUrl);
});

// Dashboard не використовує scraper-stack — реєструємо лише EF Core factory,
// без AddInfrastructure (там reg-и для repos і scraper, які тут зайві).
var connStr = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default not configured.");
builder.Services.AddDbContextFactory<AppDbContext>(opts =>
    opts.UseSqlite(connStr).UseSnakeCaseNamingConvention());

builder.Services.Configure<DashboardAuthOptions>(builder.Configuration.GetSection(DashboardAuthOptions.SectionName));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opts =>
    {
        opts.Cookie.Name = "RR_Dashboard_Auth";
        opts.Cookie.HttpOnly = true;
        opts.Cookie.SameSite = SameSiteMode.Lax;
        opts.ExpireTimeSpan = TimeSpan.FromDays(14);
        opts.SlidingExpiration = true;
        opts.LoginPath = "/login";
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery();

builder.Services.AddMudServices();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/auth/login", async (HttpContext ctx, IOptions<DashboardAuthOptions> opts, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(ctx);
    var form = await ctx.Request.ReadFormAsync();
    var entered = form["Password"].ToString();
    var returnUrl = form["ReturnUrl"].ToString();
    if (string.IsNullOrEmpty(returnUrl) || !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        returnUrl = "/";

    var expected = opts.Value.Password;
    if (string.IsNullOrEmpty(expected))
        return Results.Redirect("/login?Error=not-configured");

    if (!FixedTimeStringEquals(entered, expected))
        return Results.Redirect($"/login?Error=wrong&ReturnUrl={Uri.EscapeDataString(returnUrl)}");

    var claims = new[] { new Claim(ClaimTypes.Name, "owner") };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Redirect(returnUrl);
});

app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();

static bool FixedTimeStringEquals(string a, string b)
{
    var ab = Encoding.UTF8.GetBytes(a);
    var bb = Encoding.UTF8.GetBytes(b);
    if (ab.Length != bb.Length) return false;
    return CryptographicOperations.FixedTimeEquals(ab, bb);
}

public partial class Program { }
