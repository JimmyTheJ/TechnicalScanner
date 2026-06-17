using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TechnicalScanner.Application;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Options;
using TechnicalScanner.Domain.Entities;
using TechnicalScanner.Infrastructure.MarketData;
using TechnicalScanner.Infrastructure.MarketData.Universe;
using TechnicalScanner.Infrastructure.Notifications;
using TechnicalScanner.Infrastructure.Persistence;

namespace TechnicalScanner.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TechnicalScanner")
            ?? "Host=localhost;Port=5432;Database=technical_scanner;Username=postgres;Password=postgres";

        services.Configure<MarketDataOptions>(configuration.GetSection(MarketDataOptions.SectionName));
        services.Configure<AlertsOptions>(configuration.GetSection(AlertsOptions.SectionName));

        services.AddDbContext<TechnicalScannerDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ITechnicalScannerDbContext>(sp => sp.GetRequiredService<TechnicalScannerDbContext>());

        services.AddHttpClient<UsTickerUniverseLoader>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TechnicalScanner/1.0");
        });
        services.AddHttpClient<CanadianTickerUniverseLoader>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TechnicalScanner/1.0");
        });
        services.AddHttpClient<YahooFinanceMarketDataProvider>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TechnicalScanner/1.0");
        });

        services.AddSingleton<SeedMarketDataProvider>();
        services.AddSingleton<YahooFinanceMarketDataProvider>();
        services.AddSingleton<IMarketDataProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MarketDataOptions>>().Value;
            IMarketDataProvider inner = options.Provider.Equals("Seed", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<SeedMarketDataProvider>()
                : sp.GetRequiredService<YahooFinanceMarketDataProvider>();

            return new ResilientRateLimitedMarketDataProvider(
                inner,
                sp.GetRequiredService<IOptions<MarketDataOptions>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ResilientRateLimitedMarketDataProvider>>());
        });

        services.AddHttpClient(nameof(HttpAlertChannelBase), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<IAlertChannel, WebhookAlertChannel>();
        services.AddSingleton<IAlertChannel, DiscordAlertChannel>();
        services.AddSingleton<IAlertChannel, SlackAlertChannel>();
        services.AddSingleton<IAlertChannel, TelegramAlertChannel>();
        services.AddSingleton<IAlertChannel, EmailAlertChannel>();

        services.AddApplication();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TechnicalScannerDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await SeedData.SeedAsync(db, cancellationToken);
    }
}

internal static class SeedData
{
    public static async Task SeedAsync(TechnicalScannerDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.IndicatorDefinitions.AnyAsync(cancellationToken))
        {
            db.IndicatorDefinitions.AddRange(
                Definition("BMSB_W", "Bull Market Support Band", "BMSB", "W", new { smaPeriod = 20, emaPeriod = 21 }),
                Definition("RSI_14_D", "Daily RSI 14", "RSI", "D", new { period = 14 }),
                Definition("RSI_14_W", "Weekly RSI 14", "RSI", "W", new { period = 14 }),
                Definition("RSI_14_M", "Monthly RSI 14", "RSI", "M", new { period = 14 }),
                Definition("SMA_50_W", "50 Week SMA", "SMA", "W", new { period = 50 }),
                Definition("SMA_100_W", "100 Week SMA", "SMA", "W", new { period = 100 }),
                Definition("SMA_200_W", "200 Week SMA", "SMA", "W", new { period = 200 }),
                Definition("SMA_200_D", "200 Day SMA", "SMA", "D", new { period = 200 }),
                Definition("EMA_21_W", "21 Week EMA", "EMA", "W", new { period = 21 }),
                Definition("BB_20_2_D", "Daily Bollinger Bands 20/2", "BOLLINGER_BANDS", "D", new { period = 20, stdDevMultiplier = 2 }),
                Definition("BB_20_2_W", "Weekly Bollinger Bands 20/2", "BOLLINGER_BANDS", "W", new { period = 20, stdDevMultiplier = 2 }),
                Definition("BB_20_2_M", "Monthly Bollinger Bands 20/2", "BOLLINGER_BANDS", "M", new { period = 20, stdDevMultiplier = 2 }));
        }

        if (!await db.SignalDefinitions.AnyAsync(cancellationToken))
        {
            db.SignalDefinitions.AddRange(
                Signal("BMSB_BELOW", "Below Bull Market Support Band", "BMSB_W", "status", "=", "BelowBand"),
                Signal("BMSB_ABOVE", "Above Bull Market Support Band", "BMSB_W", "status", "=", "AboveBand"),
                Signal("RSI_D_OVERSOLD", "Daily RSI Oversold", "RSI_14_D", "category", "=", "Oversold"),
                Signal("RSI_W_OVERSOLD", "Weekly RSI Oversold", "RSI_14_W", "category", "=", "Oversold"),
                Signal("PRICE_ABOVE_200D_SMA", "Price Above 200D SMA", "SMA_200_D", "closeAboveSma", "=", true),
                Signal("PRICE_BELOW_200D_SMA", "Price Below 200D SMA", "SMA_200_D", "closeBelowSma", "=", true),
                Signal("BB_D_BELOW_LOWER", "Daily Close Below Lower Bollinger Band", "BB_20_2_D", "status", "=", "CloseBelowLowerBand"),
                Signal("BB_D_ABOVE_UPPER", "Daily Close Above Upper Bollinger Band", "BB_20_2_D", "status", "=", "CloseAboveUpperBand"),
                Signal("RSI_W_ABOVE_50", "Weekly RSI Above 50", "RSI_14_W", "rsi", ">=", 50));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static IndicatorDefinition Definition(string code, string name, string type, string timeframe, object parameters)
        => new()
        {
            Code = code,
            Name = name,
            IndicatorType = type,
            Timeframe = timeframe,
            ParametersJson = JsonSerializer.Serialize(parameters)
        };

    private static SignalDefinition Signal(string code, string name, string indicatorCode, string field, string op, object value)
        => new()
        {
            Code = code,
            Name = name,
            RuleJson = JsonSerializer.Serialize(new
            {
                indicatorCode,
                field,
                @operator = op,
                value
            })
        };
}
