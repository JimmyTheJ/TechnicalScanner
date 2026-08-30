import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  CartesianGrid,
  ComposedChart,
  Line,
  ResponsiveContainer,
  Scatter,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { api } from '../api/client'
import type {
  IndicatorLatest,
  PriceCandlePoint,
  ProfileBacktestResult,
  ProfileTickerSnapshot,
  ScanProfile,
  TickerListItem,
  TickerSignalHistory,
} from '../api/types'
import { ErrorState, LoadingState } from '../components/StateBlocks'
import { formatDate, formatDay, formatMarketCap, formatNumber, formatPercent } from '../utils/format'
import { isSymbolOnWatchlist, loadWatchlists, toggleSymbolOnWatchlist } from '../utils/storage'

export function StockDetailPage() {
  const { symbol = '' } = useParams()
  const [ticker, setTicker] = useState<TickerListItem | null>(null)
  const [indicators, setIndicators] = useState<IndicatorLatest[]>([])
  const [candles, setCandles] = useState<PriceCandlePoint[]>([])
  const [timeframe, setTimeframe] = useState('D')
  const [signals, setSignals] = useState<TickerSignalHistory[]>([])
  const [profiles, setProfiles] = useState<ScanProfile[]>([])
  const [profileCode, setProfileCode] = useState('')
  const [snapshot, setSnapshot] = useState<ProfileTickerSnapshot | null>(null)
  const [backtest, setBacktest] = useState<ProfileBacktestResult | null>(null)
  const [profileError, setProfileError] = useState<string | null>(null)
  const [loadingProfile, setLoadingProfile] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [watchlists, setWatchlists] = useState(loadWatchlists)

  useEffect(() => {
    setProfileCode('')
    setSnapshot(null)
    setBacktest(null)
    setCandles([])
    setSignals([])
  }, [symbol])

  useEffect(() => {
    if (!symbol) {
      return
    }

    let cancelled = false

    async function load() {
      try {
        setLoading(true)
        const [tickerResult, indicatorResult, signalResult, profileResult] = await Promise.all([
          api.getTicker(symbol),
          api.getIndicatorLatest(symbol),
          api.getTickerSignals(symbol),
          api.getProfiles().catch(() => [] as ScanProfile[]),
        ])

        if (!cancelled) {
          setTicker(tickerResult)
          setIndicators(indicatorResult)
          setSignals(signalResult)
          setProfiles(profileResult)
          setProfileCode((current) => current || profileResult[0]?.code || '')
          setError(null)
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load stock detail')
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [symbol])

  useEffect(() => {
    if (!symbol) {
      return
    }

    let cancelled = false
    void api
      .getTickerCandles(symbol, { timeframe, limit: 400 })
      .then((rows) => {
        if (!cancelled) {
          setCandles(rows)
        }
      })
      .catch(() => {
        if (!cancelled) {
          setCandles([])
        }
      })

    return () => {
      cancelled = true
    }
  }, [symbol, timeframe])

  useEffect(() => {
    if (!symbol || !profileCode) {
      return
    }

    let cancelled = false
    setLoadingProfile(true)
    setProfileError(null)

    Promise.all([api.getProfileSnapshot(profileCode, symbol), api.backtestProfile(profileCode, symbol, 20)])
      .then(([nextSnapshot, nextBacktest]) => {
        if (!cancelled) {
          setSnapshot(nextSnapshot)
          setBacktest(nextBacktest)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setSnapshot(null)
          setBacktest(null)
          setProfileError(err instanceof Error ? err.message : 'Failed to load profile workspace')
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoadingProfile(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [symbol, profileCode])

  const entryDates = useMemo(
    () => new Set((backtest?.entries ?? []).map((hit) => hit.date.slice(0, 10))),
    [backtest],
  )

  const chartData = useMemo(
    () =>
      candles.map((candle) => ({
        date: candle.candleDate,
        close: candle.close,
        entry: timeframe === 'D' && entryDates.has(candle.candleDate.slice(0, 10)) ? candle.close : null,
      })),
    [candles, entryDates, timeframe],
  )

  if (loading) {
    return (
      <div className="page-stack">
        <WatchlistBar symbol={symbol} watchlists={watchlists} onChange={() => setWatchlists(loadWatchlists())} />
        <LoadingState message={`Loading ${symbol}…`} />
      </div>
    )
  }

  if (error || !ticker) {
    return (
      <div className="page-stack">
        <WatchlistBar symbol={symbol} watchlists={watchlists} onChange={() => setWatchlists(loadWatchlists())} />
        <ErrorState message={error ?? `Ticker ${symbol} not found`} />
      </div>
    )
  }

  const rsiDaily = metric(indicators, 'RSI_14_D', 'rsi')
  const rsiWeekly = metric(indicators, 'RSI_14_W', 'rsi')
  const rsiMonthly = metric(indicators, 'RSI_14_M', 'rsi')
  const sma200 = metric(indicators, 'SMA_200_D', 'distanceFromSmaPct')
  const sma50w = metric(indicators, 'SMA_50_W', 'distanceFromSmaPct')
  const bollinger = text(indicators, 'BB_20_2_D', 'status')
  const bmsb = text(indicators, 'BMSB_W', 'status')

  return (
    <div className="page-stack">
      <section className="hero-card">
        <div>
          <p className="eyebrow">{ticker.exchange} · {ticker.country}</p>
          <h2>{ticker.symbol}</h2>
          <p>{ticker.companyName ?? 'Unknown company'}</p>
          <p className="muted">
            {ticker.sector ?? 'Unknown sector'} · {ticker.industry ?? 'Unknown industry'}
          </p>
          <WatchlistBar symbol={ticker.symbol} watchlists={watchlists} onChange={() => setWatchlists(loadWatchlists())} />
        </div>
        <div className="detail-stats">
          <div>
            <span>Market Cap</span>
            <strong>{formatMarketCap(ticker.marketCap)}</strong>
          </div>
          <div>
            <span>ETF</span>
            <strong>{ticker.isEtf ? 'Yes' : 'No'}</strong>
          </div>
          <div>
            <span>Profile</span>
            <strong>{snapshot?.profileMatch ? 'Match' : snapshot ? 'No match' : '—'}</strong>
          </div>
        </div>
      </section>

      <section className="stat-grid">
        <article className="metric-chip">
          <span>Daily RSI</span>
          <strong>{formatNumber(rsiDaily, 1)}</strong>
        </article>
        <article className="metric-chip">
          <span>Weekly RSI</span>
          <strong>{formatNumber(rsiWeekly, 1)}</strong>
        </article>
        <article className="metric-chip">
          <span>Monthly RSI</span>
          <strong>{formatNumber(rsiMonthly, 1)}</strong>
        </article>
        <article className="metric-chip">
          <span>vs 200D SMA</span>
          <strong>{formatPercent(sma200)}</strong>
        </article>
        <article className="metric-chip">
          <span>vs 50W SMA</span>
          <strong>{formatPercent(sma50w)}</strong>
        </article>
        <article className="metric-chip">
          <span>Bollinger</span>
          <strong>{bollinger ?? '—'}</strong>
        </article>
        <article className="metric-chip">
          <span>BMSB</span>
          <strong>{bmsb ?? '—'}</strong>
        </article>
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Price</h3>
          <div className="button-row">
            {['D', 'W', 'M'].map((code) => (
              <button
                key={code}
                type="button"
                className={timeframe === code ? 'button' : 'button secondary'}
                onClick={() => setTimeframe(code)}
              >
                {code}
              </button>
            ))}
            <Link to="/screener">Back to screener</Link>
          </div>
        </div>
        {chartData.length === 0 ? (
          <p className="muted">No stored candles for this ticker yet. Import prices, then reopen this page.</p>
        ) : (
          <div className="chart-container price-chart">
            <ResponsiveContainer width="100%" height={360}>
              <ComposedChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.2)" />
                <XAxis dataKey="date" minTickGap={28} tickFormatter={formatDay} />
                <YAxis domain={['auto', 'auto']} />
                <Tooltip labelFormatter={(label) => formatDay(String(label))} />
                <Line type="monotone" dataKey="close" stroke="#38bdf8" strokeWidth={2} dot={false} name="Close" />
                <Scatter dataKey="entry" fill="#f59e0b" name="Profile entry" />
              </ComposedChart>
            </ResponsiveContainer>
          </div>
        )}
        {timeframe === 'D' && backtest && backtest.entryCount > 0 ? (
          <p className="muted">Orange dots are {profileCode} entries from stored daily history.</p>
        ) : null}
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Profile workspace</h3>
          <div className="button-row">
            <select value={profileCode} onChange={(e) => setProfileCode(e.target.value)}>
              {profiles.map((profile) => (
                <option key={profile.code} value={profile.code}>
                  {profile.name}
                </option>
              ))}
            </select>
          </div>
        </div>
        {loadingProfile ? <p className="muted">Scoring and replaying {profileCode}…</p> : null}
        {profileError ? <p className="muted">{profileError}</p> : null}
        {snapshot ? (
          <>
            <div className="chip-row">
              <span className={snapshot.profileMatch ? 'chip match' : 'chip nomatch'}>
                {snapshot.profileMatch ? 'Rule match' : 'No rule match'}
              </span>
              <span className="chip">Score {formatNumber(snapshot.score, 1)}</span>
            </div>
            {snapshot.components.length > 0 ? (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Component</th>
                      <th>Metric</th>
                      <th>Rule</th>
                      <th>Hit</th>
                      <th>Weight</th>
                    </tr>
                  </thead>
                  <tbody>
                    {snapshot.components.map((component) => (
                      <tr key={`${component.label}-${component.indicatorCode}`}>
                        <td>{component.label}</td>
                        <td>{component.indicatorCode}.{component.field}</td>
                        <td>{component.operator}</td>
                        <td className={component.matched ? 'hit' : 'miss'}>{component.matched ? 'Yes' : 'No'}</td>
                        <td>{formatNumber(component.weight, 1)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="muted">This profile has no score weights.</p>
            )}
          </>
        ) : null}
        {backtest ? (
          <>
            <p className="muted">
              {backtest.entryCount} entries · {backtest.matchDays} matching days · {backtest.forwardDays}d forward return
            </p>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Close</th>
                    <th>Score</th>
                    <th>Fwd %</th>
                  </tr>
                </thead>
                <tbody>
                  {backtest.entries.map((hit) => (
                    <tr key={hit.date}>
                      <td>{formatDay(hit.date)}</td>
                      <td>{formatNumber(hit.close)}</td>
                      <td>{formatNumber(hit.score, 1)}</td>
                      <td>{formatPercent(hit.forwardReturnPct)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        ) : (
          <p className="muted">Pick a profile to score the latest bar and replay stored history.</p>
        )}
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Latest scan signals</h3>
        </div>
        {signals.length === 0 ? (
          <p className="muted">No evaluated signals yet. Run a scan after indicators are calculated.</p>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>When</th>
                  <th>Signal</th>
                  <th>Match</th>
                </tr>
              </thead>
              <tbody>
                {signals.map((row) => (
                  <tr key={`${row.scanDateUtc}-${row.signalCode}`}>
                    <td>{formatDate(row.scanDateUtc)}</td>
                    <td>{row.signalName}</td>
                    <td className={row.isMatch ? 'hit' : 'miss'}>{row.isMatch ? 'Yes' : 'No'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Latest indicators</h3>
        </div>
        <div className="indicator-grid">
          {indicators.map((indicator) => (
            <article key={indicator.code} className="indicator-card">
              <h4>{indicator.code}</h4>
              <p className="muted">{indicator.name}</p>
              <p>{formatDay(indicator.candleDate)}</p>
              <strong>{formatNumber(indicator.primaryValue, 2)}</strong>
              <p className="muted">{summarizeValues(indicator)}</p>
            </article>
          ))}
        </div>
      </section>
    </div>
  )
}

function metric(indicators: IndicatorLatest[], code: string, field: string): number | undefined {
  const indicator = indicators.find((item) => item.code === code)
  const value = indicator?.values?.[field]
  return typeof value === 'number' ? value : indicator?.primaryValue
}

function text(indicators: IndicatorLatest[], code: string, field: string): string | undefined {
  const value = indicators.find((item) => item.code === code)?.values?.[field]
  return typeof value === 'string' ? value : undefined
}

function summarizeValues(indicator: IndicatorLatest): string {
  const entries = Object.entries(indicator.values ?? {})
    .filter(([, value]) => value !== null && typeof value !== 'object')
    .slice(0, 4)
    .map(([key, value]) => `${key} ${String(value)}`)
  return entries.join(' · ') || 'No extra fields'
}

function WatchlistBar({
  symbol,
  watchlists,
  onChange,
}: {
  symbol: string
  watchlists: ReturnType<typeof loadWatchlists>
  onChange: () => void
}) {
  return (
    <div className="chip-row">
      {watchlists.length === 0 ? (
        <Link to="/watchlists" className="chip">
          Create a watchlist
        </Link>
      ) : (
        watchlists.map((list) => {
          const onList = isSymbolOnWatchlist(list, symbol)
          return (
            <button
              key={list.id}
              type="button"
              className={onList ? 'chip match' : 'chip nomatch'}
              onClick={() => {
                toggleSymbolOnWatchlist(list.id, symbol)
                onChange()
              }}
            >
              {onList ? `In ${list.name}` : `Add to ${list.name}`}
            </button>
          )
        })
      )}
    </div>
  )
}
