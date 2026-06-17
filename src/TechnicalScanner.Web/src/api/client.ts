import type {
  BreadthSnapshot,
  IndicatorHistoryPoint,
  IndicatorLatest,
  PagedResult,
  ScannerLatest,
  ScreenerFilters,
  ScreenerRow,
  SignalDefinition,
  SignalMatch,
  TickerListItem,
} from './types'

const API_BASE = import.meta.env.VITE_API_BASE ?? ''

function buildQuery(params: Record<string, string | number | undefined | null>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      search.set(key, String(value))
    }
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`)
  if (!response.ok) {
    throw new Error(`Request failed: ${response.status} ${response.statusText}`)
  }
  return response.json() as Promise<T>
}

export const api = {
  getScannerLatest: () => getJson<ScannerLatest>('/api/scanner/latest'),

  getScreener: (filters: ScreenerFilters) =>
    getJson<PagedResult<ScreenerRow>>(
      `/api/scanner/screener${buildQuery({
        country: filters.country,
        exchange: filters.exchange,
        sector: filters.sector,
        industry: filters.industry,
        minMarketCap: filters.minMarketCap,
        maxMarketCap: filters.maxMarketCap,
        signal: filters.signal,
        bmsbStatus: filters.bmsbStatus,
        minDailyRsi: filters.minDailyRsi,
        maxDailyRsi: filters.maxDailyRsi,
        minWeeklyRsi: filters.minWeeklyRsi,
        maxWeeklyRsi: filters.maxWeeklyRsi,
        minCompositeScore: filters.minCompositeScore,
        maxCompositeScore: filters.maxCompositeScore,
        minBollingerPercentB: filters.minBollingerPercentB,
        maxBollingerPercentB: filters.maxBollingerPercentB,
        search: filters.search,
        sort: filters.sort ?? 'compositeScoreDesc',
        page: filters.page ?? 1,
        pageSize: filters.pageSize ?? 50,
      })}`,
    ),

  getTickers: (params: {
    country?: string
    exchange?: string
    sector?: string
    search?: string
    page?: number
    pageSize?: number
    sort?: string
  }) =>
    getJson<PagedResult<TickerListItem>>(
      `/api/tickers${buildQuery({
        country: params.country,
        exchange: params.exchange,
        sector: params.sector,
        search: params.search,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 50,
        sort: params.sort ?? 'symbolAsc',
      })}`,
    ),

  getTicker: (symbol: string) => getJson<TickerListItem>(`/api/tickers/${encodeURIComponent(symbol)}`),

  getIndicatorLatest: (ticker: string) =>
    getJson<IndicatorLatest[]>(`/api/indicators/latest${buildQuery({ ticker })}`),

  getIndicatorHistory: (ticker: string, indicatorCode: string) =>
    getJson<IndicatorHistoryPoint[]>(
      `/api/indicators/history${buildQuery({ ticker, indicatorCode })}`,
    ),

  getSignalDefinitions: () => getJson<SignalDefinition[]>('/api/signals/definitions'),

  getSignalMatches: (params: { signal?: string; country?: string; page?: number; pageSize?: number }) =>
    getJson<PagedResult<SignalMatch>>(
      `/api/scanner/signals${buildQuery({
        signal: params.signal,
        country: params.country,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 50,
      })}`,
    ),

  getBreadthLatest: () => getJson<BreadthSnapshot[]>('/api/breadth/latest'),

  getBreadthHistory: (metricCode: string) =>
    getJson<BreadthSnapshot[]>(`/api/breadth/history${buildQuery({ metricCode })}`),
}
