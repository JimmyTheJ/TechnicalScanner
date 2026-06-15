using TechnicalScanner.Application.Models;
using TechnicalScanner.Domain.Enums;

namespace TechnicalScanner.Application.Abstractions;

public interface IMarketDataProvider
{
    Task<IReadOnlyList<TickerDto>> GetTickersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PriceCandleDto>> GetHistoricalCandlesAsync(
        string symbol,
        string exchange,
        string country,
        Timeframe timeframe,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken);
}
