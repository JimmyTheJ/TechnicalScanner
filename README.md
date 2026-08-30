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
| `TechnicalScanner.Api` | REST API |
| `TechnicalScanner.Web` | React dashboard (Phase 6) |
| `TechnicalScanner.Tests` | Unit tests |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (optional, for PostgreSQL and the full stack)
- [Node.js](https://nodejs.org/) 22+ (only for local dashboard `npm run dev`)

## Docker

Copy `.env.example` to `.env`, then use the compose wrappers (not bare `docker compose`) so an optional shared network is picked up:

```powershell
Copy-Item .env.example .env
.\compose.ps1 up --build -d
```

On Linux/macOS, use `./compose.sh` instead of `.\compose.ps1`.

Defaults:

| Service | Local URL | Container | Container port |
|---------|-----------|-----------|----------------|
| Dashboard | http://localhost:3080 | `technical_scanner_web` | `80` |
| API / Swagger | http://localhost:5223/swagger (also via the dashboard at `/swagger`) | `technical_scanner_api` | `8080` |
| PostgreSQL | `localhost:5432` | `technical_scanner_postgres` | `5432` |

Override `WEB_PORT`, `API_PORT`, or `POSTGRES_PORT` in `.env` if those host ports are taken.

Postgres data is a bind mount. Set `POSTGRES_DATA_PATH` in `.env` (default `./data/postgres`). On a Linux server this can be an absolute path such as `/mnt/d/technical-scanner/postgres`. Create the directory first if it does not exist. Changing this path (or `POSTGRES_PASSWORD`) after the first start does not migrate existing data.

To attach the API and dashboard to an existing reverse-proxy network (e.g. nginx), uncomment `DOCKER_SHARED_NETWORK=nginx_network` in `.env`. The network must already exist. Nginx Proxy Manager should forward with scheme `http` to hostname `technical_scanner_web` on port `80` (the container port, not the host port). Point a separate API host at `technical_scanner_api` on port `8080` if you need the API on its own hostname.

Verify the overlay with `.\compose.ps1 config` — you should see both the project default network and `name: nginx_network`.

Worker jobs are a compose profile (not started by `up`). Example:

```powershell
.\compose.ps1 --profile worker run --rm technical_scanner_worker run-nightly-scan --years 10
```

## Quick start

### 1. Start PostgreSQL

Full stack (Postgres + API + dashboard):

```powershell
.\compose.ps1 up --build -d
```

Postgres only, for host-run `dotnet` / `npm`:

```powershell
.\compose.ps1 up -d postgres
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
| `GET /api/alerts/events` | Recent alert events |
| `GET /api/alerts/events/{id}` | Single alert event |
| `GET /api/alerts/events/{id}/deliveries` | Channel delivery status |

Example screener query:

```http
GET /api/scanner/screener?country=US&signal=BMSB_BELOW&minCompositeScore=3&sort=compositeScoreDesc&page=1&pageSize=50
```

### 5. Start the dashboard

In a second terminal:

```bash
cd src/TechnicalScanner.Web
npm install
npm run dev
```

The dashboard runs at `http://localhost:5173` and proxies API requests to `http://localhost:5223`.

Pages:
- **Overview** — latest scan summary, breadth bars, sector heatmap, recent signal matches
- **Screener** — filterable/sortable table with CSV export and saved filters
- **Breadth** — latest breadth metrics and history charts
- **Watchlists** — local watchlists and saved screener filters
- **Stock detail** — `/stocks/{symbol}` indicator breakdown and RSI history

## Configuration

Edit the root `.env` file (copy from `.env.example`). Keys use `__` in place of JSON nesting and overlay `appsettings.json` for both `dotnet run` and Docker:

```env
POSTGRES_DATA_PATH=./data/postgres
ConnectionStrings__TechnicalScanner=Host=localhost;Port=5432;Database=technical_scanner;Username=postgres;Password=postgres
MarketData__Provider=YahooFinance
Alerts__Enabled=true
Alerts__Channels__Email__Password=your-app-password
```

That maps onto the same shape as `appsettings.json`:

```json
"ConnectionStrings": {
  "TechnicalScanner": "Host=localhost;Port=5432;Database=technical_scanner;Username=postgres;Password=postgres"
},
"MarketData": {
  "Provider": "YahooFinance"
},
"Alerts": {
  "Enabled": true,
  "Channels": {
    "Email": {
      "Password": "your-app-password"
    }
  }
}
```

Compose passes `.env` into the API and worker containers. Inside those containers the connection string `Host` is `technical-scanner-postgres` (a unique DNS-safe alias on this project's default network). `.NET` cannot resolve names with underscores such as `technical_scanner_postgres`. Host-run apps keep `Host=localhost`. Postgres is not attached to the reverse-proxy network.

`appsettings.Local.json` is still loaded if present, then `.env`, then process environment variables.

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

Set `MarketData__Provider=Seed` in `.env` (or `"Provider": "Seed"` in `appsettings.json`) to use the original 10-ticker synthetic provider for local/dev testing.

### Rate limiting and retries

Market data calls are wrapped with configurable rate limiting and transient retries (`MarketData__RequestsPerSecond`, `MarketData__MaxRetries`, `MarketData__RetryDelaySeconds` in `.env`).

### Incremental price updates

`update-prices` is incremental by default (fetches only new candles since the last stored date, with a short lookback for corrections). Use `--full` to reload the full history window.

### Nightly full-market scan

```bash
dotnet run --project src/TechnicalScanner.Worker -- run-nightly-scan --years 10 --export ./exports/generic_indicator_latest.csv
```

This runs: import tickers → incremental prices → aggregate candles → indicators → signals → scores → breadth → optional CSV export → optional alerts. Failures are logged per ticker in `ticker_operation_failure` and summarized in `scan_run`.

Use `--no-alerts` to skip alert processing on a nightly run. To replay alerts for a prior scan:

```bash
dotnet run --project src/TechnicalScanner.Worker -- send-alerts --scan-run 12
```

## Alerts (Phase 7)

Alerts detect **transitions** between consecutive completed scans:

- Signal fired or cleared (e.g. BMSB cross, RSI oversold recovery, price vs 200D SMA)
- Composite score crossing a threshold (default `DEEP_PULLBACK` ≥ 4)
- Market breadth crossing configured levels (default BMSB above-band % below 20% or above 50%)

Email, webhook, Telegram, Discord, and Slack channels are supported. **Email via SMTP is already implemented** — you only need configuration.

### Email with PurelyMail (recommended)

1. Create a sender address in PurelyMail (e.g. `scanner@yourdomain.com`).
2. If you use 2FA, generate an **app password** in PurelyMail.
3. Set email settings in the root `.env` (copy from `.env.example` if you have not already):

```env
Alerts__Enabled=true
Alerts__Channels__Email__Enabled=true
Alerts__Channels__Email__SmtpHost=smtp.purelymail.com
Alerts__Channels__Email__SmtpPort=587
Alerts__Channels__Email__UseSsl=true
Alerts__Channels__Email__Username=scanner@yourdomain.com
Alerts__Channels__Email__Password=your-app-password
Alerts__Channels__Email__FromAddress=scanner@yourdomain.com
Alerts__Channels__Email__ToAddress=you@yourdomain.com
```

`.env` is gitignored and loaded automatically by the Worker and API (and passed into those containers by Compose).

PurelyMail SMTP: `smtp.purelymail.com`, port **587** (STARTTLS) or **465** (SSL/TLS). Use your **full email address** as the username.

4. Send a test email (no scan required):

```bash
dotnet run --project src/TechnicalScanner.Worker -- test-alerts
```

5. Run at least **two** nightly scans before expecting transition alerts (the first scan establishes a baseline):

```bash
dotnet run --project src/TechnicalScanner.Worker -- run-nightly-scan --years 10
```

Use `--no-alerts` to skip alert processing on a one-off run. To replay alerts for a prior scan:

```bash
dotnet run --project src/TechnicalScanner.Worker -- send-alerts --scan-run 12
```

### Other channels

Enable other channels in `.env`:

```env
Alerts__Enabled=true
Alerts__NotifyOnSignalFired=true
Alerts__CompositeScoreThreshold=4
Alerts__Channels__Webhook__Enabled=true
Alerts__Channels__Webhook__Url=https://example.com/hooks/scanner
Alerts__Channels__Telegram__Enabled=true
Alerts__Channels__Telegram__BotToken=...
Alerts__Channels__Telegram__ChatId=...
Alerts__Channels__Discord__Enabled=true
Alerts__Channels__Discord__WebhookUrl=https://discord.com/api/webhooks/...
Alerts__Channels__Slack__Enabled=true
Alerts__Channels__Slack__WebhookUrl=https://hooks.slack.com/services/...
```

Alert events and per-channel delivery status are stored in `alert_event` / `alert_delivery` and exposed via the API.

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
| 6 — Dashboard | Implemented |
| 7 — Alerts | Implemented |

## Disclaimer

Scores and signals are ranking heuristics for research — not trading advice.
