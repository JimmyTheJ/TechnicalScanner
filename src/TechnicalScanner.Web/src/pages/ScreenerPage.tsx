import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../api/client'
import type { PagedResult, ScreenerFilters, ScreenerRow, SignalDefinition } from '../api/types'
import { ScreenerTable } from '../components/ScreenerTable'
import { ErrorState, LoadingState } from '../components/StateBlocks'
import { exportScreenerToCsv } from '../utils/csv'
import { createId, loadSavedFilters, saveSavedFilter } from '../utils/storage'

const defaultFilters: ScreenerFilters = {
  sort: 'compositeScoreDesc',
  page: 1,
  pageSize: 50,
}

export function ScreenerPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [filters, setFilters] = useState<ScreenerFilters>(() => readFilters(searchParams))
  const [result, setResult] = useState<PagedResult<ScreenerRow> | null>(null)
  const [signals, setSignals] = useState<SignalDefinition[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [saveName, setSaveName] = useState('')

  useEffect(() => {
    void api.getSignalDefinitions().then(setSignals).catch(() => setSignals([]))
  }, [])

  useEffect(() => {
    let cancelled = false

    async function load() {
      try {
        setLoading(true)
        const screenerResult = await api.getScreener(filters)
        if (!cancelled) {
          setResult(screenerResult)
          setError(null)
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load screener')
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [filters])

  useEffect(() => {
    setSearchParams(filtersToParams(filters), { replace: true })
  }, [filters, setSearchParams])

  function updateFilter<K extends keyof ScreenerFilters>(key: K, value: ScreenerFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value, page: 1 }))
  }

  function applySavedFilter(id: string) {
    const saved = loadSavedFilters().find((item) => item.id === id)
    if (saved) {
      setFilters({ ...defaultFilters, ...saved.filters, page: 1 })
    }
  }

  function saveCurrentFilter() {
    if (!saveName.trim()) {
      return
    }

    saveSavedFilter({
      id: createId(),
      name: saveName.trim(),
      filters,
      createdUtc: new Date().toISOString(),
    })
    setSaveName('')
  }

  return (
    <div className="page-stack">
      <section className="panel-card">
        <div className="panel-header">
          <h2>Technical Screener</h2>
          <div className="button-row">
            <button type="button" className="button secondary" onClick={() => result && exportScreenerToCsv(result.items)}>
              Export CSV
            </button>
          </div>
        </div>

        <div className="filter-grid">
          <label>
            Search
            <input value={filters.search ?? ''} onChange={(e) => updateFilter('search', e.target.value)} />
          </label>
          <label>
            Country
            <select value={filters.country ?? ''} onChange={(e) => updateFilter('country', e.target.value || undefined)}>
              <option value="">All</option>
              <option value="US">US</option>
              <option value="CA">CA</option>
            </select>
          </label>
          <label>
            Signal
            <select value={filters.signal ?? ''} onChange={(e) => updateFilter('signal', e.target.value || undefined)}>
              <option value="">All</option>
              {signals.map((signal) => (
                <option key={signal.code} value={signal.code}>
                  {signal.code}
                </option>
              ))}
            </select>
          </label>
          <label>
            BMSB Status
            <select
              value={filters.bmsbStatus ?? ''}
              onChange={(e) => updateFilter('bmsbStatus', e.target.value || undefined)}
            >
              <option value="">All</option>
              <option value="BelowBand">Below Band</option>
              <option value="InsideBand">Inside Band</option>
              <option value="AboveBand">Above Band</option>
            </select>
          </label>
          <label>
            Min Composite Score
            <input
              type="number"
              value={filters.minCompositeScore ?? ''}
              onChange={(e) => updateFilter('minCompositeScore', parseOptionalNumber(e.target.value))}
            />
          </label>
          <label>
            Max Daily RSI
            <input
              type="number"
              value={filters.maxDailyRsi ?? ''}
              onChange={(e) => updateFilter('maxDailyRsi', parseOptionalNumber(e.target.value))}
            />
          </label>
          <label>
            Sort
            <select value={filters.sort ?? 'compositeScoreDesc'} onChange={(e) => updateFilter('sort', e.target.value)}>
              <option value="compositeScoreDesc">Composite Score Desc</option>
              <option value="compositeScoreAsc">Composite Score Asc</option>
              <option value="dailyRsiAsc">Daily RSI Asc</option>
              <option value="dailyRsiDesc">Daily RSI Desc</option>
              <option value="symbolAsc">Symbol Asc</option>
              <option value="marketCapDesc">Market Cap Desc</option>
            </select>
          </label>
        </div>

        <div className="save-filter-row">
          <select onChange={(e) => e.target.value && applySavedFilter(e.target.value)} defaultValue="">
            <option value="">Load saved filter…</option>
            {loadSavedFilters().map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
          <input
            placeholder="Save current filter as…"
            value={saveName}
            onChange={(e) => setSaveName(e.target.value)}
          />
          <button type="button" className="button secondary" onClick={saveCurrentFilter}>
            Save Filter
          </button>
        </div>
      </section>

      {loading ? <LoadingState message="Running screener…" /> : null}
      {error ? <ErrorState message={error} /> : null}

      {!loading && !error && result ? (
        <>
          <div className="results-meta">
            <span>
              Showing {result.items.length} of {result.totalCount}
            </span>
            <div className="button-row">
              <button
                type="button"
                className="button secondary"
                disabled={!result.hasPreviousPage}
                onClick={() => setFilters((current) => ({ ...current, page: (current.page ?? 1) - 1 }))}
              >
                Previous
              </button>
              <span>
                Page {result.page} / {result.totalPages || 1}
              </span>
              <button
                type="button"
                className="button secondary"
                disabled={!result.hasNextPage}
                onClick={() => setFilters((current) => ({ ...current, page: (current.page ?? 1) + 1 }))}
              >
                Next
              </button>
            </div>
          </div>
          <ScreenerTable rows={result.items} />
        </>
      ) : null}
    </div>
  )
}

function parseOptionalNumber(value: string): number | undefined {
  if (!value) {
    return undefined
  }
  const parsed = Number(value)
  return Number.isNaN(parsed) ? undefined : parsed
}

function readFilters(params: URLSearchParams): ScreenerFilters {
  return {
    ...defaultFilters,
    country: params.get('country') ?? undefined,
    exchange: params.get('exchange') ?? undefined,
    sector: params.get('sector') ?? undefined,
    signal: params.get('signal') ?? undefined,
    bmsbStatus: params.get('bmsbStatus') ?? undefined,
    search: params.get('search') ?? undefined,
    sort: params.get('sort') ?? 'compositeScoreDesc',
    page: Number(params.get('page') ?? 1),
    pageSize: Number(params.get('pageSize') ?? 50),
    minCompositeScore: parseOptionalNumber(params.get('minCompositeScore') ?? ''),
    maxDailyRsi: parseOptionalNumber(params.get('maxDailyRsi') ?? ''),
  }
}

function filtersToParams(filters: ScreenerFilters): URLSearchParams {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined && value !== null && value !== '') {
      params.set(key, String(value))
    }
  }
  return params
}
