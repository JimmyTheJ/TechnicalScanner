using Microsoft.Extensions.DependencyInjection;
using TechnicalScanner.Application.Abstractions;
using TechnicalScanner.Application.Indicators;
using TechnicalScanner.Application.Indicators.Calculators;
using TechnicalScanner.Application.Services;
using TechnicalScanner.Application.Services.Queries;
using TechnicalScanner.Application.Signals;

namespace TechnicalScanner.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IIndicatorCalculator, BmsbIndicatorCalculator>();
        services.AddSingleton<IIndicatorCalculator, RsiIndicatorCalculator>();
        services.AddSingleton<IIndicatorCalculator, SmaIndicatorCalculator>();
        services.AddSingleton<IIndicatorCalculator, EmaIndicatorCalculator>();
        services.AddSingleton<IIndicatorCalculator, BollingerBandsIndicatorCalculator>();
        services.AddSingleton<IndicatorCalculatorRegistry>();
        services.AddScoped<SignalRuleEvaluator>();
        services.AddScoped<ITickerOperationLogger, TickerOperationLogger>();
        services.AddScoped<TickerImportService>();
        services.AddScoped<PriceImportService>();
        services.AddScoped<CandleAggregationService>();
        services.AddScoped<IndicatorCalculationService>();
        services.AddScoped<SignalEvaluationService>();
        services.AddScoped<CompositeScoringService>();
        services.AddScoped<BreadthCalculationService>();
        services.AddScoped<CsvExportService>();
        services.AddScoped<NightlyScanService>();
        services.AddScoped<TickerQueryService>();
        services.AddScoped<IndicatorQueryService>();
        services.AddScoped<SignalQueryService>();
        services.AddScoped<ScreenerQueryService>();
        services.AddScoped<BreadthQueryService>();
        services.AddScoped<ScannerQueryService>();
        return services;
    }
}
