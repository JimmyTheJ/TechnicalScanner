using TechnicalScanner.Application.Signals;

namespace TechnicalScanner.Tests.Signals;

public class SignalRuleEvaluatorTests
{
    [Fact]
    public void Evaluate_SimpleComparisonRule_ReturnsTrue()
    {
        var evaluator = new SignalRuleEvaluator();
        var values = new Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>>
        {
            ["RSI_14_D"] = new()
            {
                ["category"] = System.Text.Json.JsonSerializer.SerializeToElement("Oversold")
            }
        };

        var rule = """
            {
              "indicatorCode": "RSI_14_D",
              "field": "category",
              "operator": "=",
              "value": "Oversold"
            }
            """;

        Assert.True(evaluator.Evaluate(rule, values));
    }

    [Fact]
    public void Evaluate_CompositeAllRule_RequiresAllConditions()
    {
        var evaluator = new SignalRuleEvaluator();
        var values = new Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>>
        {
            ["BMSB_W"] = new()
            {
                ["status"] = System.Text.Json.JsonSerializer.SerializeToElement("BelowBand")
            },
            ["RSI_14_W"] = new()
            {
                ["rsi"] = System.Text.Json.JsonSerializer.SerializeToElement(35)
            }
        };

        var rule = """
            {
              "all": [
                {
                  "indicatorCode": "BMSB_W",
                  "field": "status",
                  "operator": "=",
                  "value": "BelowBand"
                },
                {
                  "indicatorCode": "RSI_14_W",
                  "field": "rsi",
                  "operator": "<",
                  "value": 40
                }
              ]
            }
            """;

        Assert.True(evaluator.Evaluate(rule, values));
    }

    [Fact]
    public void Evaluate_NumericStringThreshold_ComparesAsDecimal()
    {
        var evaluator = new SignalRuleEvaluator();
        var values = new Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>>
        {
            ["SMA_200_D"] = new()
            {
                ["distanceFromSmaPct"] = System.Text.Json.JsonSerializer.SerializeToElement(-5m)
            }
        };

        var rule = """
            {
              "indicatorCode": "SMA_200_D",
              "field": "distanceFromSmaPct",
              "operator": "<",
              "value": "0"
            }
            """;

        Assert.True(evaluator.Evaluate(rule, values));
    }

    [Fact]
    public void CollectIndicatorCodes_WalksAllAnyAndNot()
    {
        var rule = """
            {
              "all": [
                { "indicatorCode": "SMA_200_D", "field": "distanceFromSmaPct", "operator": "<", "value": 0 },
                {
                  "any": [
                    { "indicatorCode": "RSI_14_D", "field": "rsi", "operator": "<", "value": 30 },
                    { "not": { "indicatorCode": "BMSB_W", "field": "status", "operator": "=", "value": "AboveBand" } }
                  ]
                }
              ]
            }
            """;

        var codes = SignalRuleEvaluator.CollectIndicatorCodes(rule);

        Assert.Equal(["BMSB_W", "RSI_14_D", "SMA_200_D"], codes);
    }

    [Fact]
    public void Evaluate_CrossedAbove_RequiresPreviousBelowThreshold()
    {
        var evaluator = new SignalRuleEvaluator();
        var rule = """
            {
              "indicatorCode": "RSI_14_D",
              "field": "rsi",
              "operator": "crossed_above",
              "value": 30
            }
            """;
        var current = Values(32);
        var previous = Values(28);

        Assert.True(evaluator.Evaluate(rule, current, previous));
        Assert.False(evaluator.Evaluate(rule, current));
        Assert.False(evaluator.Evaluate(rule, Values(28), Values(25)));
        Assert.True(SignalRuleEvaluator.RequiresPrevious(rule));
    }

    [Fact]
    public void Evaluate_Between_AcceptsArrayObjectAndCsv()
    {
        var evaluator = new SignalRuleEvaluator();
        var values = Values(25);

        Assert.True(evaluator.Evaluate(
            """{"indicatorCode":"RSI_14_D","field":"rsi","operator":"between","value":[20,30]}""",
            values));
        Assert.True(evaluator.Evaluate(
            """{"indicatorCode":"RSI_14_D","field":"rsi","operator":"between","value":{"min":20,"max":30}}""",
            values));
        Assert.True(evaluator.Evaluate(
            """{"indicatorCode":"RSI_14_D","field":"rsi","operator":"between","value":"20,30"}""",
            values));
        Assert.False(evaluator.Evaluate(
            """{"indicatorCode":"RSI_14_D","field":"rsi","operator":"not_between","value":[20,30]}""",
            values));
    }

    [Fact]
    public void Evaluate_RisingAndFalling_CompareAgainstPreviousBar()
    {
        var evaluator = new SignalRuleEvaluator();
        var rising = """{"indicatorCode":"RSI_14_D","field":"rsi","operator":"rising"}""";
        var falling = """{"indicatorCode":"RSI_14_D","field":"rsi","operator":"falling"}""";

        Assert.True(evaluator.Evaluate(rising, Values(40), Values(30)));
        Assert.False(evaluator.Evaluate(rising, Values(30), Values(40)));
        Assert.True(evaluator.Evaluate(falling, Values(30), Values(40)));
        Assert.False(evaluator.Evaluate(falling, Values(40)));
    }

    [Fact]
    public void Evaluate_InAndNotIn_MatchAnyListedValue()
    {
        var evaluator = new SignalRuleEvaluator();
        var values = new Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>>
        {
            ["RSI_14_D"] = new()
            {
                ["category"] = System.Text.Json.JsonSerializer.SerializeToElement("Oversold")
            }
        };

        Assert.True(evaluator.Evaluate(
            """{"indicatorCode":"RSI_14_D","field":"category","operator":"in","value":["Oversold","Weak"]}""",
            values));
        Assert.True(evaluator.Evaluate(
            """{"indicatorCode":"RSI_14_D","field":"category","operator":"in","value":"Oversold,Weak"}""",
            values));
        Assert.False(evaluator.Evaluate(
            """{"indicatorCode":"RSI_14_D","field":"category","operator":"in","value":["Neutral"]}""",
            values));
        Assert.True(evaluator.Evaluate(
            """{"indicatorCode":"RSI_14_D","field":"category","operator":"not_in","value":["Strong","Overbought"]}""",
            values));
    }

    private static Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>> Values(decimal rsi)
        => new()
        {
            ["RSI_14_D"] = new()
            {
                ["rsi"] = System.Text.Json.JsonSerializer.SerializeToElement(rsi)
            }
        };
}
