using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server;
using MobileAICLI.Components;
using MobileAICLI.Hubs;
using MobileAICLI.Models;
using MobileAICLI.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure settings
builder.Services.Configure<MobileAICLISettings>(
    builder.Configuration.GetSection("MobileAICLI"));

// Add HttpContextAccessor for accessing HttpContext in Blazor components
builder.Services.AddHttpContextAccessor();

// Configure authentication
var settings = builder.Configuration.GetSection("MobileAICLI").Get<MobileAICLISettings>() ?? new MobileAICLISettings();

if (settings.EnableAuthentication)
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/login";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(settings.SessionTimeoutMinutes);
            options.SlidingExpiration = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Use Secure in production with HTTPS
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

    builder.Services.AddAuthorization();
    builder.Services.AddCascadingAuthenticationState();
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add controllers for authentication API
builder.Services.AddControllers();

// Add HttpClient for Blazor components
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(sp.GetRequiredService<NavigationManager>().BaseUri)
});

// Add SignalR for real-time communication
builder.Services.AddSignalR();

// Add application services
builder.Services.AddScoped<RepositoryContext>();
builder.Services.AddSingleton<AuditLogService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<TerminalService>();
builder.Services.AddScoped<CopilotService>();
builder.Services.AddScoped<ShellStreamingService>();
builder.Services.AddScoped<CopilotStreamingService>();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ToolDiscoveryService>();
builder.Services.AddScoped<GitService>();

// Add Interactive Mode services
// Interactive Mode services - singleton for session management
builder.Services.AddSingleton<ICopilotSessionService, CopilotSessionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

// Add authentication middleware
if (settings.EnableAuthentication)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map controllers
app.MapControllers();

// Map SignalR Hubs - require authentication if enabled
if (settings.EnableAuthentication)
{
    app.MapHub<ShellHub>("/shellhub").RequireAuthorization();
    app.MapHub<CopilotHub>("/copilothub").RequireAuthorization();
    app.MapHub<CopilotInteractiveHub>("/hubs/copilot-interactive").RequireAuthorization();
    app.MapHub<CopilotInteractiveHub>("/copilotinteractivehub").RequireAuthorization();
    app.MapHub<TestHub>("/testhub").RequireAuthorization();
    app.MapHub<GitHub>("/github").RequireAuthorization();
}
else
{
    app.MapHub<ShellHub>("/shellhub");
    app.MapHub<CopilotHub>("/copilothub");
    app.MapHub<CopilotInteractiveHub>("/hubs/copilot-interactive");
    app.MapHub<CopilotInteractiveHub>("/copilotinteractivehub");
    app.MapHub<TestHub>("/testhub");
    app.MapHub<GitHub>("/github");
}

// Map SignalR Hub for settings management
if (settings.EnableAuthentication)
{
    app.MapHub<SettingsHub>("/settingshub").RequireAuthorization();
}
else
{
    app.MapHub<SettingsHub>("/settingshub");
}

app.Run();
