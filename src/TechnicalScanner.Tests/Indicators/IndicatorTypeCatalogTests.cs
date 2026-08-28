using System.Text.Json;
using TechnicalScanner.Application.Indicators;

namespace TechnicalScanner.Tests.Indicators;

public class IndicatorTypeCatalogTests
{
    [Fact]
    public void SuggestCode_BuildsSmaAndVolumeCodes()
    {
        var smaParams = JsonSerializer.SerializeToElement(new { period = 20 });
        var volParams = JsonSerializer.SerializeToElement(new { period = 20 });

        Assert.Equal("SMA_20_D", IndicatorTypeCatalog.SuggestCode("SMA", "d", smaParams));
        Assert.Equal("VOL_SMA_20_D", IndicatorTypeCatalog.SuggestCode("VOLUME", "D", volParams));
        Assert.Contains(IndicatorTypeCatalog.Entries, e => e.IndicatorType == "ATR");
        Assert.Contains(IndicatorTypeCatalog.Entries, e => e.IndicatorType == "PRICE_RANGE");
    }
}
