namespace TechnicalScanner.Application.Indicators;

public static class IndicatorMath
{
    public static decimal? SimpleMovingAverage(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count < period || period <= 0)
        {
            return null;
        }

        var slice = values.Skip(values.Count - period).Take(period).ToList();
        return slice.Average();
    }

    public static decimal? ExponentialMovingAverage(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count < period || period <= 0)
        {
            return null;
        }

        var multiplier = 2m / (period + 1);
        decimal? ema = values.Take(period).Average();

        for (var i = period; i < values.Count; i++)
        {
            ema = (values[i] - ema) * multiplier + ema;
        }

        return ema;
    }

    public static decimal? RelativeStrengthIndex(IReadOnlyList<decimal> closes, int period)
    {
        if (closes.Count <= period || period <= 0)
        {
            return null;
        }

        decimal gainSum = 0;
        decimal lossSum = 0;

        for (var i = 1; i <= period; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change >= 0)
            {
                gainSum += change;
            }
            else
            {
                lossSum += Math.Abs(change);
            }
        }

        var avgGain = gainSum / period;
        var avgLoss = lossSum / period;

        for (var i = period + 1; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            var gain = change > 0 ? change : 0;
            var loss = change < 0 ? Math.Abs(change) : 0;
            avgGain = ((avgGain * (period - 1)) + gain) / period;
            avgLoss = ((avgLoss * (period - 1)) + loss) / period;
        }

        if (avgLoss == 0)
        {
            return 100;
        }

        var rs = avgGain / avgLoss;
        return 100 - (100 / (1 + rs));
    }

    public static (decimal? Middle, decimal? Upper, decimal? Lower, decimal? StdDev) BollingerBands(
        IReadOnlyList<decimal> values,
        int period,
        decimal stdDevMultiplier)
    {
        if (values.Count < period || period <= 0)
        {
            return (null, null, null, null);
        }

        var slice = values.Skip(values.Count - period).Take(period).ToList();
        var middle = slice.Average();
        var variance = slice.Select(v => (v - middle) * (v - middle)).Average();
        var stdDev = (decimal)Math.Sqrt((double)variance);
        var upper = middle + (stdDevMultiplier * stdDev);
        var lower = middle - (stdDevMultiplier * stdDev);
        return (middle, upper, lower, stdDev);
    }

    public static decimal? DistancePercent(decimal price, decimal reference)
    {
        if (reference == 0)
        {
            return null;
        }

        return ((price - reference) / reference) * 100m;
    }

    public static string ClassifyRsi(decimal rsi) => rsi switch
    {
        < 30 => "Oversold",
        < 40 => "Weak",
        < 60 => "Neutral",
        < 70 => "Strong",
        < 80 => "Overbought",
        _ => "ExtremelyOverbought"
    };
}
