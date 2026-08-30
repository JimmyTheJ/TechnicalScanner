using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TechnicalScanner.Application;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Models.Profiles;
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
        EnsureIndicator(db, "BMSB_W", "Bull Market Support Band", "BMSB", "W", new { smaPeriod = 20, emaPeriod = 21 });
        EnsureIndicator(db, "RSI_14_D", "Daily RSI 14", "RSI", "D", new { period = 14 });
        EnsureIndicator(db, "RSI_14_W", "Weekly RSI 14", "RSI", "W", new { period = 14 });
        EnsureIndicator(db, "RSI_14_M", "Monthly RSI 14", "RSI", "M", new { period = 14 });
        EnsureIndicator(db, "SMA_50_W", "50 Week SMA", "SMA", "W", new { period = 50 });
        EnsureIndicator(db, "SMA_100_W", "100 Week SMA", "SMA", "W", new { period = 100 });
        EnsureIndicator(db, "SMA_200_W", "200 Week SMA", "SMA", "W", new { period = 200 });
        EnsureIndicator(db, "SMA_200_D", "200 Day SMA", "SMA", "D", new { period = 200 });
        EnsureIndicator(db, "EMA_21_W", "21 Week EMA", "EMA", "W", new { period = 21 });
        EnsureIndicator(db, "BB_20_2_D", "Daily Bollinger Bands 20/2", "BOLLINGER_BANDS", "D", new { period = 20, stdDevMultiplier = 2 });
        EnsureIndicator(db, "BB_20_2_W", "Weekly Bollinger Bands 20/2", "BOLLINGER_BANDS", "W", new { period = 20, stdDevMultiplier = 2 });
        EnsureIndicator(db, "BB_20_2_M", "Monthly Bollinger Bands 20/2", "BOLLINGER_BANDS", "M", new { period = 20, stdDevMultiplier = 2 });
        EnsureIndicator(db, "VOL_SMA_20_D", "20 Day Volume SMA", "VOLUME", "D", new { period = 20 });
        EnsureIndicator(db, "ATR_14_D", "14 Day ATR", "ATR", "D", new { period = 14 });
        EnsureIndicator(db, "RANGE_252_D", "252 Day High/Low Range", "PRICE_RANGE", "D", new { period = 252 });
        EnsureIndicator(db, "ROC_21_D", "21 Day Rate of Change", "ROC", "D", new { period = 21 });
        EnsureIndicator(db, "ROC_63_D", "63 Day Rate of Change", "ROC", "D", new { period = 63 });
        EnsureIndicator(db, "RS_SPY_63_D", "63 Day vs SPY", "RELATIVE_STRENGTH", "D", new { period = 63, benchmarkSymbol = "SPY" });
        EnsureIndicator(db, "RS_QQQ_63_D", "63 Day vs QQQ", "RELATIVE_STRENGTH", "D", new { period = 63, benchmarkSymbol = "QQQ" });
        EnsureIndicator(db, "RS_XIU_63_D", "63 Day vs XIU.TO", "RELATIVE_STRENGTH", "D", new { period = 63, benchmarkSymbol = "XIU.TO" });
        EnsureIndicator(db, "DRAWDOWN_ATH_D", "Drawdown from all-time high", "DRAWDOWN", "D", new { });

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

        EnsureProfile(
            db,
            "DEEP_PULLBACK",
            "Deep Pullback",
            "Ranks washouts using BMSB, RSI, 200D SMA, 50W SMA, and daily Bollinger.",
            new ProfileUniverse { IncludeEtfs = true },
            [
                Spec("BMSB_W"),
                Spec("RSI_14_D"),
                Spec("RSI_14_W"),
                Spec("SMA_200_D"),
                Spec("SMA_50_W"),
                Spec("BB_20_2_D")
            ],
            """{"all":[]}""",
            [
                Weight("belowBmsb", "BMSB_W", "status", "=", "BelowBand", 2),
                Weight("dailyRsiOversold", "RSI_14_D", "rsi", "<", 30, 1),
                Weight("weeklyRsiWeak", "RSI_14_W", "rsi", "<", 40, 1),
                Weight("below200dSma", "SMA_200_D", "distanceFromSmaPct", "<", 0, 1),
                Weight("below50wSma", "SMA_50_W", "distanceFromSmaPct", "<", 0, 1),
                Weight("belowDailyBbLower", "BB_20_2_D", "status", "=", "CloseBelowLowerBand", 1)
            ]);

        EnsureProfile(
            db,
            "CHEAP_VS_MA",
            "Cheap vs moving averages",
            "Close below the 200-day SMA, 50-week SMA, and 21-week EMA.",
            new ProfileUniverse { IncludeEtfs = true },
            [Spec("SMA_200_D"), Spec("SMA_50_W"), Spec("EMA_21_W")],
            """
            {"all":[
              {"indicatorCode":"SMA_200_D","field":"distanceFromSmaPct","operator":"<","value":0},
              {"indicatorCode":"SMA_50_W","field":"closeBelowSma","operator":"=","value":true},
              {"indicatorCode":"EMA_21_W","field":"distanceFromEmaPct","operator":"<","value":0}
            ]}
            """,
            [
                Weight("below200d", "SMA_200_D", "distanceFromSmaPct", "<", 0, 1),
                Weight("below50w", "SMA_50_W", "closeBelowSma", "=", true, 1),
                Weight("below21wEma", "EMA_21_W", "distanceFromEmaPct", "<", 0, 1)
            ]);

        EnsureProfile(
            db,
            "RSI_TURNING_UP",
            "RSI turning up",
            "Daily RSI crossed back above 30, with a rising RSI as extra score.",
            new ProfileUniverse { IncludeEtfs = true },
            [Spec("RSI_14_D")],
            """{"indicatorCode":"RSI_14_D","field":"rsi","operator":"crossed_above","value":30}""",
            [
                Weight("crossedAbove30", "RSI_14_D", "rsi", "crossed_above", 30, 2),
                Weight("rsiRising", "RSI_14_D", "rsi", "rising", 0, 1)
            ]);

        EnsureProfile(
            db,
            "VOLUME_CONFIRMED_CHEAP",
            "Volume-confirmed cheap",
            "At least 10% below the 200-day SMA, daily RSI under 40, and relative volume above 1.5.",
            new ProfileUniverse { IncludeEtfs = true },
            [Spec("SMA_200_D"), Spec("RSI_14_D"), Spec("VOL_SMA_20_D")],
            """
            {"all":[
              {"indicatorCode":"SMA_200_D","field":"distanceFromSmaPct","operator":"<","value":-10},
              {"indicatorCode":"RSI_14_D","field":"rsi","operator":"<","value":40},
              {"indicatorCode":"VOL_SMA_20_D","field":"relativeVolume","operator":">","value":1.5}
            ]}
            """,
            [
                Weight("deepVs200d", "SMA_200_D", "distanceFromSmaPct", "<", -10, 2),
                Weight("weakRsi", "RSI_14_D", "rsi", "<", 40, 1),
                Weight("highRelativeVolume", "VOL_SMA_20_D", "relativeVolume", ">", 1.5, 1)
            ]);

        EnsureProfile(
            db,
            "WEAK_VS_SPY",
            "Weak vs SPY",
            "Lagging SPY over 63 days with a negative 21-day return.",
            new ProfileUniverse { IncludeEtfs = true },
            [Spec("RS_SPY_63_D"), Spec("ROC_21_D")],
            """
            {"all":[
              {"indicatorCode":"RS_SPY_63_D","field":"relativeStrengthPct","operator":"<","value":0},
              {"indicatorCode":"ROC_21_D","field":"rocPct","operator":"<","value":0}
            ]}
            """,
            [
                Weight("laggingSpy", "RS_SPY_63_D", "relativeStrengthPct", "<", 0, 2),
                Weight("negativeRoc21", "ROC_21_D", "rocPct", "<", 0, 1)
            ]);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureIndicator(
        TechnicalScannerDbContext db,
        string code,
        string name,
        string type,
        string timeframe,
        object parameters)
    {
        if (db.IndicatorDefinitions.Any(d => d.Code == code))
        {
            return;
        }

        db.Add(Definition(code, name, type, timeframe, parameters));
    }

    private static void EnsureProfile(
        TechnicalScannerDbContext db,
        string code,
        string name,
        string description,
        ProfileUniverse universe,
        IReadOnlyList<ProfileIndicatorSpec> indicators,
        string ruleJson,
        IReadOnlyList<ScoreWeightSpec> weights)
    {
        if (db.ScanProfiles.Any(p => p.Code == code))
        {
            return;
        }

        db.Add(new ScanProfile
        {
            Code = code,
            Name = name,
            Description = description,
            UniverseJson = ProfileJson.Serialize(universe),
            IndicatorsJson = ProfileJson.Serialize(indicators),
            RuleJson = ruleJson,
            ScoreWeightsJson = ProfileJson.Serialize(weights)
        });
    }

    private static ProfileIndicatorSpec Spec(string code) => new() { Code = code };

    private static ScoreWeightSpec Weight(
        string label,
        string indicatorCode,
        string field,
        string op,
        object value,
        decimal weight)
        => new()
        {
            Label = label,
            IndicatorCode = indicatorCode,
            Field = field,
            Operator = op,
            Value = JsonSerializer.SerializeToElement(value),
            Weight = weight
        };

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
