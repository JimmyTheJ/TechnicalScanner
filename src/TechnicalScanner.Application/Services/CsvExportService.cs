using System.Globalization;
using System.Text;
using System.Text.Json;
using TechnicalScanner.Application.Abstractions;

namespace TechnicalScanner.Application.Services;

public sealed class CsvExportService(ITechnicalScannerDbContext dbContext)
{
    public async Task<string> ExportLatestIndicatorsAsync(string outputPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var latest = dbContext.IndicatorValues
            .Join(dbContext.Tickers, v => v.TickerId, t => t.Id, (v, t) => new { v, t })
            .Join(dbContext.IndicatorDefinitions, x => x.v.IndicatorDefinitionId, d => d.Id, (x, d) => new { x.v, x.t, d })
            .AsEnumerable()
            .GroupBy(x => new { x.t.Symbol, x.d.Code })
            .Select(g => g.OrderByDescending(x => x.v.CandleDate).First())
            .OrderBy(x => x.t.Symbol)
            .ThenBy(x => x.d.Code)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Symbol,Exchange,Country,IndicatorCode,CandleDate,PrimaryValue,SecondaryValue,TertiaryValue,ValuesJson");

        foreach (var row in latest)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sb.AppendLine(string.Join(',',
                Csv(row.t.Symbol),
                Csv(row.t.Exchange),
                Csv(row.t.Country),
                Csv(row.d.Code),
                row.v.CandleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.v.PrimaryValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.v.SecondaryValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.v.TertiaryValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Csv(row.v.ValuesJson)));
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), cancellationToken);
        return outputPath;
    }

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
