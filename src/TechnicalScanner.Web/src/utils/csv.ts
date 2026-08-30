import type { ScreenerRow } from '../api/types'

export function exportScreenerToCsv(rows: ScreenerRow[], filename = 'technical-screener.csv'): void {
  const headers = [
    'Symbol',
    'CompanyName',
    'Exchange',
    'Country',
    'Sector',
    'Industry',
    'MarketCap',
    'Close',
    'BmsbStatus',
    'BmsbDistancePct',
    'DailyRsi',
    'WeeklyRsi',
    'MonthlyRsi',
    'Sma200DDistancePct',
    'Sma50WDistancePct',
    'DailyBollingerPercentB',
    'DistanceFromAthPct',
    'CheapHits',
    'CheapValid',
    'CheapPct',
    'ExtendedHits',
    'ExtendedValid',
    'ExtendedPct',
    'Bias',
    'CompositeScore',
    'ProfileMatch',
    'MatchedSignals',
    'Metrics',
  ]

  const lines = [
    headers.join(','),
    ...rows.map((row) =>
      [
        row.symbol,
        row.companyName ?? '',
        row.exchange,
        row.country,
        row.sector ?? '',
        row.industry ?? '',
        row.marketCap ?? '',
        row.close ?? '',
        row.bmsbStatus ?? '',
        row.bmsbDistancePct ?? '',
        row.dailyRsi ?? '',
        row.weeklyRsi ?? '',
        row.monthlyRsi ?? '',
        row.sma200DDistancePct ?? '',
        row.sma50WDistancePct ?? '',
        row.dailyBollingerPercentB ?? '',
        row.distanceFromAthPct ?? '',
        row.cheapHits ?? '',
        row.cheapValid ?? '',
        row.cheapPct ?? '',
        row.extendedHits ?? '',
        row.extendedValid ?? '',
        row.extendedPct ?? '',
        row.bias ?? '',
        row.compositeScore ?? '',
        row.profileMatch ?? '',
        row.matchedSignals.join('|'),
        (row.metrics ?? []).map((metric) => `${metric.code}:${metric.primaryValue ?? ''}`).join('|'),
      ]
        .map(csvEscape)
        .join(','),
    ),
  ]

  const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()
  URL.revokeObjectURL(url)
}

function csvEscape(value: string | number | boolean): string {
  const text = String(value)
  if (text.includes(',') || text.includes('"') || text.includes('\n')) {
    return `"${text.replaceAll('"', '""')}"`
  }
  return text
}
