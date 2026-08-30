import { useEffect, useState } from 'react'
import { api } from '../api/client'
import type { BreadthSnapshot } from '../api/types'
import { BreadthBarChart, BreadthHistoryChart } from '../components/BreadthCharts'
import { ErrorState, LoadingState } from '../components/StateBlocks'
import { metricLabel } from '../utils/format'

const defaultMetrics = [
  'BMSB_W_ABOVE_PCT',
  'BMSB_W_BELOW_PCT',
  'RSI_14_D_OVERSOLD_PCT',
  'SMA_200_D_ABOVE_PCT',
  'SMA_50_W_ABOVE_PCT',
  'BB_20_2_D_BELOW_LOWER_PCT',
  'PROFILE_DEEP_PULLBACK_PCT',
]

export function BreadthPage() {
  const [universe, setUniverse] = useState('ALL')
  const [latest, setLatest] = useState<BreadthSnapshot[]>([])
  const [selectedMetric, setSelectedMetric] = useState(defaultMetrics[0])
  const [history, setHistory] = useState<BreadthSnapshot[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false

    async function loadLatest() {
      try {
        setLoading(true)
        const latestResult = await api.getBreadthLatest(universe)
        if (!cancelled) {
          setLatest(latestResult)
          setSelectedMetric((current) =>
            latestResult.some((item) => item.metricCode === current)
              ? current
              : latestResult[0]?.metricCode || defaultMetrics[0],
          )
          setError(null)
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load breadth')
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    void loadLatest()
    return () => {
      cancelled = true
    }
  }, [universe])

  useEffect(() => {
    if (!selectedMetric) {
      return
    }

    let cancelled = false
    void api.getBreadthHistory(selectedMetric, universe).then((result) => {
      if (!cancelled) {
        setHistory(result)
      }
    })

    return () => {
      cancelled = true
    }
  }, [selectedMetric, universe])

  if (loading) {
    return <LoadingState message="Loading breadth metrics…" />
  }

  if (error) {
    return <ErrorState message={error} />
  }

  const barItems = latest.map((item) => ({
    label: metricLabel(item.metricCode),
    value: Number(item.matchPct.toFixed(1)),
  }))

  const metricOptions = [...new Set([...defaultMetrics, ...latest.map((item) => item.metricCode)])]
  const universes = ['ALL', 'US', 'CA']

  return (
    <div className="page-stack">
      <section className="panel-card">
        <div className="panel-header">
          <h2>Breadth Dashboard</h2>
          <select value={universe} onChange={(e) => setUniverse(e.target.value)}>
            {universes.map((code) => (
              <option key={code} value={code}>
                {code === 'ALL' ? 'All markets' : code}
              </option>
            ))}
          </select>
        </div>
        <BreadthBarChart title={`Latest breadth · ${universe}`} items={barItems} />
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Breadth History</h3>
          <select value={selectedMetric} onChange={(e) => setSelectedMetric(e.target.value)}>
            {metricOptions.map((metric) => (
              <option key={metric} value={metric}>
                {metricLabel(metric)}
              </option>
            ))}
          </select>
        </div>
        <BreadthHistoryChart metricCode={selectedMetric} snapshots={history} />
      </section>
    </div>
  )
}
