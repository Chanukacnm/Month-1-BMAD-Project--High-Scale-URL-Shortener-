using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using UrlShortener.Application.Common.Interfaces;
using UrlShortener.Application.Common.Services;
using UrlShortener.Application.Urls.Commands;
using UrlShortener.Infrastructure.Persistence;
using UrlShortener.Infrastructure.Services;
using UrlShortener.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (IsPostgresReachable(defaultConnection))
    {
        options.UseNpgsql(defaultConnection);
    }
    else
    {
        options.UseSqlite("Data Source=urlshortener.db");
    }
});

builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
builder.Services.AddScoped<IShortCodeGenerator, ShortCodeGenerator>();
builder.Services.AddSingleton<IShardConnectionFactory, ShardConnectionFactory>();
builder.Services.AddSingleton<IShardRouter, ShardRouter>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = builder.Configuration.GetConnectionString("Redis");
    if (string.IsNullOrEmpty(configuration) || configuration == "localhost:6379")
    {
        // Standalone/Local: Return null to trigger fallback in RedisCacheService
        return null!;
    }
    
    try
    {
        var options = ConfigurationOptions.Parse(configuration);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 1000; // Shorter timeout for standalone detection
        options.SyncTimeout = 1000;
        return ConnectionMultiplexer.Connect(options);
    }
    catch
    {
        return null!;
    }
});

static bool IsPostgresReachable(string? connectionString)
{
    if (string.IsNullOrEmpty(connectionString)) return false;
    try
    {
        var host = "localhost";
        var port = 5432;
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            var key = kv[0].Trim().ToLowerInvariant();
            var value = kv[1].Trim();
            if (key == "host" || key == "server") host = value;
            else if (key == "port") int.TryParse(value, out port);
        }
        using var client = new System.Net.Sockets.TcpClient();
        var result = client.BeginConnect(host, port, null, null);
        var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
        if (success && client.Connected)
        {
            client.EndConnect(result);
            return true;
        }
        return false;
    }
    catch { return false; }
}

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateShortUrlCommand).Assembly));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<AnalyticsMiddleware>();

// Configure the HTTP request pipeline.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var shardFactory = services.GetRequiredService<IShardConnectionFactory>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    for (int i = 0; i < shardFactory.ShardCount; i++)
    {
        try
        {
            var context = shardFactory.CreateDbContext(i);
            if (context is DbContext efContext)
            {
                if (efContext.Database.IsSqlite())
                {
                    efContext.Database.EnsureCreated();
                }
                else if (efContext.Database.GetPendingMigrations().Any())
                {
                    efContext.Database.Migrate();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating shard {ShardIndex}.", i);
        }
    }
}

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
