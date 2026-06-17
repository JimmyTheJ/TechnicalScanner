export function formatNumber(value?: number | null, digits = 2): string {
  if (value === undefined || value === null || Number.isNaN(value)) {
    return '—'
  }
  return value.toLocaleString(undefined, { maximumFractionDigits: digits })
}

export function formatPercent(value?: number | null, digits = 1): string {
  if (value === undefined || value === null || Number.isNaN(value)) {
    return '—'
  }
  return `${value.toFixed(digits)}%`
}

export function formatMarketCap(value?: number | null): string {
  if (value === undefined || value === null) {
    return '—'
  }
  if (value >= 1_000_000_000_000) {
    return `$${(value / 1_000_000_000_000).toFixed(2)}T`
  }
  if (value >= 1_000_000_000) {
    return `$${(value / 1_000_000_000).toFixed(2)}B`
  }
  if (value >= 1_000_000) {
    return `$${(value / 1_000_000).toFixed(2)}M`
  }
  return `$${value.toLocaleString()}`
}

export function formatDate(value?: string | null): string {
  if (!value) {
    return '—'
  }
  return new Date(value).toLocaleString()
}

export function metricLabel(code: string): string {
  return code
    .replaceAll('_', ' ')
    .replace(/\bPCT\b/g, '%')
    .toLowerCase()
    .replace(/\b\w/g, (c) => c.toUpperCase())
}
