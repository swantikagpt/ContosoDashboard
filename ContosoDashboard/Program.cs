using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using ContosoDashboard.Models;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add authentication state provider for Blazor
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Mock Authentication (Cookie-based for training purposes)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Add authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Employee", policy => policy.RequireRole("Employee", "TeamLead", "ProjectManager", "Administrator"));
    options.AddPolicy("TeamLead", policy => policy.RequireRole("TeamLead", "ProjectManager", "Administrator"));
    options.AddPolicy("ProjectManager", policy => policy.RequireRole("ProjectManager", "Administrator"));
    options.AddPolicy("Administrator", policy => policy.RequireRole("Administrator"));
});

// Register application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

if (builder.Environment.IsDevelopment() && builder.Configuration["ClamAv:Host"] is null)
{
    // No ClamAV configured for local dev (research.md #1 follow-up) — never used outside Development.
    builder.Services.AddScoped<IVirusScanner, NoOpVirusScanner>();
}
else
{
    builder.Services.AddScoped<IVirusScanner, ClamAvVirusScanner>();
}

// Add HttpContextAccessor for accessing user claims
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated(); // For development - use migrations in production
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    // Use HSTS even in development for training purposes
    app.UseHsts();
}

// Add security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    
    // Content Security Policy for Blazor Server
    context.Response.Headers["Content-Security-Policy"] = 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "font-src 'self' https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' wss: ws:;";
    
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Document download/preview endpoints (contracts/document-download-endpoint.md).
// Files live outside wwwroot, so these are the only HTTP-level access path to file bytes.
app.MapGet("/api/documents/{id:int}/download", async (int id, HttpContext httpContext, IDocumentService documentService, IFileStorageService fileStorageService, ApplicationDbContext dbContext) =>
{
    var userId = GetUserId(httpContext);
    if (userId is null) return Results.Unauthorized();

    // GetByIdAsync returns null for both "does not exist" and "not authorized" — never disclose which.
    var document = await documentService.GetByIdAsync(userId.Value, id);
    if (document is null) return Results.NotFound();

    var stream = await fileStorageService.DownloadAsync(document.FilePath);

    dbContext.DocumentActivityLogs.Add(new DocumentActivityLog
    {
        DocumentId = document.DocumentId,
        DocumentTitleSnapshot = document.Title,
        ActionType = DocumentActivityType.Download,
        PerformedByUserId = userId.Value,
        OccurredDate = DateTime.UtcNow
    });
    await dbContext.SaveChangesAsync();

    return Results.Stream(stream, document.FileType, document.FileName);
}).RequireAuthorization();

app.MapGet("/api/documents/{id:int}/preview", async (int id, HttpContext httpContext, IDocumentService documentService, IFileStorageService fileStorageService) =>
{
    var userId = GetUserId(httpContext);
    if (userId is null) return Results.Unauthorized();

    var document = await documentService.GetByIdAsync(userId.Value, id);
    if (document is null) return Results.NotFound();

    var previewableTypes = new[] { "application/pdf", "image/jpeg", "image/png" };
    if (!previewableTypes.Contains(document.FileType))
    {
        return Results.BadRequest("This file type cannot be previewed.");
    }

    var stream = await fileStorageService.DownloadAsync(document.FilePath);
    httpContext.Response.Headers["Content-Disposition"] = "inline";
    return Results.Stream(stream, document.FileType);
}).RequireAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

static int? GetUserId(HttpContext context)
{
    var claim = context.User.FindFirst(ClaimTypes.NameIdentifier);
    return claim != null && int.TryParse(claim.Value, out var userId) ? userId : null;
}
