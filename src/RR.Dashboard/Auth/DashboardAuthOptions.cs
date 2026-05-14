namespace RR.Dashboard.Auth;

public sealed class DashboardAuthOptions
{
    public const string SectionName = "Dashboard";

    /// <summary>
    /// Plaintext-пароль для одного користувача. Краще передавати через env
    /// `DASHBOARD_PASSWORD`. Порівняння — constant-time, але це не bcrypt:
    /// для public exposure треба робити PBKDF2-хеш у production.
    /// За SSH-tunnel-ом / Tailscale у personal use — достатньо.
    /// </summary>
    public string Password { get; init; } = "";

    public string CookieName { get; init; } = "RR_Dashboard_Auth";

    /// <summary>Скільки днів cookie живе після успішного логіну.</summary>
    public int CookieDaysValid { get; init; } = 14;
}
