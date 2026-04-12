using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ljp_itsolutions.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddSingleton<ljp_itsolutions.Services.InMemoryStore>();
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;
        if (path.EndsWith(".js") || path.EndsWith(".css") || path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".jpeg") || path.EndsWith(".ico") || path.EndsWith(".woff") || path.EndsWith(".woff2"))
        {
            return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("stat");
        }

        return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 30,  
                Window = TimeSpan.FromSeconds(10), 
                SegmentsPerWindow = 5,  
                QueueLimit = 0
            });
    });

    options.AddFixedWindowLimiter(policyName: "login", options =>
    {
        options.PermitLimit = 5;
        options.Window = TimeSpan.FromSeconds(10);
        options.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddSingleton<ljp_itsolutions.Services.IEmailSender, ljp_itsolutions.Services.EmailSender>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ljp_itsolutions.Services.IPayMongoService, ljp_itsolutions.Services.PayMongoService>();
builder.Services.AddScoped<ljp_itsolutions.Services.IPhotoService, ljp_itsolutions.Services.PhotoService>();
builder.Services.AddHttpClient<ljp_itsolutions.Services.IRecipeService, ljp_itsolutions.Services.RecipeService>();
builder.Services.AddScoped<ljp_itsolutions.Services.IReceiptService, ljp_itsolutions.Services.ReceiptService>();
builder.Services.AddScoped<ljp_itsolutions.Services.IInventoryService, ljp_itsolutions.Services.InventoryService>();
builder.Services.AddScoped<ljp_itsolutions.Services.IAnalyticsService, ljp_itsolutions.Services.AnalyticsService>();
builder.Services.AddScoped<ljp_itsolutions.Services.IOrderService, ljp_itsolutions.Services.OrderService>();
builder.Services.AddScoped<ljp_itsolutions.Services.IOtpService, ljp_itsolutions.Services.OtpService>();
builder.Services.AddHostedService<ljp_itsolutions.Services.OrderCleanupService>();
builder.Services.Configure<ljp_itsolutions.Services.CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict; 
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; 
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api") || 
                    context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase) || 
                    context.Request.Path.StartsWithSegments("/config", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api") || 
                    context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase) || 
                    context.Request.Path.StartsWithSegments("/config", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=.\\SQLEXPRESS;Initial Catalog=ljp_itsolutions;Integrated Security=True;Trust Server Certificate=True";
builder.Services.AddDbContext<ljp_itsolutions.Data.ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlServerOptionsAction: sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 15,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(60); 
    });
});

builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<ljp_itsolutions.Models.User>, Microsoft.AspNetCore.Identity.PasswordHasher<ljp_itsolutions.Models.User>>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.Use(async (context, next) =>
{
    // 1. Maintenance Mode Enforcement
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ljp_itsolutions.Data.ApplicationDbContext>();
        var maintenanceMode = await db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "MaintenanceMode");
        
        if (maintenanceMode?.SettingValue == "true" && 
            !context.Request.Path.StartsWithSegments("/Account/Login") && 
            !context.User.IsInRole(UserRoles.SuperAdmin) && 
            !context.User.IsInRole(UserRoles.Admin))
        {
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync("<html><head><title>Maintenance</title><link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'></head><body class='bg-light'><div class='container text-center' style='padding-top:100px;'><h1 class='display-4 fw-bold'>🚀 System Maintenance</h1><p class='lead'>We are currently performing infrastructure upgrades to improve your experience. Personnel may log in for administrative access.</p><a href='/Account/Login' class='btn btn-primary rounded-pill px-4'>Staff Login</a></div></body></html>");
            return;
        }
    }

    try
    {
        // 2. Security Headers
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("X-Robots-Tag", "noindex, nofollow");
        context.Response.Headers.Append("Referrer-Policy", "same-origin");
        context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), interest-cohort=()");
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://www.google.com https://www.gstatic.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://www.gstatic.com; font-src 'self' https://fonts.gstatic.com https://cdnjs.cloudflare.com; img-src 'self' data: https: https://www.gstatic.com; connect-src 'self' https://chart.googleapis.com; frame-src 'self' https://www.google.com;");

        await next();
    }
    catch (Exception ex)
    {
        // 3. Global Infrastructure Error Logging (Information Assurance)
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ljp_itsolutions.Data.ApplicationDbContext>();
            db.SecurityLogs.Add(new SecurityLog {
                EventType = "SystemCriticalError",
                Severity = "Critical",
                Description = $"Global Exception in [{context.Request.Method}] {context.Request.Path}: {ex.Message}",
                Timestamp = DateTime.UtcNow,
                IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0"
            });
            await db.SaveChangesAsync();
        }
        context.Response.Redirect("/Home/Error");
    }
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// ── Visitor Access Log ──────────────────────────────────────────────────────
// Logs every page visit to the AuditLog table
var _visitThrottle = new System.Collections.Concurrent.ConcurrentDictionary<string, DateTime>();
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var isStaticAsset = path.EndsWith(".js") || path.EndsWith(".css") || path.EndsWith(".png")
                     || path.EndsWith(".jpg") || path.EndsWith(".jpeg") || path.EndsWith(".ico")
                     || path.EndsWith(".woff") || path.EndsWith(".woff2") || path.EndsWith(".map")
                     || path.StartsWith("/lib/") || path.StartsWith("/_framework/");

    if (!isStaticAsset)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;
        var throttleKey = ip + path;
        var shouldLog = !_visitThrottle.TryGetValue(throttleKey, out var lastLogged)
                        || (now - lastLogged).TotalSeconds > 5;

        if (shouldLog)
        {
            _visitThrottle[throttleKey] = now;
            // Clean up old entries every ~500 visits to avoid unbounded growth
            if (_visitThrottle.Count > 500)
            {
                var stale = _visitThrottle.Where(kv => (now - kv.Value).TotalMinutes > 5).Select(kv => kv.Key).ToList();
                foreach (var k in stale) _visitThrottle.TryRemove(k, out _);
            }

            try
            {
                var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
                var username = isAuthenticated ? (context.User.Identity?.Name ?? "Authenticated") : "Anonymous Visitor";
                var role = isAuthenticated ? (context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Unknown") : "Guest";
                var method = context.Request.Method;
                var userAgent = context.Request.Headers.UserAgent.ToString();

                Guid? userId = null;
                if (isAuthenticated)
                {
                    var userIdStr = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (Guid.TryParse(userIdStr, out var parsedId)) userId = parsedId;
                }

                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ljp_itsolutions.Data.ApplicationDbContext>();
                db.AuditLogs.Add(new ljp_itsolutions.Models.AuditLog
                {
                    Action = $"[{method}] {path}",
                    Details = $"User: {username} | Role: {role}",
                    Timestamp = now,
                    UserID = userId,
                    IpAddress = ip,
                    UserAgent = userAgent
                });
                await db.SaveChangesAsync();
            }
            catch { /* Never break the request pipeline if logging fails */ }
        }
    }

    await next();
});
// ───────────────────────────────────────────────────────────────────────────

app.MapStaticAssets();

app.MapControllers(); // maps [ApiController] routes (e.g. /api/recipes)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    ljp_itsolutions.Data.DbInitializer.Initialize(app.Services);
}

app.Run();
