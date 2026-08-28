using TechnicalScanner.Application.Signals;
using TechnicalScanner.Domain.Enums;

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
}
