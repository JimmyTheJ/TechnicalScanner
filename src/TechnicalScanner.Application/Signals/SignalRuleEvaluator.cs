using System.Text.Json;

namespace TechnicalScanner.Application.Signals;

public sealed class SignalRuleEvaluator
{
    public bool Evaluate(
        string ruleJson,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>> indicatorValues,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>>? previousValues = null)
    {
        using var document = JsonDocument.Parse(ruleJson);
        return EvaluateNode(document.RootElement, indicatorValues, previousValues);
    }

    public static bool RequiresPrevious(string ruleJson)
    {
        if (string.IsNullOrWhiteSpace(ruleJson))
        {
            return false;
        }

        using var document = JsonDocument.Parse(ruleJson);
        return RequiresPrevious(document.RootElement);
    }

    public static bool IsTemporalOperator(string op)
        => op is "crossed_above" or "crossed_below" or "rising" or "falling";

    public static IReadOnlyList<string> CollectIndicatorCodes(string ruleJson)
    {
        if (string.IsNullOrWhiteSpace(ruleJson))
        {
            return [];
        }

        using var document = JsonDocument.Parse(ruleJson);
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectIndicatorCodes(document.RootElement, codes);
        return codes.OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void CollectIndicatorCodes(JsonElement node, ISet<string> codes)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (node.TryGetProperty("all", out var all))
        {
            foreach (var child in all.EnumerateArray())
            {
                CollectIndicatorCodes(child, codes);
            }

            return;
        }

        if (node.TryGetProperty("any", out var any))
        {
            foreach (var child in any.EnumerateArray())
            {
                CollectIndicatorCodes(child, codes);
            }

            return;
        }

        if (node.TryGetProperty("not", out var not))
        {
            CollectIndicatorCodes(not, codes);
            return;
        }

        if (node.TryGetProperty("indicatorCode", out var code) &&
            code.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(code.GetString()))
        {
            codes.Add(code.GetString()!);
        }
    }

    private static bool RequiresPrevious(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (node.TryGetProperty("all", out var all))
        {
            return all.EnumerateArray().Any(RequiresPrevious);
        }

        if (node.TryGetProperty("any", out var any))
        {
            return any.EnumerateArray().Any(RequiresPrevious);
        }

        if (node.TryGetProperty("not", out var not))
        {
            return RequiresPrevious(not);
        }

        var op = node.TryGetProperty("operator", out var opElement) ? opElement.GetString() : null;
        return IsTemporalOperator(op ?? string.Empty);
    }

    private static bool EvaluateNode(
        JsonElement node,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>> indicatorValues,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>>? previousValues)
    {
        if (node.TryGetProperty("all", out var all))
        {
            return all.EnumerateArray().All(child => EvaluateNode(child, indicatorValues, previousValues));
        }

        if (node.TryGetProperty("any", out var any))
        {
            return any.EnumerateArray().Any(child => EvaluateNode(child, indicatorValues, previousValues));
        }

        if (node.TryGetProperty("not", out var not))
        {
            return !EvaluateNode(not, indicatorValues, previousValues);
        }

        var indicatorCode = node.GetProperty("indicatorCode").GetString()
            ?? throw new InvalidOperationException("Rule is missing indicatorCode.");
        var field = node.GetProperty("field").GetString()
            ?? throw new InvalidOperationException("Rule is missing field.");
        var op = node.GetProperty("operator").GetString()
            ?? throw new InvalidOperationException("Rule is missing operator.");

        if (!indicatorValues.TryGetValue(indicatorCode, out var values) ||
            !values.TryGetValue(field, out var actual))
        {
            return false;
        }

        JsonElement? previous = null;
        if (previousValues is not null &&
            previousValues.TryGetValue(indicatorCode, out var previousFields) &&
            previousFields.TryGetValue(field, out var previousActual))
        {
            previous = previousActual;
        }

        var expected = node.TryGetProperty("value", out var valueElement) ? valueElement : default;
        return Compare(actual, op, expected, previous);
    }

    private static bool Compare(
        JsonElement actual,
        string op,
        JsonElement expected,
        JsonElement? previous) => op switch
    {
        "=" => JsonEquals(actual, expected),
        "!=" => !JsonEquals(actual, expected),
        "<" => AsDecimal(actual) < AsDecimal(expected),
        "<=" => AsDecimal(actual) <= AsDecimal(expected),
        ">" => AsDecimal(actual) > AsDecimal(expected),
        ">=" => AsDecimal(actual) >= AsDecimal(expected),
        "between" => InRange(actual, expected),
        "not_between" => !InRange(actual, expected),
        "in" => InList(actual, expected),
        "not_in" => !InList(actual, expected),
        "crossed_above" => previous is { } prior &&
            AsDecimal(prior) < AsDecimal(expected) &&
            AsDecimal(actual) >= AsDecimal(expected),
        "crossed_below" => previous is { } prior &&
            AsDecimal(prior) > AsDecimal(expected) &&
            AsDecimal(actual) <= AsDecimal(expected),
        "rising" => previous is { } prior && AsDecimal(actual) > AsDecimal(prior),
        "falling" => previous is { } prior && AsDecimal(actual) < AsDecimal(prior),
        _ => throw new NotSupportedException($"Operator '{op}' is not supported.")
    };

    private static bool InRange(JsonElement actual, JsonElement expected)
    {
        var (min, max) = ReadRange(expected);
        var value = AsDecimal(actual);
        return value >= min && value <= max;
    }

    private static (decimal Min, decimal Max) ReadRange(JsonElement expected)
    {
        if (expected.ValueKind == JsonValueKind.Array)
        {
            var values = expected.EnumerateArray().ToList();
            if (values.Count >= 2)
            {
                return (AsDecimal(values[0]), AsDecimal(values[1]));
            }
        }

        if (expected.ValueKind == JsonValueKind.Object &&
            expected.TryGetProperty("min", out var minElement) &&
            expected.TryGetProperty("max", out var maxElement))
        {
            return (AsDecimal(minElement), AsDecimal(maxElement));
        }

        if (expected.ValueKind == JsonValueKind.String)
        {
            var parts = expected.GetString()?.Split(',', StringSplitOptions.TrimEntries) ?? [];
            if (parts.Length >= 2 &&
                decimal.TryParse(parts[0], out var min) &&
                decimal.TryParse(parts[1], out var max))
            {
                return (min, max);
            }
        }

        throw new InvalidOperationException("between/not_between requires a [min, max] value.");
    }

    private static bool InList(JsonElement actual, JsonElement expected)
    {
        var needle = actual.ToString();
        return ReadList(expected).Any(item => string.Equals(item, needle, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ReadList(JsonElement expected)
    {
        if (expected.ValueKind == JsonValueKind.Array)
        {
            return expected.EnumerateArray()
                .Select(item => item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }

        if (expected.ValueKind == JsonValueKind.String)
        {
            return (expected.GetString() ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        var single = expected.ToString();
        return string.IsNullOrWhiteSpace(single) ? [] : [single];
    }

    private static decimal AsDecimal(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetDecimal(),
        JsonValueKind.String when decimal.TryParse(element.GetString(), out var parsed) => parsed,
        JsonValueKind.True => 1,
        JsonValueKind.False => 0,
        _ => throw new InvalidOperationException($"Cannot compare non-numeric value '{element}'.")
    };

    private static bool JsonEquals(JsonElement actual, JsonElement expected)
    {
        if (TryGetBoolean(actual, out var actualBool) && TryGetBoolean(expected, out var expectedBool))
        {
            return actualBool == expectedBool;
        }

        if (actual.ValueKind == JsonValueKind.Number || expected.ValueKind == JsonValueKind.Number)
        {
            try
            {
                return AsDecimal(actual) == AsDecimal(expected);
            }
            catch (InvalidOperationException)
            {
                // fall through to string compare
            }
        }

        return string.Equals(actual.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetBoolean(JsonElement element, out bool value)
    {
        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = element.GetBoolean();
            return true;
        }

        if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out value))
        {
            return true;
        }

        value = false;
        return false;
    }
}
