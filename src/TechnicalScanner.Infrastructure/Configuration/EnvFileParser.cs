using Microsoft.Extensions.Configuration;

namespace TechnicalScanner.Infrastructure.Configuration;

public static class EnvFileParser
{
    public static Dictionary<string, string?> Parse(string contents)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(contents);
        while (reader.ReadLine() is { } rawLine)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].TrimStart();
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = NormalizeKey(line[..separator].Trim());
            if (key.Length == 0)
            {
                continue;
            }

            data[key] = Unquote(line[(separator + 1)..].Trim());
        }

        return data;
    }

    private static string NormalizeKey(string key)
        => key.Replace("__", ConfigurationPath.KeyDelimiter);

    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            var startsWithDouble = value.StartsWith('"') && value.EndsWith('"');
            var startsWithSingle = value.StartsWith('\'') && value.EndsWith('\'');
            if (startsWithDouble || startsWithSingle)
            {
                return value[1..^1];
            }
        }

        return value;
    }
}
