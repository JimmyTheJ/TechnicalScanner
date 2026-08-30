import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { ScanProfile, ScreenerRow, Watchlist } from '../api/types'
import { ScreenerTable } from '../components/ScreenerTable'
import { ErrorState, LoadingState } from '../components/StateBlocks'
import {
  createId,
  deleteSavedFilter,
  deleteWatchlist,
  loadSavedFilters,
  loadWatchlists,
  saveWatchlist,
} from '../utils/storage'

export function WatchlistsPage() {
  const [watchlists, setWatchlists] = useState(loadWatchlists())
  const [savedFilters, setSavedFilters] = useState(loadSavedFilters())
  const [name, setName] = useState('')
  const [symbols, setSymbols] = useState('')
  const [selectedId, setSelectedId] = useState(watchlists[0]?.id ?? '')
  const [profiles, setProfiles] = useState<ScanProfile[]>([])
  const [profileCode, setProfileCode] = useState('')
  const [rows, setRows] = useState<ScreenerRow[]>([])
  const [loadingRows, setLoadingRows] = useState(false)
  const [rowError, setRowError] = useState<string | null>(null)

  const selected = watchlists.find((list) => list.id === selectedId) ?? watchlists[0] ?? null

  function refresh(nextSelectedId?: string) {
    const lists = loadWatchlists()
    setWatchlists(lists)
    setSavedFilters(loadSavedFilters())
    const preferred = nextSelectedId ?? selectedId
    if (preferred && lists.some((list) => list.id === preferred)) {
      setSelectedId(preferred)
      return
    }
    setSelectedId(lists[0]?.id ?? '')
  }

  function addWatchlist() {
    if (!name.trim()) {
      return
    }

    const watchlist: Watchlist = {
      id: createId(),
      name: name.trim(),
      symbols: symbols
        .split(',')
        .map((symbol) => symbol.trim().toUpperCase())
        .filter(Boolean),
      createdUtc: new Date().toISOString(),
    }

    saveWatchlist(watchlist)
    setName('')
    setSymbols('')
    refresh(watchlist.id)
  }

  function removeWatchlist(id: string) {
    deleteWatchlist(id)
    refresh()
  }

  function removeSavedFilter(id: string) {
    deleteSavedFilter(id)
    refresh()
  }

  useEffect(() => {
    void api
      .getProfiles()
      .then((result) => {
        setProfiles(result)
        setProfileCode((current) => current || result[0]?.code || '')
      })
      .catch(() => setProfiles([]))
  }, [])

  useEffect(() => {
    if (!selected || selected.symbols.length === 0) {
      setRows([])
      setRowError(null)
      setLoadingRows(false)
      return
    }

    let cancelled = false
    setLoadingRows(true)

    void api
      .getScreener({
        symbols: selected.symbols.join(','),
        profile: profileCode || undefined,
        profileMatchOnly: false,
        page: 1,
        pageSize: Math.min(200, Math.max(selected.symbols.length, 1)),
        sort: 'compositeScoreDesc',
      })
      .then((result) => {
        if (!cancelled) {
          setRows(result.items)
          setRowError(null)
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setRows([])
          setRowError(err instanceof Error ? err.message : 'Failed to score watchlist')
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoadingRows(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [selected?.id, selected?.symbols.join(','), profileCode])

  const missing = useMemo(() => {
    if (!selected) {
      return []
    }
    const found = new Set(rows.map((row) => row.symbol.toUpperCase()))
    return selected.symbols.filter((symbol) => !found.has(symbol.toUpperCase()))
  }, [rows, selected])

  const matchCount = rows.filter((row) => row.profileMatch || (row.compositeScore ?? 0) > 0).length

  return (
    <div className="page-stack">
      <section className="panel-card">
        <div className="panel-header">
          <h2>Watchlists</h2>
        </div>
        <p className="muted">
          Overlay a personal universe on any cheapness profile. Names stay in this browser; scoring uses the local
          database.
        </p>
        <div className="filter-grid">
          <label>
            Name
            <input value={name} onChange={(e) => setName(e.target.value)} />
          </label>
          <label>
            Symbols
            <input
              placeholder="AAPL, MSFT, NVDA"
              value={symbols}
              onChange={(e) => setSymbols(e.target.value)}
            />
          </label>
        </div>
        <button type="button" className="button" onClick={addWatchlist}>
          Save Watchlist
        </button>

        <div className="list-grid">
          {watchlists.map((watchlist) => (
            <article key={watchlist.id} className="list-card">
              <div className="panel-header">
                <h3>{watchlist.name}</h3>
                <div className="button-row">
                  <button
                    type="button"
                    className={watchlist.id === selected?.id ? 'button' : 'button secondary'}
                    onClick={() => setSelectedId(watchlist.id)}
                  >
                    Score
                  </button>
                  <Link
                    className="button secondary"
                    to={`/screener?watchlist=${encodeURIComponent(watchlist.id)}&profile=${encodeURIComponent(profileCode)}&profileMatchOnly=false`}
                  >
                    Screener
                  </Link>
                  <button type="button" className="button secondary" onClick={() => removeWatchlist(watchlist.id)}>
                    Delete
                  </button>
                </div>
              </div>
              <p className="muted">{watchlist.symbols.length} symbols</p>
              <div className="chip-row">
                {watchlist.symbols.map((symbol) => (
                  <Link key={symbol} to={`/stocks/${symbol}`} className="chip">
                    {symbol}
                  </Link>
                ))}
              </div>
            </article>
          ))}
        </div>
      </section>

      {selected ? (
        <section className="panel-card">
          <div className="panel-header">
            <h2>{selected.name}</h2>
            <div className="button-row">
              <select value={profileCode} onChange={(e) => setProfileCode(e.target.value)}>
                {profiles.map((profile) => (
                  <option key={profile.code} value={profile.code}>
                    {profile.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <p className="muted">
            {matchCount}/{selected.symbols.length} currently score as cheap
            {profileCode ? ` on ${profileCode}` : ''}.
            {missing.length > 0 ? ` Missing from the local universe: ${missing.join(', ')}.` : ''}
          </p>
          {loadingRows ? <LoadingState message={`Scoring ${selected.name}…`} /> : null}
          {rowError ? <ErrorState message={rowError} /> : null}
          {!loadingRows && !rowError && selected.symbols.length === 0 ? (
            <p className="muted">Add symbols to this list, then it will score against the selected profile.</p>
          ) : null}
          {!loadingRows && !rowError && rows.length > 0 ? <ScreenerTable rows={rows} /> : null}
        </section>
      ) : null}

      <section className="panel-card">
        <div className="panel-header">
          <h2>Saved Screener Filters</h2>
        </div>
        <div className="list-grid">
          {savedFilters.map((filter) => (
            <article key={filter.id} className="list-card">
              <div className="panel-header">
                <h3>{filter.name}</h3>
                <div className="button-row">
                  <Link className="button secondary" to={`/screener?${new URLSearchParams(filter.filters as Record<string, string>).toString()}`}>
                    Open
                  </Link>
                  <button type="button" className="button secondary" onClick={() => removeSavedFilter(filter.id)}>
                    Delete
                  </button>
                </div>
              </div>
              <pre>{JSON.stringify(filter.filters, null, 2)}</pre>
            </article>
          ))}
        </div>
      </section>
    </div>
  )
}
