export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}

export interface TickerListItem {
  id: number
  symbol: string
  exchange: string
  country: string
  companyName?: string
  sector?: string
  industry?: string
  marketCap?: number
  isEtf: boolean
}

export interface ScreenerRow {
  tickerId: number
  symbol: string
  companyName?: string
  exchange: string
  country: string
  sector?: string
  industry?: string
  marketCap?: number
  close?: number
  bmsbStatus?: string
  bmsbDistancePct?: number
  dailyRsi?: number
  weeklyRsi?: number
  monthlyRsi?: number
  sma200DDistancePct?: number
  sma50WDistancePct?: number
  sma100WDistancePct?: number
  sma200WDistancePct?: number
  dailyBollingerPercentB?: number
  weeklyBollingerPercentB?: number
  compositeScore?: number
  matchedSignals: string[]
}

export interface ScreenerFilters {
  country?: string
  exchange?: string
  sector?: string
  industry?: string
  minMarketCap?: number
  maxMarketCap?: number
  signal?: string
  bmsbStatus?: string
  minDailyRsi?: number
  maxDailyRsi?: number
  minWeeklyRsi?: number
  maxWeeklyRsi?: number
  minCompositeScore?: number
  maxCompositeScore?: number
  minBollingerPercentB?: number
  maxBollingerPercentB?: number
  search?: string
  sort?: string
  page?: number
  pageSize?: number
}

export interface BreadthMetric {
  metricCode: string
  totalValidStocks: number
  matchCount: number
  matchPct: number
}

export interface ScannerLatest {
  scanDateUtc?: string
  scanRunId?: number
  scanStatus?: string
  activeTickers: number
  matchedSignals: number
  breadth: BreadthMetric[]
}

export interface BreadthSnapshot {
  id: number
  scanDateUtc: string
  universeName: string
  country?: string
  exchange?: string
  sector?: string
  industry?: string
  metricCode: string
  totalValidStocks: number
  matchCount: number
  matchPct: number
  medianValue?: number
  averageValue?: number
}

export interface SignalDefinition {
  id: number
  code: string
  name: string
  description?: string
}

export interface IndicatorLatest {
  code: string
  name: string
  candleDate: string
  primaryValue?: number
  secondaryValue?: number
  tertiaryValue?: number
  values: Record<string, unknown>
}

export interface IndicatorHistoryPoint {
  candleDate: string
  primaryValue?: number
  secondaryValue?: number
  tertiaryValue?: number
  valuesJson: string
}

export interface SignalMatch {
  tickerId: number
  symbol: string
  exchange: string
  country: string
  signalCode: string
  signalName: string
  score?: number
}

export interface SavedFilter {
  id: string
  name: string
  filters: ScreenerFilters
  createdUtc: string
}

export interface Watchlist {
  id: string
  name: string
  symbols: string[]
  createdUtc: string
}

export interface SectorHeatmapCell {
  sector: string
  tickerCount: number
  avgDailyRsi?: number
  avgCompositeScore?: number
  belowBmsbPct?: number
  above200dSmaPct?: number
}
