using System.Text.Json;

namespace TechnicalScanner.Application.Signals;

public sealed class SignalRuleEvaluator
{
    public bool Evaluate(string ruleJson, IReadOnlyDictionary<string, Dictionary<string, JsonElement>> indicatorValues)
    {
        using var document = JsonDocument.Parse(ruleJson);
        return EvaluateNode(document.RootElement, indicatorValues);
    }

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

    private static bool EvaluateNode(
        JsonElement node,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>> indicatorValues)
    {
        if (node.TryGetProperty("all", out var all))
        {
            return all.EnumerateArray().All(child => EvaluateNode(child, indicatorValues));
        }

        if (node.TryGetProperty("any", out var any))
        {
            return any.EnumerateArray().Any(child => EvaluateNode(child, indicatorValues));
        }

        if (node.TryGetProperty("not", out var not))
        {
            return !EvaluateNode(not, indicatorValues);
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

        var expected = node.GetProperty("value");
        return Compare(actual, op, expected);
    }

    private static bool Compare(JsonElement actual, string op, JsonElement expected) => op switch
    {
        "=" => JsonEquals(actual, expected),
        "!=" => !JsonEquals(actual, expected),
        "<" => AsDecimal(actual) < AsDecimal(expected),
        "<=" => AsDecimal(actual) <= AsDecimal(expected),
        ">" => AsDecimal(actual) > AsDecimal(expected),
        ">=" => AsDecimal(actual) >= AsDecimal(expected),
        _ => throw new NotSupportedException($"Operator '{op}' is not supported in phase 1.")
    };

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
