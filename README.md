# Technical Scanner

Generic technical indicator market scanner for US and Canadian equities. Evaluates modular indicators (BMSB, RSI, SMA, EMA, Bollinger Bands), applies composable signal rules, calculates breadth metrics, and exports scan results.

Built from the implementation plan in `docs/generic_technical_market_scanner_plan.md`.

## Architecture

```text
Ticker universe
    -> price data
        -> indicator calculations
            -> signal rules
                -> scan results
                    -> breadth summaries
                        -> dashboard / alerts / backtests
```

### Projects

| Project | Purpose |
|---------|---------|
| `TechnicalScanner.Domain` | Entities and enums |
| `TechnicalScanner.Application` | Indicator calculators, services, signal engine |
| `TechnicalScanner.Infrastructure` | EF Core persistence, market data providers |
| `TechnicalScanner.Worker` | CLI batch jobs |
| `TechnicalScanner.Api` | REST API (Phase 5 skeleton) |
| `TechnicalScanner.Tests` | Unit tests |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (optional, for PostgreSQL)

## Quick start

### 1. Start PostgreSQL

```bash
docker compose up -d
```

### 2. Run migrations and seed data

Migrations run automatically on worker/API startup.

### 3. Run the Phase 1 vertical slice

```bash
dotnet run --project src/TechnicalScanner.Worker -- import-tickers
dotnet run --project src/TechnicalScanner.Worker -- update-prices --years 10
dotnet run --project src/TechnicalScanner.Worker -- aggregate-candles --timeframes W,M
dotnet run --project src/TechnicalScanner.Worker -- calculate-indicators
dotnet run --project src/TechnicalScanner.Worker -- export-csv ./exports/generic_indicator_latest.csv
```

Optional follow-on commands:

```bash
dotnet run --project src/TechnicalScanner.Worker -- evaluate-signals
dotnet run --project src/TechnicalScanner.Worker -- calculate-scores
dotnet run --project src/TechnicalScanner.Worker -- calculate-breadth
```

### 4. Start the API

```bash
dotnet run --project src/TechnicalScanner.Api
```

Swagger UI is available at `/swagger`.

#### Key endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /api/tickers` | Paginated ticker list with filters |
| `GET /api/tickers/{symbol}` | Single ticker detail |
| `GET /api/indicators/definitions` | Active indicator definitions |
| `POST /api/indicators/definitions` | Create indicator definition |
| `GET /api/indicators/latest?ticker=AAPL` | Latest indicators for a ticker |
| `GET /api/indicators/history?ticker=AAPL&indicatorCode=RSI_14_W` | Indicator history |
| `GET /api/signals/definitions` | Active signal definitions |
| `POST /api/signals/definitions` | Create signal definition |
| `GET /api/scanner/latest` | Latest scan summary + breadth |
| `GET /api/scanner/screener` | Filterable/sortable screener |
| `GET /api/scanner/signals` | Matched signals (paginated) |
| `GET /api/scanner/scores` | Composite scores (paginated) |
| `GET /api/breadth/latest` | Latest breadth metrics |
| `GET /api/breadth/history?metricCode=...` | Breadth metric history |

Example screener query:

```http
GET /api/scanner/screener?country=US&signal=BMSB_BELOW&minCompositeScore=3&sort=compositeScoreDesc&page=1&pageSize=50
```

## Configuration

Connection string and market data settings in `appsettings.json`:

```json
"ConnectionStrings": {
  "TechnicalScanner": "Host=localhost;Port=5432;Database=technical_scanner;Username=postgres;Password=postgres"
},
"MarketData": {
  "Provider": "YahooFinance",
  "RequestsPerSecond": 2,
  "MaxRetries": 3,
  "RetryDelaySeconds": 2,
  "InitialHistoryYears": 10,
  "Universe": {
    "IncludeUs": true,
    "IncludeCanada": true,
    "IncludeEtfs": true
  }
}
```

## Seeded indicators

- `BMSB_W` — Bull Market Support Band (weekly)
- `RSI_14_D`, `RSI_14_W`, `RSI_14_M`
- `SMA_50_W`, `SMA_100_W`, `SMA_200_W`, `SMA_200_D`
- `EMA_21_W`
- `BB_20_2_D`, `BB_20_2_W`, `BB_20_2_M`

## Market data

Phase 4 uses **Yahoo Finance** for historical candles and public US/CA universe sources:

- US: NASDAQ Trader symbol directories (NYSE, NASDAQ, AMEX, etc.)
- Canada: TSX / TSXV company directories

Set `"MarketData": { "Provider": "Seed" }` in `appsettings.json` to use the original 10-ticker synthetic provider for local/dev testing.

### Rate limiting and retries

Market data calls are wrapped with configurable rate limiting and transient retries (`RequestsPerSecond`, `MaxRetries`, `RetryDelaySeconds` in `appsettings.json`).

### Incremental price updates

`update-prices` is incremental by default (fetches only new candles since the last stored date, with a short lookback for corrections). Use `--full` to reload the full history window.

### Nightly full-market scan

```bash
dotnet run --project src/TechnicalScanner.Worker -- run-nightly-scan --years 10 --export ./exports/generic_indicator_latest.csv
```

This runs: import tickers → incremental prices → aggregate candles → indicators → signals → scores → breadth → optional CSV export. Failures are logged per ticker in `ticker_operation_failure` and summarized in `scan_run`.

## Tests

```bash
dotnet test
```

## Implementation phases

| Phase | Status |
|-------|--------|
| 1 — Generic vertical slice | Scaffolded |
| 2 — Signal engine | Scaffolded |
| 3 — Composite scoring | Scaffolded |
| 4 — Full universe scanner | Implemented |
| 5 — API | Implemented |
| 6 — Dashboard | Planned |
| 7 — Alerts | Planned |

## Disclaimer

Scores and signals are ranking heuristics for research — not trading advice.
