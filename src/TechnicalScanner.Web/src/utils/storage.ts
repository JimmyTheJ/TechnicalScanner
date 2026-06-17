import type { SavedFilter, Watchlist } from '../api/types'

const SAVED_FILTERS_KEY = 'technical-scanner.saved-filters'
const WATCHLISTS_KEY = 'technical-scanner.watchlists'

export function loadSavedFilters(): SavedFilter[] {
  return readJson<SavedFilter[]>(SAVED_FILTERS_KEY, [])
}

export function saveSavedFilter(filter: SavedFilter): void {
  const filters = loadSavedFilters().filter((item) => item.id !== filter.id)
  filters.unshift(filter)
  writeJson(SAVED_FILTERS_KEY, filters)
}

export function deleteSavedFilter(id: string): void {
  writeJson(SAVED_FILTERS_KEY, loadSavedFilters().filter((item) => item.id !== id))
}

export function loadWatchlists(): Watchlist[] {
  return readJson<Watchlist[]>(WATCHLISTS_KEY, [])
}

export function saveWatchlist(watchlist: Watchlist): void {
  const lists = loadWatchlists().filter((item) => item.id !== watchlist.id)
  lists.unshift(watchlist)
  writeJson(WATCHLISTS_KEY, lists)
}

export function deleteWatchlist(id: string): void {
  writeJson(WATCHLISTS_KEY, loadWatchlists().filter((item) => item.id !== id))
}

function readJson<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key)
    return raw ? (JSON.parse(raw) as T) : fallback
  } catch {
    return fallback
  }
}

function writeJson<T>(key: string, value: T): void {
  localStorage.setItem(key, JSON.stringify(value))
}

export function createId(): string {
  return crypto.randomUUID()
}
