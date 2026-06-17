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
import type { IndicatorHistoryPoint, IndicatorLatest, TickerListItem } from '../api/types'
import { ErrorState, LoadingState } from '../components/StateBlocks'
import { formatDate, formatMarketCap, formatNumber } from '../utils/format'

export function StockDetailPage() {
  const { symbol = '' } = useParams()
  const [ticker, setTicker] = useState<TickerListItem | null>(null)
  const [indicators, setIndicators] = useState<IndicatorLatest[]>([])
  const [rsiHistory, setRsiHistory] = useState<IndicatorHistoryPoint[]>([])
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
        const [tickerResult, indicatorResult, historyResult] = await Promise.all([
          api.getTicker(symbol),
          api.getIndicatorLatest(symbol),
          api.getIndicatorHistory(symbol, 'RSI_14_W'),
        ])

        if (!cancelled) {
          setTicker(tickerResult)
          setIndicators(indicatorResult)
          setRsiHistory(historyResult)
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
