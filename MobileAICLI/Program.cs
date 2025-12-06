using MobileAICLI.Components;
using MobileAICLI.Models;
using MobileAICLI.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure settings
builder.Services.Configure<MobileAICLISettings>(
    builder.Configuration.GetSection("MobileAICLI"));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add application services
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<TerminalService>();
builder.Services.AddScoped<CopilotService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
