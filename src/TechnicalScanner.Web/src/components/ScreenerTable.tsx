import { Link } from 'react-router-dom'
import type { ScreenerRow } from '../api/types'
import { formatNumber, formatPercent } from '../utils/format'

interface ScreenerTableProps {
  rows: ScreenerRow[]
}

export function ScreenerTable({ rows }: ScreenerTableProps) {
  return (
    <div className="table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            <th>Symbol</th>
            <th>Company</th>
            <th>Country</th>
            <th>Sector</th>
            <th>Close</th>
            <th>BMSB</th>
            <th>D RSI</th>
            <th>W RSI</th>
            <th>200D %</th>
            <th>BB %B</th>
            <th>Score</th>
            <th>Signals</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={`${row.symbol}-${row.exchange}`}>
              <td>
                <Link to={`/stocks/${row.symbol}`} className="symbol-link">
                  {row.symbol}
                </Link>
              </td>
              <td>{row.companyName ?? '—'}</td>
              <td>{row.country}</td>
              <td>{row.sector ?? '—'}</td>
              <td>{formatNumber(row.close)}</td>
              <td>
                <span className={`pill ${row.bmsbStatus?.toLowerCase() ?? ''}`}>{row.bmsbStatus ?? '—'}</span>
              </td>
              <td>{formatNumber(row.dailyRsi, 1)}</td>
              <td>{formatNumber(row.weeklyRsi, 1)}</td>
              <td>{formatPercent(row.sma200DDistancePct)}</td>
              <td>{formatNumber(row.dailyBollingerPercentB, 2)}</td>
              <td>{formatNumber(row.compositeScore, 1)}</td>
              <td className="signals-cell">{row.matchedSignals.join(', ') || '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
