import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { api } from '../api/client'
import type { IndicatorHistoryPoint, IndicatorLatest, ProfileBacktestResult, ScanProfile, TickerListItem } from '../api/types'
import { ErrorState, LoadingState } from '../components/StateBlocks'
import { formatDate, formatMarketCap, formatNumber, formatPercent } from '../utils/format'

export function StockDetailPage() {
  const { symbol = '' } = useParams()
  const [ticker, setTicker] = useState<TickerListItem | null>(null)
  const [indicators, setIndicators] = useState<IndicatorLatest[]>([])
  const [rsiHistory, setRsiHistory] = useState<IndicatorHistoryPoint[]>([])
  const [profiles, setProfiles] = useState<ScanProfile[]>([])
  const [profileCode, setProfileCode] = useState('')
  const [backtest, setBacktest] = useState<ProfileBacktestResult | null>(null)
  const [backtestError, setBacktestError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!symbol) {
      return
    }

    let cancelled = false

    async function load() {
      try {
        setLoading(true)
        const [tickerResult, indicatorResult, historyResult, profileResult] = await Promise.all([
          api.getTicker(symbol),
          api.getIndicatorLatest(symbol),
          api.getIndicatorHistory(symbol, 'RSI_14_W'),
          api.getProfiles().catch(() => [] as ScanProfile[]),
        ])

        if (!cancelled) {
          setTicker(tickerResult)
          setIndicators(indicatorResult)
          setRsiHistory(historyResult)
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

  if (loading) {
    return <LoadingState message={`Loading ${symbol}…`} />
  }

  if (error || !ticker) {
    return <ErrorState message={error ?? `Ticker ${symbol} not found`} />
  }

  const chartData = rsiHistory.map((point) => ({
    date: point.candleDate,
    rsi: point.primaryValue ?? 0,
  }))

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
        </div>
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Weekly RSI History</h3>
          <Link to="/screener">Back to screener</Link>
        </div>
        <div className="chart-container">
          <ResponsiveContainer width="100%" height={280}>
            <LineChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.2)" />
              <XAxis dataKey="date" />
              <YAxis domain={[0, 100]} />
              <Tooltip />
              <Line type="monotone" dataKey="rsi" stroke="#f59e0b" strokeWidth={2} dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Profile replay</h3>
          <div className="button-row">
            <select value={profileCode} onChange={(e) => setProfileCode(e.target.value)}>
              {profiles.map((profile) => (
                <option key={profile.code} value={profile.code}>
                  {profile.name}
                </option>
              ))}
            </select>
            <button
              type="button"
              className="button secondary"
              onClick={() => {
                if (!profileCode) {
                  return
                }
                setBacktestError(null)
                void api
                  .backtestProfile(profileCode, symbol, 20)
                  .then(setBacktest)
                  .catch((err: unknown) => {
                    setBacktest(null)
                    setBacktestError(err instanceof Error ? err.message : 'Backtest failed')
                  })
              }}
            >
              Replay
            </button>
          </div>
        </div>
        {backtestError ? <p className="muted">{backtestError}</p> : null}
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
                      <td>{hit.date}</td>
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
          <p className="muted">Replay a cheapness profile across this ticker’s stored indicator history.</p>
        )}
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Latest Indicators</h3>
        </div>
        <div className="indicator-grid">
          {indicators.map((indicator) => (
            <article key={indicator.code} className="indicator-card">
              <h4>{indicator.code}</h4>
              <p className="muted">{indicator.name}</p>
              <p>{formatDate(indicator.candleDate)}</p>
              <strong>{formatNumber(indicator.primaryValue, 2)}</strong>
              <pre>{JSON.stringify(indicator.values, null, 2)}</pre>
            </article>
          ))}
        </div>
      </section>
    </div>
  )
}
