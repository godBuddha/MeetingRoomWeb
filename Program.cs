using MeetingRoomWeb.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; // Tailscale handles HTTPS, app runs HTTP internally
        options.Cookie.SameSite = SameSiteMode.Lax;            // Lax allows cross-site redirect from Tailscale
    });

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Rate Limiting — block brute-force on login/register
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit       = 10;
        o.Window            = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit        = 0;
    });
    options.RejectionStatusCode = 429;
});

var app = builder.Build();

// Trust Tailscale reverse-proxy: forward IP, protocol AND host so antiforgery sees the correct origin
var fwdOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
// Allow all internal proxies (Tailscale runs locally — safe to trust)
fwdOptions.KnownIPNetworks.Clear();
fwdOptions.KnownProxies.Clear();
app.UseForwardedHeaders(fwdOptions);

// Only redirect to HTTPS when NOT behind a reverse proxy (direct local run with HTTPS cert)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    // Do NOT call UseHttpsRedirection — Tailscale already enforces HTTPS at the edge
}

// ─── Security Headers ────────────────────────────────────────────────────────
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Frame-Options"]        = "DENY";
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-XSS-Protection"]       = "1; mode=block";
    ctx.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com; " +
        "style-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self';";
    await next();
});

app.UseRateLimiter();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
