import { Link } from 'react-router-dom'
import type { ScreenerRow } from '../api/types'
import { formatNumber, formatPercent } from '../utils/format'

interface ScreenerTableProps {
  rows: ScreenerRow[]
}

export function ScreenerTable({ rows }: ScreenerTableProps) {
  const metricCodes = [...new Set(rows.flatMap((row) => (row.metrics ?? []).map((metric) => metric.code)))]

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
            {metricCodes.length > 0 ? metricCodes.map((code) => <th key={code}>{code}</th>) : (
              <>
                <th>BMSB</th>
                <th>D RSI</th>
                <th>W RSI</th>
                <th>200D %</th>
                <th>BB %B</th>
              </>
            )}
            <th>Score</th>
            <th>Cheap</th>
            <th>Ext</th>
            <th>ATH %</th>
            <th>Match</th>
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
              {metricCodes.length > 0
                ? metricCodes.map((code) => {
                    const metric = (row.metrics ?? []).find((item) => item.code === code)
                    return <td key={code}>{formatNumber(metric?.primaryValue, 2)}</td>
                  })
                : (
                  <>
                    <td>
                      <span className={`pill ${row.bmsbStatus?.toLowerCase() ?? ''}`}>{row.bmsbStatus ?? '—'}</span>
                    </td>
                    <td>{formatNumber(row.dailyRsi, 1)}</td>
                    <td>{formatNumber(row.weeklyRsi, 1)}</td>
                    <td>{formatPercent(row.sma200DDistancePct)}</td>
                    <td>{formatNumber(row.dailyBollingerPercentB, 2)}</td>
                  </>
                )}
              <td>{formatNumber(row.compositeScore, 1)}</td>
              <td>{formatConfluence(row.cheapHits, row.cheapValid, row.cheapPct)}</td>
              <td>{formatConfluence(row.extendedHits, row.extendedValid, row.extendedPct)}</td>
              <td>{formatPercent(row.distanceFromAthPct)}</td>
              <td>{row.profileMatch == null ? '—' : row.profileMatch ? 'Yes' : 'No'}</td>
              <td className="signals-cell">{row.matchedSignals.join(', ') || '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function formatConfluence(hits: number | undefined, valid: number | undefined, pct: number | undefined): string {
  if (!valid) {
    return '—'
  }
  return `${hits ?? 0}/${valid} (${formatNumber(pct, 0)}%)`
}
