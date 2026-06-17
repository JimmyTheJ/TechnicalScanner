import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { ScannerLatest, ScreenerRow, SignalMatch } from '../api/types'
import { BreadthBarChart } from '../components/BreadthCharts'
import { SectorHeatmap } from '../components/SectorHeatmap'
import { StatCard } from '../components/StatCard'
import { ErrorState, LoadingState } from '../components/StateBlocks'
import { formatDate, formatPercent, metricLabel } from '../utils/format'
import { buildSectorHeatmap } from '../utils/sectorHeatmap'

export function OverviewPage() {
  const [latest, setLatest] = useState<ScannerLatest | null>(null)
  const [matches, setMatches] = useState<SignalMatch[]>([])
  const [heatmapRows, setHeatmapRows] = useState<ScreenerRow[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    async function load() {
      try {
        setLoading(true)
        const [latestResult, signalResult, screenerResult] = await Promise.all([
          api.getScannerLatest(),
          api.getSignalMatches({ page: 1, pageSize: 10 }),
          api.getScreener({ page: 1, pageSize: 500, sort: 'compositeScoreDesc' }),
        ])

        if (!cancelled) {
          setLatest(latestResult)
          setMatches(signalResult.items)
          setHeatmapRows(screenerResult.items)
          setError(null)
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load overview')
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
  }, [])

  if (loading) {
    return <LoadingState message="Loading market overview…" />
  }

  if (error || !latest) {
    return <ErrorState message={error ?? 'Overview data unavailable'} />
  }

  const breadthItems = latest.breadth
    .slice(0, 8)
    .map((item) => ({ label: metricLabel(item.metricCode), value: Number(item.matchPct.toFixed(1)) }))

  return (
    <div className="page-stack">
      <section className="hero-card">
        <div>
          <p className="eyebrow">Latest Scan</p>
          <h2>{formatDate(latest.scanDateUtc)}</h2>
          <p className="muted">
            Status: {latest.scanStatus ?? 'Unknown'} · Run #{latest.scanRunId ?? '—'}
          </p>
        </div>
        <Link className="button" to="/screener">
          Open Screener
        </Link>
      </section>

      <section className="stat-grid">
        <StatCard label="Active Tickers" value={latest.activeTickers} />
        <StatCard label="Matched Signals" value={latest.matchedSignals} />
        <StatCard
          label="Breadth Metrics"
          value={latest.breadth.length}
          hint="Tracked in the latest scan"
        />
      </section>

      <section className="panel-grid">
        <BreadthBarChart title="Market Breadth Snapshot" items={breadthItems} />
        <div className="panel-card">
          <div className="panel-header">
            <h3>Recent Signal Matches</h3>
            <Link to="/screener">View all</Link>
          </div>
          <ul className="simple-list">
            {matches.map((match) => (
              <li key={`${match.symbol}-${match.signalCode}`}>
                <Link to={`/stocks/${match.symbol}`}>{match.symbol}</Link>
                <span>{match.signalName}</span>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Sector Heatmap</h3>
          <span className="muted">Based on top screener rows</span>
        </div>
        <SectorHeatmap cells={buildSectorHeatmap(heatmapRows)} />
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Key Breadth Metrics</h3>
        </div>
        <div className="metric-grid">
          {latest.breadth.map((metric) => (
            <article key={metric.metricCode} className="metric-chip">
              <p>{metricLabel(metric.metricCode)}</p>
              <strong>{formatPercent(metric.matchPct)}</strong>
              <span className="muted">
                {metric.matchCount}/{metric.totalValidStocks}
              </span>
            </article>
          ))}
        </div>
      </section>
    </div>
  )
}
