using Microsoft.EntityFrameworkCore;
using SalesChatbot.Api;
using SalesChatbot.Data;
using SalesChatbot.Data.Seed;
using SalesChatbot.Infrastructure.Dial;
using SalesChatbot.Services;
using SalesChatbot.Services.Interfaces;
using SalesChatbot.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<DialOptions>(builder.Configuration.GetSection(DialOptions.SectionName));
builder.Services.AddHttpClient<IDialClient, DialClient>();

builder.Services.AddDbContext<SalesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SalesDb")));

builder.Services.AddScoped<ITextToSqlService, TextToSqlService>();
builder.Services.AddScoped<ISqlExecutionService, SqlExecutionService>();
builder.Services.AddScoped<IResultInterpreterService, ResultInterpreterService>();
builder.Services.AddScoped<IConversationService, ConversationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
    await db.Database.MigrateAsync();
    await SalesDataSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapChatEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
