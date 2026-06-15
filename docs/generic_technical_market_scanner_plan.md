# Generic Technical Indicator Market Scanner — Agentic AI Implementation Plan

## Goal

Build a generic stock-market technical analysis scanner that can evaluate many indicators across US and Canadian equities, then rank, filter, backtest, and summarize market breadth.

The initial system should support:

- Bull Market Support Band
- Daily / Weekly / Monthly RSI
- Bollinger Bands
- 50W / 100W / 200W SMA
- 200D SMA
- Other future indicators

The system should answer:

- Which stocks are technically cheap, oversold, weak, or below key support?
- Which stocks are technically expensive, overbought, strong, or extended?
- How many stocks are above/below major technical thresholds?
- Are broad markets washed out, neutral, strong, or euphoric?
- Which signals agree across multiple timeframes?

---

## Core Design Principle

Do **not** hard-code the system around one indicator.

Design it as a generic indicator engine:

```text
Ticker universe
    -> price data
        -> indicator calculations
            -> signal rules
                -> scan results
                    -> breadth summaries
                        -> dashboard / alerts / backtests
```

Indicators should be modular and composable.

Example:

```text
BMSB:
    Weekly Close vs 20W SMA and 21W EMA

RSI:
    Daily RSI(14)
    Weekly RSI(14)
    Monthly RSI(14)

Moving Average:
    Close vs 50W SMA
    Close vs 100W SMA
    Close vs 200W SMA
    Close vs 200D SMA

Bollinger Bands:
    Close vs 20-period SMA ± 2 standard deviations
```

---

## Initial Indicator Modules

## 1. Bull Market Support Band

Defined on weekly candles:

```text
SMA20W = 20-week simple moving average
EMA21W = 21-week exponential moving average

BandTop = max(SMA20W, EMA21W)
BandBottom = min(SMA20W, EMA21W)
BandMidpoint = (SMA20W + EMA21W) / 2
```

Classification:

```text
BelowBand:
    Close < BandBottom

InsideBand:
    BandBottom <= Close <= BandTop

AboveBand:
    Close > BandTop
```

Useful metrics:

```text
DistanceFromBandBottomPct
DistanceFromBandTopPct
DistanceFromBandMidpointPct
```

---

## 2. RSI

Support RSI across multiple timeframes:

```text
Daily RSI(14)
Weekly RSI(14)
Monthly RSI(14)
```

Default thresholds:

```text
RSI < 30: Oversold
RSI 30-40: Weak
RSI 40-60: Neutral
RSI 60-70: Strong
RSI > 70: Overbought
RSI > 80: Extremely Overbought
```

Store:

```text
Timeframe
Period
RSI value
RSI category
Distance from threshold
```

Potential signal rules:

```text
Daily RSI < 30
Weekly RSI < 40
Monthly RSI > 50
Daily RSI crossing above 30
Weekly RSI crossing above 50
RSI divergence, future enhancement
```

---

## 3. Simple Moving Averages

Support:

```text
Daily:
    20D SMA
    50D SMA
    100D SMA
    200D SMA

Weekly:
    20W SMA
    50W SMA
    100W SMA
    200W SMA

Monthly:
    10M SMA
    20M SMA
```

Useful classifications:

```text
CloseAboveSma
CloseBelowSma
DistanceFromSmaPct
SmaSlopePositive
SmaSlopeNegative
CrossedAboveSma
CrossedBelowSma
```

Key examples:

```text
Close > 200D SMA: long-term daily trend positive
Close < 200D SMA: long-term daily trend negative
Close > 50W SMA: intermediate weekly trend positive
Close > 200W SMA: long-term weekly trend positive
```

---

## 4. Exponential Moving Averages

Support:

```text
Daily:
    8D EMA
    21D EMA
    50D EMA
    200D EMA

Weekly:
    21W EMA
    50W EMA
```

Useful classifications:

```text
CloseAboveEma
CloseBelowEma
DistanceFromEmaPct
EmaSlopePositive
EmaSlopeNegative
CrossedAboveEma
CrossedBelowEma
```

---

## 5. Bollinger Bands

Default:

```text
Period = 20
Standard Deviations = 2
```

Support daily, weekly, and monthly Bollinger Bands.

For each timeframe:

```text
MiddleBand = SMA(period)
UpperBand = MiddleBand + StdDevMultiplier * StdDev(period)
LowerBand = MiddleBand - StdDevMultiplier * StdDev(period)
BandWidthPct = ((UpperBand - LowerBand) / MiddleBand) * 100
PercentB = (Close - LowerBand) / (UpperBand - LowerBand)
```

Classification:

```text
CloseBelowLowerBand
CloseNearLowerBand
CloseInsideBands
CloseNearUpperBand
CloseAboveUpperBand
Squeeze
Expansion
```

Useful rules:

```text
Close < LowerBand
Close > UpperBand
PercentB < 0
PercentB > 1
BandWidthPct near N-period low
```

---

## Generic Timeframes

The engine should support multiple timeframes:

```text
Daily
Weekly
Monthly
```

Potential future timeframes:

```text
Hourly
4H
Quarterly
```

Use a normalized enum:

```text
D
W
M
```

Each indicator calculation should specify:

```text
IndicatorType
Timeframe
Parameters
InputSeries
OutputValues
```

---

## Generic Indicator Model

Avoid one database table per indicator if possible.

Use generic indicator definitions and values.

### indicator_definition

```sql
CREATE TABLE indicator_definition (
    id BIGSERIAL PRIMARY KEY,
    code TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL,
    description TEXT NULL,
    indicator_type TEXT NOT NULL,
    timeframe TEXT NOT NULL,
    parameters JSONB NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Examples:

```json
{
  "code": "BMSB_W",
  "name": "Bull Market Support Band",
  "indicator_type": "BMSB",
  "timeframe": "W",
  "parameters": {
    "smaPeriod": 20,
    "emaPeriod": 21
  }
}
```

```json
{
  "code": "RSI_14_D",
  "name": "Daily RSI 14",
  "indicator_type": "RSI",
  "timeframe": "D",
  "parameters": {
    "period": 14
  }
}
```

```json
{
  "code": "BB_20_2_D",
  "name": "Daily Bollinger Bands 20/2",
  "indicator_type": "BOLLINGER_BANDS",
  "timeframe": "D",
  "parameters": {
    "period": 20,
    "stdDevMultiplier": 2
  }
}
```

---

## Generic Indicator Values

### indicator_value

Stores calculated indicator outputs.

```sql
CREATE TABLE indicator_value (
    id BIGSERIAL PRIMARY KEY,
    ticker_id BIGINT NOT NULL REFERENCES ticker(id),
    indicator_definition_id BIGINT NOT NULL REFERENCES indicator_definition(id),
    candle_date DATE NOT NULL,
    timeframe TEXT NOT NULL,
    primary_value NUMERIC NULL,
    secondary_value NUMERIC NULL,
    tertiary_value NUMERIC NULL,
    values JSONB NOT NULL,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(ticker_id, indicator_definition_id, candle_date)
);
```

Examples:

### BMSB values

```json
{
  "close": 100.25,
  "sma20w": 96.10,
  "ema21w": 97.35,
  "bandTop": 97.35,
  "bandBottom": 96.10,
  "bandMidpoint": 96.725,
  "distanceFromBandBottomPct": 4.32,
  "distanceFromBandTopPct": 2.98,
  "distanceFromBandMidpointPct": 3.64,
  "status": "AboveBand"
}
```

### RSI values

```json
{
  "close": 100.25,
  "rsi": 28.6,
  "period": 14,
  "category": "Oversold"
}
```

### Bollinger Band values

```json
{
  "close": 100.25,
  "middleBand": 95.00,
  "upperBand": 105.00,
  "lowerBand": 85.00,
  "bandWidthPct": 21.05,
  "percentB": 0.7625,
  "status": "InsideBands"
}
```

The `primary_value`, `secondary_value`, and `tertiary_value` fields should be used for fast sorting/filtering.

Suggested mapping:

```text
BMSB:
    primary_value = distanceFromBandMidpointPct
    secondary_value = bandTop
    tertiary_value = bandBottom

RSI:
    primary_value = rsi

Bollinger Bands:
    primary_value = percentB
    secondary_value = bandWidthPct

SMA:
    primary_value = distanceFromSmaPct
    secondary_value = sma

EMA:
    primary_value = distanceFromEmaPct
    secondary_value = ema
```

---

## Generic Signal Rules

Indicators are raw calculated values.

Signals are interpretations of those values.

Examples:

```text
BMSB_BELOW
BMSB_ABOVE
RSI_D_OVERSOLD
RSI_W_OVERSOLD
RSI_M_BULLISH
PRICE_ABOVE_200D_SMA
PRICE_BELOW_200W_SMA
BB_D_BELOW_LOWER
BB_D_ABOVE_UPPER
```

### signal_definition

```sql
CREATE TABLE signal_definition (
    id BIGSERIAL PRIMARY KEY,
    code TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL,
    description TEXT NULL,
    indicator_definition_id BIGINT NULL REFERENCES indicator_definition(id),
    rule_json JSONB NOT NULL,
    severity TEXT NULL,
    direction TEXT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Example rule:

```json
{
  "indicatorCode": "RSI_14_W",
  "field": "rsi",
  "operator": "<",
  "value": 30
}
```

Example composite rule:

```json
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
    },
    {
      "indicatorCode": "SMA_200_D",
      "field": "distanceFromSmaPct",
      "operator": "<",
      "value": 0
    }
  ]
}
```

Composite example:

```text
Potential Deep Value / Weak Technical Setup:
    Below weekly BMSB
    Weekly RSI < 40
    Price below 200D SMA
    Price below 50W SMA
```

Another:

```text
High Confidence Recovery Setup:
    Price reclaimed BMSB
    Daily RSI crossed above 50
    Price above 200D SMA
    Weekly RSI rising
```

---

## Signal Results

### signal_result

```sql
CREATE TABLE signal_result (
    id BIGSERIAL PRIMARY KEY,
    scan_date_utc TIMESTAMPTZ NOT NULL,
    ticker_id BIGINT NOT NULL REFERENCES ticker(id),
    signal_definition_id BIGINT NOT NULL REFERENCES signal_definition(id),
    is_match BOOLEAN NOT NULL,
    score NUMERIC NULL,
    values JSONB NULL,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(scan_date_utc, ticker_id, signal_definition_id)
);
```

---

## Scoring Model

Add an optional confidence or confluence score.

Example:

```text
Score starts at 0

+2 if below BMSB
+1 if weekly RSI < 40
+2 if daily RSI < 30
+1 if below 200D SMA
+1 if below 50W SMA
+1 if below lower daily Bollinger Band
-2 if monthly RSI is below 40 and falling
+2 if monthly RSI is above 50 and rising
```

Important:

- Scores should be transparent.
- Store component contributions.
- Do not hide how the score was calculated.

### scan_score

```sql
CREATE TABLE scan_score (
    id BIGSERIAL PRIMARY KEY,
    scan_date_utc TIMESTAMPTZ NOT NULL,
    ticker_id BIGINT NOT NULL REFERENCES ticker(id),
    score_code TEXT NOT NULL,
    score NUMERIC NOT NULL,
    components JSONB NOT NULL,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(scan_date_utc, ticker_id, score_code)
);
```

---

## Price Data Model

Use normalized candle data by timeframe.

### ticker

```sql
CREATE TABLE ticker (
    id BIGSERIAL PRIMARY KEY,
    symbol TEXT NOT NULL,
    exchange TEXT NOT NULL,
    country TEXT NOT NULL,
    currency TEXT NULL,
    company_name TEXT NULL,
    sector TEXT NULL,
    industry TEXT NULL,
    market_cap NUMERIC NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_etf BOOLEAN NOT NULL DEFAULT FALSE,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(symbol, exchange, country)
);
```

### price_candle

```sql
CREATE TABLE price_candle (
    id BIGSERIAL PRIMARY KEY,
    ticker_id BIGINT NOT NULL REFERENCES ticker(id),
    timeframe TEXT NOT NULL,
    candle_date DATE NOT NULL,
    open NUMERIC NULL,
    high NUMERIC NULL,
    low NUMERIC NULL,
    close NUMERIC NOT NULL,
    adjusted_close NUMERIC NULL,
    volume BIGINT NULL,
    source TEXT NOT NULL,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(ticker_id, timeframe, candle_date)
);
```

Recommended indexes:

```sql
CREATE INDEX ix_price_candle_ticker_timeframe_date
ON price_candle(ticker_id, timeframe, candle_date DESC);

CREATE INDEX ix_indicator_value_ticker_indicator_date
ON indicator_value(ticker_id, indicator_definition_id, candle_date DESC);

CREATE INDEX ix_indicator_value_indicator_date_primary
ON indicator_value(indicator_definition_id, candle_date DESC, primary_value);

CREATE INDEX ix_signal_result_scan_signal
ON signal_result(scan_date_utc, signal_definition_id, is_match);
```

---

## Indicator Calculation Interface

Use an interface like:

```csharp
public interface IIndicatorCalculator
{
    string IndicatorType { get; }

    Task<IReadOnlyList<IndicatorValueResult>> CalculateAsync(
        Ticker ticker,
        IndicatorDefinition definition,
        IReadOnlyList<PriceCandle> candles,
        CancellationToken cancellationToken);
}
```

Example implementations:

```text
BmsbIndicatorCalculator
RsiIndicatorCalculator
SmaIndicatorCalculator
EmaIndicatorCalculator
BollingerBandsIndicatorCalculator
```

The engine should select the calculator based on `indicator_type`.

---

## Indicator Registry

Create a registry of active indicator definitions.

Seed examples:

```text
BMSB_W
RSI_14_D
RSI_14_W
RSI_14_M
SMA_50_W
SMA_100_W
SMA_200_W
SMA_200_D
EMA_21_W
BB_20_2_D
BB_20_2_W
BB_20_2_M
```

Store definitions in DB so new indicators can be added without code changes when they reuse existing calculator types.

---

## Signal Rule Engine

Build a simple rule evaluator.

Support operators:

```text
=
!=
<
<=
>
>=
between
not_between
in
not_in
crossed_above
crossed_below
rising
falling
```

Support logical composition:

```text
all
any
not
```

Minimum viable rule engine can skip advanced temporal operators at first.

Phase 1 operators:

```text
=
<
>
<=
>=
all
any
```

Phase 2 operators:

```text
crossed_above
crossed_below
rising
falling
```

---

## Market Breadth

Breadth should be generic and not only BMSB-specific.

Examples:

```text
% of stocks above BMSB
% of stocks with weekly RSI > 50
% of stocks with daily RSI < 30
% of stocks above 200D SMA
% of stocks above 50W SMA
% of stocks above 200W SMA
% of stocks above upper daily Bollinger Band
% of stocks below lower daily Bollinger Band
```

### breadth_snapshot

```sql
CREATE TABLE breadth_snapshot (
    id BIGSERIAL PRIMARY KEY,
    scan_date_utc TIMESTAMPTZ NOT NULL,
    universe_name TEXT NOT NULL,
    country TEXT NULL,
    exchange TEXT NULL,
    sector TEXT NULL,
    industry TEXT NULL,
    metric_code TEXT NOT NULL,
    total_valid_stocks INTEGER NOT NULL,
    match_count INTEGER NOT NULL,
    match_pct NUMERIC NOT NULL,
    median_value NUMERIC NULL,
    average_value NUMERIC NULL,
    created_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Example metrics:

```text
BMSB_W_ABOVE_PCT
BMSB_W_BELOW_PCT
RSI_14_D_OVERSOLD_PCT
RSI_14_W_ABOVE_50_PCT
SMA_200_D_ABOVE_PCT
SMA_50_W_ABOVE_PCT
BB_20_2_D_BELOW_LOWER_PCT
```

---

## Dashboard Requirements

## 1. Market Overview

Show major breadth indicators:

```text
% Above BMSB
% Above 200D SMA
% Above 50W SMA
% Above 200W SMA
% Weekly RSI > 50
% Daily RSI < 30
% Below lower Bollinger Band
```

Charts:

```text
Breadth metric history
Sector heatmap
Exchange comparison
Country comparison
Market-cap bucket comparison
```

---

## 2. Generic Screener

Columns:

```text
Ticker
CompanyName
Exchange
Country
Sector
Industry
MarketCap
Close

BMSB Status
BMSB Distance %

Daily RSI
Weekly RSI
Monthly RSI

200D SMA Distance %
50W SMA Distance %
100W SMA Distance %
200W SMA Distance %

Daily Bollinger %B
Weekly Bollinger %B

Composite Score
Matched Signals
```

Filters:

```text
Country
Exchange
Sector
Industry
MarketCap bucket
Volume
Dollar volume
Indicator status
Signal match
RSI range
Distance from moving average
BMSB status
Bollinger %B range
Composite score range
```

---

## 3. Stock Detail Page

For each ticker, show:

```text
Price chart
Selected indicators
Signal history
Current scan status
Composite score breakdown
Distance from key moving averages
RSI by timeframe
Bollinger Band state
```

---

## API Endpoints

Suggested endpoints:

```http
GET /api/tickers
GET /api/tickers/{ticker}

GET /api/indicators/definitions
POST /api/indicators/definitions

GET /api/indicators/latest?ticker=AAPL
GET /api/indicators/history?ticker=AAPL&indicatorCode=RSI_14_W

GET /api/signals/definitions
POST /api/signals/definitions

GET /api/scanner/latest
GET /api/scanner/screener
GET /api/scanner/signals
GET /api/scanner/scores

GET /api/breadth/latest
GET /api/breadth/history?metricCode=SMA_200_D_ABOVE_PCT
```

Example screener query:

```http
GET /api/scanner/screener?country=US&signal=BMSB_BELOW&minMarketCap=1000000000&minDollarVolume=10000000&sort=compositeScoreDesc
```

---

## Batch Job Flow

Nightly job:

```text
1. Import/update ticker universe
2. Download/update daily price candles
3. Aggregate weekly candles
4. Aggregate monthly candles
5. Calculate active indicators
6. Evaluate active signal rules
7. Calculate composite scores
8. Calculate breadth snapshots
9. Export optional CSV
10. Send optional alert summary
```

Weekly job:

```text
1. Recalculate weekly indicators after weekly close
2. Recalculate weekly breadth
3. Recalculate weekly signal transitions
```

Monthly job:

```text
1. Recalculate monthly candles
2. Recalculate monthly RSI and long-term indicators
```

---

## Data Sources

Start simple:

```text
Yahoo Finance / yfinance-compatible library
```

Better production options:

```text
Polygon.io
Tiingo
EOD Historical Data
Financial Modeling Prep
Nasdaq Data Link
```

Coverage needed:

```text
NYSE
NASDAQ
AMEX
TSX
TSXV
```

Data provider abstraction:

```csharp
public interface IMarketDataProvider
{
    Task<IReadOnlyList<TickerDto>> GetTickersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PriceCandleDto>> GetHistoricalCandlesAsync(
        string symbol,
        string exchange,
        Timeframe timeframe,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken);
}
```

---

## Implementation Phases

## Phase 1 — Generic Vertical Slice

Objective:

Build enough generic infrastructure to calculate multiple indicators for a small ticker universe.

Tasks:

1. Create schema:
   - ticker
   - price_candle
   - indicator_definition
   - indicator_value
2. Seed indicator definitions:
   - BMSB_W
   - RSI_14_D
   - RSI_14_W
   - SMA_200_D
   - SMA_50_W
   - BB_20_2_D
3. Download candles for 50-100 liquid tickers.
4. Calculate indicators.
5. Export latest indicator values to CSV.

Deliverable:

```text
generic_indicator_latest.csv
```

---

## Phase 2 — Signal Engine

Objective:

Add composable signal rules.

Tasks:

1. Create signal_definition.
2. Create signal_result.
3. Implement simple rule evaluator.
4. Seed useful signal rules:
   - BMSB_BELOW
   - BMSB_ABOVE
   - RSI_D_OVERSOLD
   - RSI_W_OVERSOLD
   - PRICE_ABOVE_200D_SMA
   - PRICE_BELOW_200D_SMA
   - BB_D_BELOW_LOWER
   - BB_D_ABOVE_UPPER
5. Export matched signals to CSV.

Deliverable:

```text
signal_results_latest.csv
```

---

## Phase 3 — Composite Scoring

Objective:

Rank tickers by technical confluence.

Tasks:

1. Create scan_score table.
2. Implement score definitions in JSON.
3. Add transparent component-level score output.
4. Create initial scores:
   - Deep Pullback Score
   - Trend Strength Score
   - Recovery Confirmation Score

Example:

```text
Deep Pullback Score:
    +2 if below BMSB
    +1 if daily RSI < 30
    +1 if weekly RSI < 40
    +1 if below 200D SMA
    +1 if below 50W SMA
    +1 if below lower daily Bollinger Band
```

Deliverable:

```text
ranked_technical_scan_latest.csv
```

---

## Phase 4 — Full Universe Scanner

Objective:

Scale from prototype to thousands of US/Canadian equities.

Tasks:

1. Import US and Canadian ticker universe.
2. Add provider rate limiting.
3. Add retries and per-ticker failure logging.
4. Add incremental candle update.
5. Run full indicator calculation.
6. Store latest scan outputs.

Deliverable:

```text
Nightly full-market scan.
```

---

## Phase 5 — API

Objective:

Expose scanner data.

Tasks:

1. Build ticker endpoints.
2. Build indicator endpoints.
3. Build signal endpoints.
4. Build screener endpoint.
5. Build breadth endpoints.
6. Add pagination, sorting, filtering.
7. Add Swagger/OpenAPI.

Deliverable:

```text
Queryable backend API.
```

---

## Phase 6 — Dashboard

Objective:

Create practical UI.

Tasks:

1. Market overview dashboard.
2. Generic screener table.
3. Stock detail page.
4. Breadth history charts.
5. Sector heatmaps.
6. CSV export.
7. Saved filters/watchlists.

Deliverable:

```text
React dashboard for technical scanning.
```

---

## Phase 7 — Alerts

Objective:

Notify on useful transitions.

Examples:

```text
Ticker crossed above BMSB
Ticker crossed below 200D SMA
Daily RSI crossed above 30
Weekly RSI crossed above 50
Price closed below lower Bollinger Band
Composite score exceeded threshold
Market breadth crossed below 20%
Market breadth crossed above 50%
```

Channels:

```text
Email
Telegram
Discord
Slack
Webhook
```

---

## MVP Completion Criteria

The MVP is complete when it can:

1. Load at least 500 US/Canadian tickers.
2. Download daily price candles.
3. Aggregate weekly and monthly candles.
4. Calculate:
   - BMSB weekly
   - RSI daily
   - RSI weekly
   - RSI monthly
   - SMA 200D
   - SMA 50W
   - Bollinger Bands daily
5. Evaluate simple signal rules.
6. Calculate generic breadth metrics.
7. Produce a sorted screener table.
8. Export results to CSV.
9. Run repeatably from CLI or scheduled worker.

---

## Example CLI Commands

```bash
dotnet run --project src/TechnicalScanner.Worker -- import-tickers
dotnet run --project src/TechnicalScanner.Worker -- update-prices --years 10
dotnet run --project src/TechnicalScanner.Worker -- aggregate-candles --timeframes W,M
dotnet run --project src/TechnicalScanner.Worker -- calculate-indicators
dotnet run --project src/TechnicalScanner.Worker -- evaluate-signals
dotnet run --project src/TechnicalScanner.Worker -- calculate-breadth
dotnet run --project src/TechnicalScanner.Worker -- export-csv ./exports/latest-technical-scan.csv
```

---

## Suggested Project Structure

```text
/src
  /TechnicalScanner.Api
  /TechnicalScanner.Application
  /TechnicalScanner.Domain
  /TechnicalScanner.Infrastructure
  /TechnicalScanner.Worker
  /TechnicalScanner.Tests
```

Application services:

```text
TickerImportService
PriceImportService
CandleAggregationService
IndicatorCalculationService
SignalEvaluationService
CompositeScoringService
BreadthCalculationService
AlertService
CsvExportService
```

Calculators:

```text
BmsbIndicatorCalculator
RsiIndicatorCalculator
SmaIndicatorCalculator
EmaIndicatorCalculator
BollingerBandsIndicatorCalculator
```

Rule evaluators:

```text
SimpleComparisonRuleEvaluator
CompositeRuleEvaluator
CrossingRuleEvaluator
TrendRuleEvaluator
```

---

## Testing Requirements

Unit tests:

```text
SMA calculation
EMA calculation
RSI calculation
Bollinger Band calculation
BMSB calculation
Daily-to-weekly aggregation
Daily-to-monthly aggregation
Signal rule evaluation
Composite score calculation
Insufficient-data handling
```

Integration tests:

```text
Import ticker list
Import daily candles
Aggregate weekly/monthly candles
Calculate all seeded indicators
Evaluate seeded signals
Generate breadth snapshots
Export CSV
```

Known test symbols:

```text
AAPL
MSFT
NVDA
TSLA
SPY
QQQ
SHOP.TO
RY.TO
TD.TO
XIU.TO
```

---

## Important Edge Cases

Handle:

```text
Stocks with insufficient candles
Ticker symbol changes
Delisted tickers
Splits and dividends
Illiquid stocks
Bad candles
Duplicate symbols across countries
Canadian suffix differences
ETFs vs common stocks
Preferred shares and warrants
Currency differences
Market holidays
Partial current week/month data
Provider rate limits
Provider missing fields
Zero volume candles
Negative or zero prices
```

---

## Agent Instructions

When implementing:

1. Build a generic indicator engine, not a BMSB-only app.
2. Keep indicator definitions in the database.
3. Keep signal definitions data-driven where possible.
4. Isolate provider-specific ticker formatting behind interfaces.
5. Use adjusted close where possible.
6. Log per-ticker failures without killing the full scan.
7. Add CSV export early for sanity checking.
8. Add unit tests for every indicator formula.
9. Make calculations deterministic and reproducible.
10. Store enough raw values to debug every signal.
11. Keep scoring transparent with component breakdowns.
12. Do not treat scores as trading advice; they are ranking heuristics.
13. Prefer a working vertical slice before scaling to the full market.

---

## Future Enhancements

```text
MACD
ADX
ATR
Stochastic RSI
Volume moving averages
Relative strength vs SPY/QQQ/XIU
Sector-relative strength
52-week high/low distance
Drawdown from all-time high
Earnings-date proximity
Insider buying data
Options IV rank
Valuation metrics
Portfolio/watchlist overlay
Backtesting engine
AI-generated daily market summary
```

---

## Final Design Note

The most valuable output may be less about any one indicator and more about technical confluence.

Example high-confidence pullback setup:

```text
Below weekly BMSB
Below 50W SMA
Near 200W SMA
Daily RSI oversold
Weekly RSI weak but stabilizing
Daily Bollinger %B below 0
Market breadth washed out
```

Example broad-market overheating setup:

```text
90%+ of stocks above 200D SMA
85%+ above weekly BMSB
Daily RSI overbought across many sectors
Many stocks above upper Bollinger Bands
Low number of oversold stocks
```

The system should make these relationships easy to query, rank, visualize, and backtest.
