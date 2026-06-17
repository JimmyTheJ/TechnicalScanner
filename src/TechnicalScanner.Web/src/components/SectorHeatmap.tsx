import type { SectorHeatmapCell } from '../api/types'
import { formatNumber, formatPercent } from '../utils/format'

interface SectorHeatmapProps {
  cells: SectorHeatmapCell[]
}

export function SectorHeatmap({ cells }: SectorHeatmapProps) {
  if (cells.length === 0) {
    return <p className="muted">No sector data available.</p>
  }

  return (
    <div className="heatmap-grid">
      {cells.map((cell) => (
        <article key={cell.sector} className="heatmap-cell">
          <h4>{cell.sector}</h4>
          <p className="heatmap-meta">{cell.tickerCount} tickers</p>
          <dl className="heatmap-metrics">
            <div>
              <dt>Avg Daily RSI</dt>
              <dd>{formatNumber(cell.avgDailyRsi, 1)}</dd>
            </div>
            <div>
              <dt>Avg Pullback Score</dt>
              <dd>{formatNumber(cell.avgCompositeScore, 1)}</dd>
            </div>
            <div>
              <dt>Below BMSB</dt>
              <dd>{formatPercent(cell.belowBmsbPct)}</dd>
            </div>
            <div>
              <dt>Above 200D SMA</dt>
              <dd>{formatPercent(cell.above200dSmaPct)}</dd>
            </div>
          </dl>
        </article>
      ))}
    </div>
  )
}
