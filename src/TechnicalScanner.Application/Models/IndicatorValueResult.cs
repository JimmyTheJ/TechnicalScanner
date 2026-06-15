using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Models;

public sealed record IndicatorValueResult(
    DateOnly CandleDate,
    string Timeframe,
    decimal? PrimaryValue,
    decimal? SecondaryValue,
    decimal? TertiaryValue,
    IReadOnlyDictionary<string, object?> Values);
