import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { BreadthMetric, ScannerLatest, ScreenerRow, SignalMatch } from '../api/types'
import { BreadthBarChart } from '../components/BreadthCharts'
import { SectorHeatmap } from '../components/SectorHeatmap'
import { StatCard } from '../components/StatCard'
import { ErrorState, LoadingState } from '../components/StateBlocks'
import { formatDate, formatPercent, metricLabel, profileCodeFromMetric } from '../utils/format'
import { loadWatchlists } from '../utils/storage'
import { buildSectorHeatmap } from '../utils/sectorHeatmap'

export function OverviewPage() {
  const [latest, setLatest] = useState<ScannerLatest | null>(null)
  const [matches, setMatches] = useState<SignalMatch[]>([])
  const [heatmapRows, setHeatmapRows] = useState<ScreenerRow[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [watchlistStats, setWatchlistStats] = useState<
    { id: string; name: string; total: number; matches: number }[]
  >([])

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

  useEffect(() => {
    const lists = loadWatchlists().slice(0, 4)
    if (lists.length === 0) {
      setWatchlistStats([])
      return
    }

    let cancelled = false

    async function loadLists() {
      const stats = await Promise.all(
        lists.map(async (list) => {
          if (list.symbols.length === 0) {
            return { id: list.id, name: list.name, total: 0, matches: 0 }
          }

          try {
            const result = await api.getScreener({
              symbols: list.symbols.join(','),
              profile: 'DEEP_PULLBACK',
              profileMatchOnly: false,
              page: 1,
              pageSize: Math.min(200, Math.max(list.symbols.length, 1)),
              sort: 'compositeScoreDesc',
            })
            return {
              id: list.id,
              name: list.name,
              total: result.totalCount,
              matches: result.items.filter((row) => (row.compositeScore ?? 0) > 0).length,
            }
          } catch {
            return { id: list.id, name: list.name, total: list.symbols.length, matches: 0 }
          }
        }),
      )

      if (!cancelled) {
        setWatchlistStats(stats)
      }
    }

    void loadLists()
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

  const profileBreadth = latest.breadth.filter((item) => profileCodeFromMetric(item.metricCode))
  const structureBreadth = latest.breadth.filter((item) => !profileCodeFromMetric(item.metricCode))
  const washout = profileBreadth.reduce<BreadthMetric | null>(
    (best, item) => (best === null || item.matchPct > best.matchPct ? item : best),
    null,
  )
  const structureItems = structureBreadth
    .slice(0, 8)
    .map((item) => ({ label: metricLabel(item.metricCode), value: Number(item.matchPct.toFixed(1)) }))
  const profileItems = profileBreadth.map((item) => ({
    label: metricLabel(item.metricCode),
    value: Number(item.matchPct.toFixed(1)),
  }))

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
          label={washout ? metricLabel(washout.metricCode) : 'Cheapness'}
          value={washout ? formatPercent(washout.matchPct) : '—'}
          hint={washout ? `${washout.matchCount}/${washout.totalValidStocks} matching` : 'Run calculate-breadth'}
        />
      </section>

      <section className="panel-grid">
        <BreadthBarChart title="Market structure" items={structureItems} />
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

      {watchlistStats.length > 0 ? (
        <section className="panel-card">
          <div className="panel-header">
            <h3>Watchlists</h3>
            <Link to="/watchlists">Manage</Link>
          </div>
          <p className="muted">Names on your lists that currently score above zero on Deep Pullback.</p>
          <div className="metric-grid">
            {watchlistStats.map((list) => (
              <article key={list.id} className="metric-chip">
                <p>{list.name}</p>
                <strong>
                  {list.matches}/{list.total}
                </strong>
                <Link
                  to={`/screener?watchlist=${encodeURIComponent(list.id)}&profile=DEEP_PULLBACK&profileMatchOnly=false`}
                >
                  Screen
                </Link>
              </article>
            ))}
          </div>
        </section>
      ) : null}

      {profileItems.length > 0 ? (
        <section className="panel-card">
          <div className="panel-header">
            <h3>Cheapness profiles</h3>
            <Link to="/breadth">History</Link>
          </div>
          <p className="muted">Share of the universe that currently matches each saved recipe.</p>
          <BreadthBarChart title="Profile washout" items={profileItems} />
          <div className="metric-grid">
            {profileBreadth.map((metric) => {
              const profileCode = profileCodeFromMetric(metric.metricCode)
              return (
                <article key={metric.metricCode} className="metric-chip">
                  <p>{metricLabel(metric.metricCode)}</p>
                  <strong>{formatPercent(metric.matchPct)}</strong>
                  <span className="muted">
                    {metric.matchCount}/{metric.totalValidStocks}
                  </span>
                  {profileCode ? (
                    <Link to={`/screener?profile=${encodeURIComponent(profileCode)}`}>Screen</Link>
                  ) : null}
                </article>
              )
            })}
          </div>
        </section>
      ) : null}

      <section className="panel-card">
        <div className="panel-header">
          <h3>Sector Heatmap</h3>
          <span className="muted">Based on top screener rows</span>
        </div>
        <SectorHeatmap cells={buildSectorHeatmap(heatmapRows)} />
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Key structure metrics</h3>
        </div>
        <div className="metric-grid">
          {structureBreadth.map((metric) => (
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
