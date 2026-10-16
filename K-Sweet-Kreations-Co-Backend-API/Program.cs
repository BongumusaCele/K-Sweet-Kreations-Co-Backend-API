
using K_Sweet_Kreations_Co_Backend_API.Data;
using K_Sweet_Kreations_Co_Backend_API.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using K_Sweet_Kreations_Co_Backend_API.Controllers;
using K_Sweet_Kreations_Co_Backend_API.Services;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;
using K_Sweet_Kreations_Co_Backend_API.Seeders;
using K_Sweet_Kreations_Co_Backend_API.Middleware;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Configure SQL Server database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    else options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Configure API controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            ReferenceHandler.IgnoreCycles;
    });

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference("Bearer", document),
                new List<string>()
            }
        });
});
builder.Services.AddScoped<
    IPasswordHasher<UserAccount>,
    PasswordHasher<UserAccount>>();


var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT secret key is not configured.");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "JWT secret key must be at least 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.Response.Cookies.Delete("ksk_session", new CookieOptions { Path = "/api" });
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("ksk_session", out var cookie)) context.Token = cookie;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var principal = context.Principal!;
                var rawId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
                if (!int.TryParse(rawId, out var id) || jti == null) { context.Fail("Invalid session."); return; }
                var hash = AuthController.HashSession(jti);
                var valid = await db.UserSessions.AnyAsync(s => s.UserId == id && s.RefreshTokenHash == hash &&
                    s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow && s.UserAccount.Active && s.UserAccount.Role == principal.FindFirstValue(ClaimTypes.Role));
                if (!valid) context.Fail("Session expired. Sign in again.");
            }
        };
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtKey)),

                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<WebsiteService>();
builder.Services.AddScoped<PrivateImageStore>();
builder.Services.AddSingleton<MediaStorage>();
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimits:RequestsPerMinute", 120), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("identity", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimits:IdentityPerMinute", 15), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});


var app = builder.Build();

app.Use(async (context, next) => {
    if (app.Configuration["Website:RedirectOrigin"] is string destination && Uri.TryCreate(destination, UriKind.Absolute, out var target) &&
        !string.Equals(context.Request.Host.Host, target.Host, StringComparison.OrdinalIgnoreCase))
    {
        if (context.Request.Method is "GET" or "HEAD") context.Response.Redirect(destination.TrimEnd('/') + context.Request.Path + context.Request.QueryString, permanent: true);
        else { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { message = "The bakery website has moved. Reload the page to continue at " + destination }); }
        return;
    }
    await next();
});

// Explicit deployment command; HTTP startup never applies schema changes.
if (args.Contains("--verify-media-storage"))
{
    var storage = app.Services.GetRequiredService<MediaStorage>();
    await storage.MigrateLocalFiles();
    foreach (var kind in new[] { "products", "payment-proofs" })
    {
        var name = Guid.NewGuid().ToString("N") + (kind == "products" ? ".png" : ".pdf");
        var bytes = Encoding.ASCII.GetBytes(kind == "products" ? "storage roundtrip check" : "%PDF-1.4\nstorage roundtrip check");
        try {
            await storage.Save(kind, name, new MemoryStream(bytes));
            await using var input = await storage.Open(kind, name) ?? throw new InvalidOperationException("Stored file is missing.");
            using var output = new MemoryStream();
            await input.CopyToAsync(output);
            if (!bytes.SequenceEqual(output.ToArray())) throw new InvalidOperationException("Stored file contents do not match.");
        } finally { await storage.Delete(kind, name); }
    }
    Console.WriteLine("Media storage upload, download and deletion verified; no database records created.");
    return;
}
if (args.Contains("--initialize-database"))
{
    await DatabaseDeployment.InitializeAsync(app.Services);
    return;
}

app.UseMiddleware<GlobalExceptionMiddleware>();

// Enable Swagger during development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


if (!app.Environment.IsEnvironment("Testing")) app.UseHttpsRedirection();

// Enable access to uploaded product images
app.UseDefaultFiles();
app.UseStaticFiles();
if (builder.Configuration["Storage:PublicRoot"] is string publicRoot && !string.IsNullOrWhiteSpace(publicRoot))
{
    Directory.CreateDirectory(publicRoot);
    app.UseStaticFiles(new StaticFileOptions {
        FileProvider = new PhysicalFileProvider(Path.GetFullPath(publicRoot)),
        RequestPath = "/uploads/products"
    });
}

app.UseCors();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
        var origin = context.Request.Headers.Origin.ToString();
        if (context.Request.Method is not ("GET" or "HEAD" or "OPTIONS") && origin.Length > 0 &&
            origin != $"{context.Request.Scheme}://{context.Request.Host}" && !origins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { message = "This website origin is not permitted." });
            return;
        }
    }
    await next();
});
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();
app.MapGet("/api/health", async (ApplicationDbContext db) => {
    try {
        return await db.Database.CanConnectAsync()
            ? Results.Ok(new { status = "healthy" })
            : Results.Json(new { status = "unavailable" }, statusCode: 503);
    } catch {
        return Results.Json(new { status = "unavailable" }, statusCode: 503);
    }
}).AllowAnonymous();

if (app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
}

if (builder.Configuration.GetValue<bool>("SeedAdmin:Enabled"))
    await AdminSeeder.SeedAdminAsync(app.Services);



await app.Services.GetRequiredService<MediaStorage>().MigrateLocalFiles();
app.MapGet("/uploads/products/{name}", async (string name, MediaStorage storage) => {
    if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-f0-9]{32}\.(jpg|jpeg|png|webp)$")) return Results.NotFound();
    var stream = await storage.Open("products", name);
    return stream == null ? Results.NotFound() : Results.File(stream, PrivateImageStore.Mime(name));
}).AllowAnonymous();
app.Run();

public partial class Program { }
