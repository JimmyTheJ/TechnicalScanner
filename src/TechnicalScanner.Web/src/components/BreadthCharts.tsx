import {
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { BreadthSnapshot } from '../api/types'
import { formatDate, metricLabel } from '../utils/format'

interface BreadthHistoryChartProps {
  snapshots: BreadthSnapshot[]
  metricCode: string
}

export function BreadthHistoryChart({ snapshots, metricCode }: BreadthHistoryChartProps) {
  const data = snapshots.map((snapshot) => ({
    date: formatDate(snapshot.scanDateUtc),
    matchPct: Number(snapshot.matchPct.toFixed(2)),
  }))

  if (data.length === 0) {
    return <p className="muted">No history available for {metricLabel(metricCode)}.</p>
  }

  return (
    <div className="chart-card">
      <h3>{metricLabel(metricCode)}</h3>
      <div className="chart-container">
        <ResponsiveContainer width="100%" height={280}>
          <LineChart data={data}>
            <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.2)" />
            <XAxis dataKey="date" hide />
            <YAxis domain={[0, 100]} tickFormatter={(v) => `${v}%`} />
            <Tooltip formatter={(value) => [`${value ?? 0}%`, 'Match %']} />
            <Line type="monotone" dataKey="matchPct" stroke="#38bdf8" strokeWidth={2} dot={false} />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}

interface BreadthBarChartProps {
  items: { label: string; value: number }[]
  title: string
}

export function BreadthBarChart({ items, title }: BreadthBarChartProps) {
  return (
    <div className="chart-card">
      <h3>{title}</h3>
      <div className="chart-container">
        <ResponsiveContainer width="100%" height={320}>
          <BarChart data={items} layout="vertical" margin={{ left: 24, right: 16 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="rgba(148,163,184,0.2)" />
            <XAxis type="number" domain={[0, 100]} tickFormatter={(v) => `${v}%`} />
            <YAxis type="category" dataKey="label" width={180} />
            <Tooltip formatter={(value) => [`${value ?? 0}%`, 'Match %']} />
            <Bar dataKey="value" fill="#22c55e" radius={[0, 4, 4, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
