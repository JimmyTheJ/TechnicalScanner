using TechnicalScanner.Application.Abstractions;

namespace TechnicalScanner.Application.Indicators;

public sealed class IndicatorCalculatorRegistry
{
    private readonly IReadOnlyDictionary<string, IIndicatorCalculator> _calculators;

    public IndicatorCalculatorRegistry(IEnumerable<IIndicatorCalculator> calculators)
    {
        _calculators = calculators.ToDictionary(c => c.IndicatorType, StringComparer.OrdinalIgnoreCase);
    }

    public IIndicatorCalculator GetRequired(string indicatorType)
    {
        if (!_calculators.TryGetValue(indicatorType, out var calculator))
        {
            throw new InvalidOperationException($"No calculator registered for indicator type '{indicatorType}'.");
        }

        return calculator;
    }
}
