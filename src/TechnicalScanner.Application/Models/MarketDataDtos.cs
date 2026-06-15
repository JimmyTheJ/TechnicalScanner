using TechnicalScanner.Domain.Enums;

namespace TechnicalScanner.Application.Models;

public sealed record TickerDto(
    string Symbol,
    string Exchange,
    string Country,
    string? Currency,
    string? CompanyName,
    string? Sector,
    string? Industry,
    decimal? MarketCap,
    bool IsEtf);

public sealed record PriceCandleDto(
    DateOnly CandleDate,
    decimal? Open,
    decimal? High,
    decimal? Low,
    decimal Close,
    decimal? AdjustedClose,
    long? Volume,
    string Source);
