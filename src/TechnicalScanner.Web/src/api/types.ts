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
  profileMatch?: boolean | null
  profileCode?: string
  metrics: ScreenerMetric[]
  matchedSignals: string[]
}

export interface ScreenerMetric {
  code: string
  primaryValue?: number
  secondaryValue?: number
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
  profile?: string
  profileMatchOnly?: boolean
  symbols?: string
  watchlist?: string
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
  medianValue?: number
  averageValue?: number
  universeName?: string
  country?: string
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

export interface IndicatorDefinition {
  id: number
  code: string
  name: string
  description?: string
  indicatorType: string
  timeframe: string
  parametersJson: string
}

export interface IndicatorFieldSchema {
  name: string
  valueType: string
  description: string
}

export interface IndicatorParameterSchema {
  name: string
  valueType: string
  defaultValue?: unknown
  description: string
}

export interface IndicatorTypeCatalogEntry {
  indicatorType: string
  name: string
  description: string
  defaultCodePattern: string
  parameters: IndicatorParameterSchema[]
  fields: IndicatorFieldSchema[]
}

export interface ProfileUniverse {
  country?: string
  includeEtfs: boolean
  minDollarVolume?: number | null
  minHistoryDays?: number | null
}

export interface ProfileIndicatorSpec {
  code: string
  name?: string
  indicatorType?: string
  timeframe?: string
  parameters?: Record<string, unknown>
}

export interface ScoreWeightSpec {
  label: string
  indicatorCode: string
  field: string
  operator: string
  value: unknown
  weight: number
}

export interface ScanProfile {
  id: number
  code: string
  name: string
  description?: string
  universe: ProfileUniverse
  indicators: ProfileIndicatorSpec[]
  ruleJson: string
  scoreWeights: ScoreWeightSpec[]
  isActive: boolean
  missingCalculationCodes: string[]
  updatedUtc: string
}

export interface UpsertScanProfileRequest {
  code: string
  name: string
  description?: string
  universe: ProfileUniverse
  indicators: ProfileIndicatorSpec[]
  ruleJson: string
  scoreWeights: ScoreWeightSpec[]
  isActive: boolean
}

export interface ProfileBacktestHit {
  date: string
  close: number
  forwardReturnPct?: number
  score?: number
}

export interface ProfileBacktestResult {
  profileCode: string
  symbol: string
  matchDays: number
  entryCount: number
  forwardDays: number
  entries: ProfileBacktestHit[]
}

export interface ProfileUniverseBacktestHit {
  symbol: string
  entryDate: string
  close: number
  forwardReturnPct?: number
  score?: number
}

export interface ProfileUniverseBacktest {
  runId: number
  profileCode: string
  status: string
  startedUtc: string
  completedUtc?: string
  forwardDays: number
  fromDate?: string
  toDate?: string
  tickerCount: number
  tickersWithEntries: number
  entryCount: number
  matchDays: number
  medianForwardReturnPct?: number
  meanForwardReturnPct?: number
  errorMessage?: string
  sampleEntries: ProfileUniverseBacktestHit[]
}

export interface IndicatorCalculationResult {
  calculated: number
  codes: string[]
}

export interface PriceCandlePoint {
  candleDate: string
  open?: number
  high?: number
  low?: number
  close: number
  volume?: number
}

export interface TickerSignalHistory {
  scanDateUtc: string
  signalCode: string
  signalName: string
  isMatch: boolean
}

export interface ProfileScoreComponent {
  label: string
  indicatorCode: string
  field: string
  operator: string
  matched: boolean
  weight: number
}

export interface ProfileTickerSnapshot {
  profileCode: string
  symbol: string
  profileMatch: boolean
  score?: number
  components: ProfileScoreComponent[]
  metrics: ScreenerMetric[]
}

export interface SectorHeatmapCell {
  sector: string
  tickerCount: number
  avgDailyRsi?: number
  avgCompositeScore?: number
  belowBmsbPct?: number
  above200dSmaPct?: number
}
