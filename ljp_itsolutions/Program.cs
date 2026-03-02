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
builder.Services.AddSession();
builder.Services.AddRateLimiter(options =>
{
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
builder.Services.AddHostedService<ljp_itsolutions.Services.OrderCleanupService>();
builder.Services.Configure<ljp_itsolutions.Services.CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.SameSite = SameSiteMode.Lax; 
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; 
        options.Cookie.IsEssential = true;
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

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
