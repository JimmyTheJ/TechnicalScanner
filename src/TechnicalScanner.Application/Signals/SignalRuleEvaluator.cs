using System.Text.Json;
using TechnicalScanner.Domain.Entities;

namespace TechnicalScanner.Application.Signals;

public sealed class SignalRuleEvaluator
{
    public bool Evaluate(string ruleJson, IReadOnlyDictionary<string, Dictionary<string, JsonElement>> indicatorValues)
    {
        using var document = JsonDocument.Parse(ruleJson);
        return EvaluateNode(document.RootElement, indicatorValues);
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
        "<" => actual.GetDecimal() < expected.GetDecimal(),
        "<=" => actual.GetDecimal() <= expected.GetDecimal(),
        ">" => actual.GetDecimal() > expected.GetDecimal(),
        ">=" => actual.GetDecimal() >= expected.GetDecimal(),
        _ => throw new NotSupportedException($"Operator '{op}' is not supported in phase 1.")
    };

    private static bool JsonEquals(JsonElement actual, JsonElement expected)
    {
        if (actual.ValueKind != expected.ValueKind)
        {
            if (actual.ValueKind == JsonValueKind.True || actual.ValueKind == JsonValueKind.False)
            {
                return actual.GetBoolean() == (expected.ValueKind == JsonValueKind.True);
            }

            if (actual.ValueKind == JsonValueKind.Number && expected.ValueKind == JsonValueKind.Number)
            {
                return actual.GetDecimal() == expected.GetDecimal();
            }

            return actual.ToString() == expected.ToString();
        }

        return actual.ToString() == expected.ToString();
    }
}
