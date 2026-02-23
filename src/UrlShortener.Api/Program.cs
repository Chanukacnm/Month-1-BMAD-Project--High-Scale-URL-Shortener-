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
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
        options.ConnectTimeout = 5000;
        options.SyncTimeout = 5000;
        return ConnectionMultiplexer.Connect(options);
    }
    catch
    {
        return null!;
    }
});

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
