import type { ScreenerRow, SectorHeatmapCell } from '../api/types'

export function buildSectorHeatmap(rows: ScreenerRow[]): SectorHeatmapCell[] {
  const grouped = new Map<string, ScreenerRow[]>()

  for (const row of rows) {
    const sector = row.sector?.trim() || 'Unknown'
    const bucket = grouped.get(sector) ?? []
    bucket.push(row)
    grouped.set(sector, bucket)
  }

  return [...grouped.entries()]
    .map(([sector, sectorRows]) => {
      const dailyRsiValues = sectorRows.map((r) => r.dailyRsi).filter(isNumber)
      const compositeValues = sectorRows.map((r) => r.compositeScore).filter(isNumber)
      const belowBmsb = sectorRows.filter((r) => r.bmsbStatus === 'BelowBand').length
      const above200d = sectorRows.filter((r) => (r.sma200DDistancePct ?? -1) > 0).length

      return {
        sector,
        tickerCount: sectorRows.length,
        avgDailyRsi: average(dailyRsiValues),
        avgCompositeScore: average(compositeValues),
        belowBmsbPct: (belowBmsb / sectorRows.length) * 100,
        above200dSmaPct: (above200d / sectorRows.length) * 100,
      }
    })
    .sort((a, b) => b.tickerCount - a.tickerCount)
}

function isNumber(value: number | undefined): value is number {
  return typeof value === 'number' && !Number.isNaN(value)
}

function average(values: number[]): number | undefined {
  if (values.length === 0) {
    return undefined
  }
  return values.reduce((sum, value) => sum + value, 0) / values.length
}
