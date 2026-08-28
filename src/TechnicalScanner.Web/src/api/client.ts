import type {
  BreadthSnapshot,
  IndicatorHistoryPoint,
  IndicatorLatest,
  IndicatorDefinition,
  IndicatorTypeCatalogEntry,
  PagedResult,
  ProfileBacktestResult,
  ProfileUniverseBacktest,
  ScanProfile,
  ScannerLatest,
  ScreenerFilters,
  ScreenerRow,
  SignalDefinition,
  SignalMatch,
  TickerListItem,
  UpsertScanProfileRequest,
} from './types'

const API_BASE = import.meta.env.VITE_API_BASE ?? ''

function buildQuery(params: Record<string, string | number | boolean | undefined | null>): string {
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
    throw new Error(await readError(response))
  }
  return response.json() as Promise<T>
}

async function sendJson<T>(path: string, method: string, body?: unknown): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    throw new Error(await readError(response))
  }
  if (response.status === 204) {
    return undefined as T
  }
  return response.json() as Promise<T>
}

async function readError(response: Response): Promise<string> {
  const text = await response.text()
  try {
    const parsed = JSON.parse(text) as { error?: string }
    if (parsed.error) {
      return parsed.error
    }
  } catch {
    // use raw text
  }
  return text || `Request failed: ${response.status} ${response.statusText}`
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
        profile: filters.profile,
        profileMatchOnly: filters.profile ? Boolean(filters.profileMatchOnly) : undefined,
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

  getIndicatorDefinitions: () => getJson<IndicatorDefinition[]>('/api/indicators/definitions'),

  getIndicatorCatalog: () => getJson<IndicatorTypeCatalogEntry[]>('/api/indicators/catalog'),

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

  getProfiles: () => getJson<ScanProfile[]>('/api/profiles'),

  getProfile: (code: string) => getJson<ScanProfile>(`/api/profiles/${encodeURIComponent(code)}`),

  createProfile: (request: UpsertScanProfileRequest) =>
    sendJson<ScanProfile>('/api/profiles', 'POST', request),

  updateProfile: (code: string, request: UpsertScanProfileRequest) =>
    sendJson<ScanProfile>(`/api/profiles/${encodeURIComponent(code)}`, 'PUT', request),

  deactivateProfile: (code: string) =>
    sendJson<ScanProfile>(`/api/profiles/${encodeURIComponent(code)}`, 'DELETE'),

  backtestProfile: (code: string, ticker: string, forwardDays = 20) =>
    getJson<ProfileBacktestResult>(
      `/api/profiles/${encodeURIComponent(code)}/backtest${buildQuery({ ticker, forwardDays })}`,
    ),

  getUniverseBacktest: (code: string) =>
    getJson<ProfileUniverseBacktest>(`/api/profiles/${encodeURIComponent(code)}/universe-backtest`),

  runUniverseBacktest: (code: string, forwardDays = 20) =>
    sendJson<ProfileUniverseBacktest>(
      `/api/profiles/${encodeURIComponent(code)}/universe-backtest${buildQuery({ forwardDays })}`,
      'POST',
    ),

  calculateIndicators: (params: { code?: string; pending?: boolean }) =>
    sendJson<{ calculated: number; codes: string[] }>(
      `/api/indicators/calculate${buildQuery({ code: params.code, pending: params.pending })}`,
      'POST',
    ),

  getBreadthLatest: () => getJson<BreadthSnapshot[]>('/api/breadth/latest'),

  getBreadthHistory: (metricCode: string) =>
    getJson<BreadthSnapshot[]>(`/api/breadth/history${buildQuery({ metricCode })}`),
}
