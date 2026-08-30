using System.Text.Json;
using TechnicalScanner.Application.Models.Profiles;
using TechnicalScanner.Application.Signals;

namespace TechnicalScanner.Application.Profiles;

public static class IndicatorValueMaps
{
    public static Dictionary<string, Dictionary<string, JsonElement>> ToEvaluatorMap(
        IReadOnlyDictionary<string, JsonElement> valuesByCode)
    {
        var result = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, element) in valuesByCode)
        {
            result[code] = ToFields(element);
        }

        return result;
    }

    public static Dictionary<string, JsonElement> ToFields(JsonElement element)
    {
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        foreach (var property in element.EnumerateObject())
        {
            fields[property.Name] = property.Value;
        }

        return fields;
    }

    public static Dictionary<string, JsonElement> ParseValuesJson(string valuesJson)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(valuesJson)
           ?? new Dictionary<string, JsonElement>();
}

public static class ProfileScoring
{
    public static (decimal Score, Dictionary<string, decimal> Components) Score(
        IReadOnlyList<ScoreWeightSpec> weights,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>> valuesByCode,
        SignalRuleEvaluator evaluator,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>>? previousValues = null)
    {
        var components = new Dictionary<string, decimal>();
        var score = 0m;

        foreach (var weight in weights)
        {
            if (!Matches(weight, valuesByCode, previousValues, evaluator))
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(weight.Label) ? weight.IndicatorCode : weight.Label;
            components[label] = weight.Weight;
            score += weight.Weight;
        }

        return (score, components);
    }

    public static IReadOnlyList<ProfileScoreComponentDto> Explain(
        IReadOnlyList<ScoreWeightSpec> weights,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>> valuesByCode,
        SignalRuleEvaluator evaluator,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>>? previousValues = null)
    {
        return weights.Select(weight =>
        {
            var matched = Matches(weight, valuesByCode, previousValues, evaluator);
            return new ProfileScoreComponentDto
            {
                Label = string.IsNullOrWhiteSpace(weight.Label) ? weight.IndicatorCode : weight.Label,
                IndicatorCode = weight.IndicatorCode,
                Field = weight.Field,
                Operator = weight.Operator,
                Matched = matched,
                Weight = weight.Weight
            };
        }).ToList();
    }

    private static bool Matches(
        ScoreWeightSpec weight,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>> valuesByCode,
        IReadOnlyDictionary<string, Dictionary<string, JsonElement>>? previousValues,
        SignalRuleEvaluator evaluator)
    {
        var rule = ProfileJson.Serialize(new ComparisonCondition
        {
            IndicatorCode = weight.IndicatorCode,
            Field = weight.Field,
            Operator = weight.Operator,
            Value = weight.Value
        });
        return evaluator.Evaluate(rule, valuesByCode, previousValues);
    }
}
