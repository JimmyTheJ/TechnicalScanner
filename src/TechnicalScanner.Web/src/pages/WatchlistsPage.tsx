import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { Watchlist } from '../api/types'
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

  function refresh() {
    setWatchlists(loadWatchlists())
    setSavedFilters(loadSavedFilters())
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
    refresh()
  }

  function removeWatchlist(id: string) {
    deleteWatchlist(id)
    refresh()
  }

  function removeSavedFilter(id: string) {
    deleteSavedFilter(id)
    refresh()
  }

  return (
    <div className="page-stack">
      <section className="panel-card">
        <div className="panel-header">
          <h2>Watchlists</h2>
        </div>
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
                <button type="button" className="button secondary" onClick={() => removeWatchlist(watchlist.id)}>
                  Delete
                </button>
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
