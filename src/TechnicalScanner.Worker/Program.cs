using TechnicalScanner.Application.Services;
using TechnicalScanner.Domain.Enums;
using TechnicalScanner.Infrastructure;
using TechnicalScanner.Infrastructure.Configuration;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddLocalAppSettings();
builder.Services.AddInfrastructure(builder.Configuration);
using var host = builder.Build();

if (args.Length == 0)
{
    PrintUsage();
    return;
}

await host.Services.InitializeDatabaseAsync();

var command = args[0].ToLowerInvariant();
using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;

switch (command)
{
    case "import-tickers":
        await services.GetRequiredService<TickerImportService>().ImportAsync(CancellationToken.None);
        break;
    case "update-prices":
    {
        var years = ParseIntOption(args, "--years", 10);
        var incremental = !args.Contains("--full");
        await services.GetRequiredService<PriceImportService>()
            .UpdatePricesAsync(years, incremental, CancellationToken.None);
        break;
    }
    case "aggregate-candles":
    {
        var timeframes = ParseTimeframes(args);
        await services.GetRequiredService<CandleAggregationService>().AggregateAsync(timeframes, CancellationToken.None);
        break;
    }
    case "calculate-indicators":
        await services.GetRequiredService<IndicatorCalculationService>().CalculateAsync(CancellationToken.None);
        break;
    case "evaluate-signals":
        await services.GetRequiredService<SignalEvaluationService>().EvaluateAsync(null, CancellationToken.None);
        break;
    case "calculate-scores":
        await services.GetRequiredService<CompositeScoringService>().CalculateAsync(null, CancellationToken.None);
        break;
    case "calculate-breadth":
        await services.GetRequiredService<BreadthCalculationService>().CalculateAsync(null, CancellationToken.None);
        break;
    case "export-csv":
    {
        var outputPath = args.Length > 1 && !args[1].StartsWith('-')
            ? args[1]
            : "./exports/generic_indicator_latest.csv";
        var path = await services.GetRequiredService<CsvExportService>()
            .ExportLatestIndicatorsAsync(outputPath, CancellationToken.None);
        Console.WriteLine($"Exported CSV to {path}");
        break;
    }
    case "run-nightly-scan":
    {
        var years = ParseIntOption(args, "--years", 10);
        var incremental = !args.Contains("--full");
        var exportPath = ParseStringOption(args, "--export", "./exports/generic_indicator_latest.csv");
        var sendAlerts = !args.Contains("--no-alerts");
        var scan = await services.GetRequiredService<NightlyScanService>()
            .RunAsync(incremental, years, exportPath, sendAlerts, CancellationToken.None);
        Console.WriteLine($"Nightly scan {scan.Id} completed with status {scan.Status}.");
        break;
    }
    case "send-alerts":
    {
        var scanRunId = ParseLongOption(args, "--scan-run", null);
        if (scanRunId is null)
        {
            Console.WriteLine("--scan-run is required for send-alerts.");
            Environment.ExitCode = 1;
            break;
        }

        var sent = await services.GetRequiredService<AlertService>()
            .ProcessTransitionsAsync(scanRunId.Value, CancellationToken.None);
        Console.WriteLine($"Processed {sent} alert transitions for scan {scanRunId}.");
        break;
    }
    case "test-alerts":
    {
        await services.GetRequiredService<AlertService>()
            .SendTestNotificationAsync(CancellationToken.None);
        Console.WriteLine("Test alert dispatched to configured channels.");
        break;
    }
    default:
        Console.WriteLine($"Unknown command: {command}");
        PrintUsage();
        Environment.ExitCode = 1;
        break;
}

static void PrintUsage()
{
    Console.WriteLine("""
        Technical Scanner Worker

        Commands:
          import-tickers
          update-prices --years 10 [--full]
          aggregate-candles --timeframes W,M
          calculate-indicators
          evaluate-signals
          calculate-scores
          calculate-breadth
          export-csv ./exports/generic_indicator_latest.csv
          send-alerts --scan-run 1
          test-alerts
          run-nightly-scan --years 10 [--full] [--export ./exports/generic_indicator_latest.csv] [--no-alerts]
        """);
}

static int ParseIntOption(string[] args, string optionName, int defaultValue)
{
    var index = Array.IndexOf(args, optionName);
    if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var value))
    {
        return value;
    }

    return defaultValue;
}

static string? ParseStringOption(string[] args, string optionName, string? defaultValue)
{
    var index = Array.IndexOf(args, optionName);
    if (index >= 0 && index + 1 < args.Length)
    {
        return args[index + 1];
    }

    return defaultValue;
}

static long? ParseLongOption(string[] args, string optionName, long? defaultValue)
{
    var index = Array.IndexOf(args, optionName);
    if (index >= 0 && index + 1 < args.Length && long.TryParse(args[index + 1], out var value))
    {
        return value;
    }

    return defaultValue;
}

static IEnumerable<Timeframe> ParseTimeframes(string[] args)
{
    var index = Array.IndexOf(args, "--timeframes");
    if (index < 0 || index + 1 >= args.Length)
    {
        return [Timeframe.W, Timeframe.M];
    }

    return args[index + 1]
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(TimeframeExtensions.FromCode);
}
