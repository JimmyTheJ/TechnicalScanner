import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type {
  IndicatorDefinition,
  IndicatorTypeCatalogEntry,
  ProfileIndicatorSpec,
  ProfileUniverseBacktest,
  ScanProfile,
  ScoreWeightSpec,
  UpsertScanProfileRequest,
} from '../api/types'
import { ErrorState, LoadingState } from '../components/StateBlocks'
import { formatDate, formatNumber, formatPercent } from '../utils/format'

interface Condition {
  indicatorCode: string
  field: string
  operator: string
  value: string
}

const operators = [
  '=',
  '!=',
  '<',
  '<=',
  '>',
  '>=',
  'between',
  'not_between',
  'in',
  'not_in',
  'crossed_above',
  'crossed_below',
  'rising',
  'falling',
]

const emptyForm: UpsertScanProfileRequest = {
  code: '',
  name: '',
  description: '',
  universe: { includeEtfs: true },
  indicators: [],
  ruleJson: '{"all":[]}',
  scoreWeights: [],
  isActive: true,
}

export function ProfilesPage() {
  const [profiles, setProfiles] = useState<ScanProfile[]>([])
  const [catalog, setCatalog] = useState<IndicatorTypeCatalogEntry[]>([])
  const [definitions, setDefinitions] = useState<IndicatorDefinition[]>([])
  const [selectedCode, setSelectedCode] = useState<string | null>(null)
  const [form, setForm] = useState<UpsertScanProfileRequest>(emptyForm)
  const [conditions, setConditions] = useState<Condition[]>([])
  const [matchMode, setMatchMode] = useState<'all' | 'any'>('all')
  const [useRawRule, setUseRawRule] = useState(false)
  const [newType, setNewType] = useState('SMA')
  const [newTimeframe, setNewTimeframe] = useState('D')
  const [newPeriod, setNewPeriod] = useState(20)
  const [newStdDev, setNewStdDev] = useState(2)
  const [newBenchmark, setNewBenchmark] = useState('SPY')
  const [existingCode, setExistingCode] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [calculating, setCalculating] = useState(false)
  const [universeBacktest, setUniverseBacktest] = useState<ProfileUniverseBacktest | null>(null)
  const [runningUniverse, setRunningUniverse] = useState(false)

  async function reload() {
    const [profileRows, catalogRows, definitionRows] = await Promise.all([
      api.getProfiles(),
      api.getIndicatorCatalog(),
      api.getIndicatorDefinitions(),
    ])
    setProfiles(profileRows)
    setCatalog(catalogRows)
    setDefinitions(definitionRows)
  }

  useEffect(() => {
    let cancelled = false
    async function load() {
      try {
        setLoading(true)
        await reload()
        if (!cancelled) {
          setError(null)
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Failed to load profiles')
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
  }, [])

  const selectedType = catalog.find((item) => item.indicatorType === newType) ?? catalog[0]
  const fieldsByCode = useMemo(() => buildFieldMap(catalog, definitions, form.indicators), [catalog, definitions, form.indicators])

  function startNew() {
    setSelectedCode(null)
    setForm(emptyForm)
    setConditions([])
    setMatchMode('all')
    setUseRawRule(false)
    setNotice(null)
    setUniverseBacktest(null)
  }

  function loadProfile(profile: ScanProfile) {
    setSelectedCode(profile.code)
    setForm({
      code: profile.code,
      name: profile.name,
      description: profile.description ?? '',
      universe: {
        country: profile.universe?.country,
        includeEtfs: profile.universe?.includeEtfs ?? true,
        minDollarVolume: profile.universe?.minDollarVolume ?? undefined,
        minHistoryDays: profile.universe?.minHistoryDays ?? undefined,
      },
      indicators: profile.indicators ?? [],
      ruleJson: profile.ruleJson,
      scoreWeights: profile.scoreWeights ?? [],
      isActive: profile.isActive,
    })
    const parsed = parseRule(profile.ruleJson)
    setUseRawRule(parsed.raw)
    setMatchMode(parsed.mode)
    setConditions(parsed.conditions)
    setNotice(
      profile.missingCalculationCodes.length
        ? `Needs calculation: ${profile.missingCalculationCodes.join(', ')}.`
        : null,
    )
    void api
      .getUniverseBacktest(profile.code)
      .then(setUniverseBacktest)
      .catch(() => setUniverseBacktest(null))
  }

  function addExistingIndicator() {
    const definition = definitions.find((item) => item.code === existingCode)
    if (!definition || form.indicators.some((item) => item.code === definition.code)) {
      return
    }

    setForm((current) => ({
      ...current,
      indicators: [...current.indicators, { code: definition.code, name: definition.name, indicatorType: definition.indicatorType, timeframe: definition.timeframe }],
    }))
    setExistingCode('')
  }

  function addCatalogIndicator() {
    if (!selectedType) {
      return
    }

    const parameters: Record<string, unknown> = {}
    if (selectedType.parameters.some((item) => item.name === 'period')) {
      parameters.period = newPeriod
    }
    if (selectedType.parameters.some((item) => item.name === 'stdDevMultiplier')) {
      parameters.stdDevMultiplier = newStdDev
    }
    if (selectedType.parameters.some((item) => item.name === 'smaPeriod')) {
      parameters.smaPeriod = 20
    }
    if (selectedType.parameters.some((item) => item.name === 'emaPeriod')) {
      parameters.emaPeriod = 21
    }
    if (selectedType.parameters.some((item) => item.name === 'benchmarkSymbol')) {
      parameters.benchmarkSymbol = newBenchmark.trim().toUpperCase() || 'SPY'
    }

    const code = suggestCode(selectedType.indicatorType, newTimeframe, parameters)
    if (form.indicators.some((item) => item.code === code)) {
      return
    }

    const spec: ProfileIndicatorSpec = {
      code,
      name: suggestName(selectedType.indicatorType, newTimeframe, parameters),
      indicatorType: selectedType.indicatorType,
      timeframe: newTimeframe,
      parameters,
    }
    setForm((current) => ({ ...current, indicators: [...current.indicators, spec] }))
  }

  function removeIndicator(code: string) {
    setForm((current) => ({
      ...current,
      indicators: current.indicators.filter((item) => item.code !== code),
      scoreWeights: current.scoreWeights.filter((item) => item.indicatorCode !== code),
    }))
    setConditions((current) => current.filter((item) => item.indicatorCode !== code))
  }

  async function save() {
    try {
      setSaving(true)
      const ruleJson = useRawRule ? form.ruleJson : serializeRule(matchMode, conditions, fieldsByCode)
      const request: UpsertScanProfileRequest = {
        ...form,
        code: form.code.trim().toUpperCase(),
        ruleJson,
        scoreWeights: form.scoreWeights.map((weight) => ({
          ...weight,
          value: coerceWeightValue(weight, fieldsByCode),
        })),
      }
      const saved = selectedCode
        ? await api.updateProfile(selectedCode, request)
        : await api.createProfile(request)
      await reload()
      loadProfile(saved)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save profile')
    } finally {
      setSaving(false)
    }
  }

  async function calculateMissing() {
    const codes = profiles.find((item) => item.code === selectedCode)?.missingCalculationCodes ?? []
    if (codes.length === 0) {
      return
    }

    try {
      setCalculating(true)
      for (const code of codes) {
        await api.calculateIndicators({ code })
      }
      await reload()
      const refreshed = (await api.getProfiles()).find((item) => item.code === selectedCode)
      if (refreshed) {
        loadProfile(refreshed)
      }
      setNotice(`Calculated ${codes.join(', ')}.`)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to calculate indicators')
    } finally {
      setCalculating(false)
    }
  }

  async function runUniverseBacktest() {
    if (!selectedCode) {
      return
    }

    try {
      setRunningUniverse(true)
      const result = await api.runUniverseBacktest(selectedCode, 20)
      setUniverseBacktest(result)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to run universe backtest')
    } finally {
      setRunningUniverse(false)
    }
  }

  async function deactivate() {
    if (!selectedCode) {
      return
    }
    try {
      await api.deactivateProfile(selectedCode)
      await reload()
      startNew()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete profile')
    }
  }

  if (loading) {
    return <LoadingState message="Loading profiles…" />
  }

  return (
    <div className="page-stack">
      <section className="hero-card">
        <div>
          <p className="eyebrow">Cheapness profiles</p>
          <h2>Define what “cheap” means</h2>
          <p className="muted">
            Stack metrics, save the recipe in the database, then open the screener. New SMA/EMA/volume/ATR/range/ROC
            and relative-strength definitions are created on save.
          </p>
        </div>
        <button type="button" className="button secondary" onClick={startNew}>
          New profile
        </button>
      </section>

      {error ? <ErrorState message={error} /> : null}
      {notice ? <div className="state-card">{notice}</div> : null}

      <section className="panel-grid">
        <div className="panel-card">
          <div className="panel-header">
            <h3>Saved profiles</h3>
          </div>
          <ul className="simple-list">
            {profiles.map((profile) => (
              <li key={profile.code}>
                <button type="button" className="button secondary" onClick={() => loadProfile(profile)}>
                  {profile.name}
                </button>
                <Link to={`/screener?profile=${encodeURIComponent(profile.code)}&profileMatchOnly=true`}>
                  Screen
                </Link>
              </li>
            ))}
          </ul>
        </div>

        <div className="panel-card">
          <div className="panel-header">
            <h3>{selectedCode ? `Edit ${selectedCode}` : 'New profile'}</h3>
            <div className="button-row">
              {selectedCode ? (
                <button type="button" className="button secondary" onClick={() => void deactivate()}>
                  Deactivate
                </button>
              ) : null}
              {selectedCode && (profiles.find((item) => item.code === selectedCode)?.missingCalculationCodes.length ?? 0) > 0 ? (
                <button type="button" className="button secondary" onClick={() => void calculateMissing()} disabled={calculating}>
                  {calculating ? 'Calculating…' : 'Calculate missing'}
                </button>
              ) : null}
              <button type="button" className="button" onClick={() => void save()} disabled={saving}>
                {saving ? 'Saving…' : 'Save'}
              </button>
            </div>
          </div>

          <div className="filter-grid">
            <label>
              Code
              <input
                value={form.code}
                disabled={Boolean(selectedCode)}
                onChange={(e) => setForm((current) => ({ ...current, code: e.target.value }))}
              />
            </label>
            <label>
              Name
              <input value={form.name} onChange={(e) => setForm((current) => ({ ...current, name: e.target.value }))} />
            </label>
            <label>
              Country
              <select
                value={form.universe.country ?? ''}
                onChange={(e) =>
                  setForm((current) => ({
                    ...current,
                    universe: { ...current.universe, country: e.target.value || undefined },
                  }))
                }
              >
                <option value="">All</option>
                <option value="US">US</option>
                <option value="CA">CA</option>
              </select>
            </label>
            <label>
              Include ETFs
              <select
                value={form.universe.includeEtfs ? 'yes' : 'no'}
                onChange={(e) =>
                  setForm((current) => ({
                    ...current,
                    universe: { ...current.universe, includeEtfs: e.target.value === 'yes' },
                  }))
                }
              >
                <option value="yes">Yes</option>
                <option value="no">No</option>
              </select>
            </label>
            <label>
              Min dollar volume
              <input
                type="number"
                value={form.universe.minDollarVolume ?? ''}
                onChange={(e) =>
                  setForm((current) => ({
                    ...current,
                    universe: {
                      ...current.universe,
                      minDollarVolume: e.target.value ? Number(e.target.value) : undefined,
                    },
                  }))
                }
              />
            </label>
            <label>
              Min history (days)
              <input
                type="number"
                value={form.universe.minHistoryDays ?? ''}
                onChange={(e) =>
                  setForm((current) => ({
                    ...current,
                    universe: {
                      ...current.universe,
                      minHistoryDays: e.target.value ? Number(e.target.value) : undefined,
                    },
                  }))
                }
              />
            </label>
          </div>
          <label>
            Description
            <textarea
              rows={2}
              value={form.description ?? ''}
              onChange={(e) => setForm((current) => ({ ...current, description: e.target.value }))}
            />
          </label>
        </div>
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Metrics</h3>
        </div>
        <div className="filter-grid">
          <label>
            Existing definition
            <select value={existingCode} onChange={(e) => setExistingCode(e.target.value)}>
              <option value="">Select…</option>
              {definitions.map((item) => (
                <option key={item.code} value={item.code}>
                  {item.code}
                </option>
              ))}
            </select>
          </label>
          <div className="save-filter-row">
            <button type="button" className="button secondary" onClick={addExistingIndicator}>
              Add existing
            </button>
          </div>
          <label>
            New type
            <select value={newType} onChange={(e) => setNewType(e.target.value)}>
              {catalog.map((item) => (
                <option key={item.indicatorType} value={item.indicatorType}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Timeframe
            <select value={newTimeframe} onChange={(e) => setNewTimeframe(e.target.value)}>
              <option value="D">Daily</option>
              <option value="W">Weekly</option>
              <option value="M">Monthly</option>
            </select>
          </label>
          {selectedType?.parameters.some((item) => item.name === 'period') ? (
            <label>
              Period
              <input type="number" value={newPeriod} onChange={(e) => setNewPeriod(Number(e.target.value))} />
            </label>
          ) : null}
          {selectedType?.parameters.some((item) => item.name === 'stdDevMultiplier') ? (
            <label>
              Std dev
              <input type="number" value={newStdDev} onChange={(e) => setNewStdDev(Number(e.target.value))} />
            </label>
          ) : null}
          {selectedType?.parameters.some((item) => item.name === 'benchmarkSymbol') ? (
            <label>
              Benchmark
              <input
                value={newBenchmark}
                placeholder="SPY"
                onChange={(e) => setNewBenchmark(e.target.value)}
              />
            </label>
          ) : null}
        </div>
        <button type="button" className="button secondary" onClick={addCatalogIndicator}>
          Add metric
        </button>
        <div className="chip-row">
          {form.indicators.map((item) => (
            <button key={item.code} type="button" className="chip" onClick={() => removeIndicator(item.code)}>
              {item.code} ×
            </button>
          ))}
        </div>
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Match rule</h3>
          <label className="inline-toggle">
            <input type="checkbox" checked={useRawRule} onChange={(e) => setUseRawRule(e.target.checked)} />
            Edit JSON
          </label>
        </div>
        {useRawRule ? (
          <textarea
            rows={10}
            value={form.ruleJson}
            onChange={(e) => setForm((current) => ({ ...current, ruleJson: e.target.value }))}
          />
        ) : (
          <>
            <label>
              Combine with
              <select value={matchMode} onChange={(e) => setMatchMode(e.target.value as 'all' | 'any')}>
                <option value="all">All conditions</option>
                <option value="any">Any condition</option>
              </select>
            </label>
            {conditions.map((condition, index) => (
              <div className="rule-row" key={`${condition.indicatorCode}-${index}`}>
                <select
                  value={condition.indicatorCode}
                  onChange={(e) => updateCondition(index, { ...condition, indicatorCode: e.target.value, field: fieldsByCode.get(e.target.value)?.[0]?.name ?? '' })}
                >
                  <option value="">Metric</option>
                  {form.indicators.map((item) => (
                    <option key={item.code} value={item.code}>
                      {item.code}
                    </option>
                  ))}
                </select>
                <select
                  value={condition.field}
                  onChange={(e) => updateCondition(index, { ...condition, field: e.target.value })}
                >
                  {(fieldsByCode.get(condition.indicatorCode) ?? []).map((field) => (
                    <option key={field.name} value={field.name}>
                      {field.name}
                    </option>
                  ))}
                </select>
                <select
                  value={condition.operator}
                  onChange={(e) => updateCondition(index, { ...condition, operator: e.target.value })}
                >
                  {operators.map((op) => (
                    <option key={op} value={op}>
                      {op}
                    </option>
                  ))}
                </select>
                <input
                  value={condition.value}
                  placeholder={valuePlaceholder(condition.operator)}
                  disabled={operatorOmitsValue(condition.operator)}
                  onChange={(e) => updateCondition(index, { ...condition, value: e.target.value })}
                />
                <button type="button" className="button secondary" onClick={() => setConditions((current) => current.filter((_, i) => i !== index))}>
                  Remove
                </button>
              </div>
            ))}
            <button
              type="button"
              className="button secondary"
              onClick={() =>
                setConditions((current) => [
                  ...current,
                  {
                    indicatorCode: form.indicators[0]?.code ?? '',
                    field: fieldsByCode.get(form.indicators[0]?.code ?? '')?.[0]?.name ?? '',
                    operator: '<',
                    value: '0',
                  },
                ])
              }
            >
              Add condition
            </button>
          </>
        )}
      </section>

      <section className="panel-card">
        <div className="panel-header">
          <h3>Score weights</h3>
        </div>
        {form.scoreWeights.map((weight, index) => (
          <div className="rule-row score-row" key={`${weight.label}-${index}`}>
            <input
              placeholder="Label"
              value={weight.label}
              onChange={(e) => updateWeight(index, { ...weight, label: e.target.value })}
            />
            <select
              value={weight.indicatorCode}
              onChange={(e) => updateWeight(index, { ...weight, indicatorCode: e.target.value })}
            >
              {form.indicators.map((item) => (
                <option key={item.code} value={item.code}>
                  {item.code}
                </option>
              ))}
            </select>
            <select value={weight.field} onChange={(e) => updateWeight(index, { ...weight, field: e.target.value })}>
              {(fieldsByCode.get(weight.indicatorCode) ?? []).map((field) => (
                <option key={field.name} value={field.name}>
                  {field.name}
                </option>
              ))}
            </select>
            <select value={weight.operator} onChange={(e) => updateWeight(index, { ...weight, operator: e.target.value })}>
              {operators.map((op) => (
                <option key={op} value={op}>
                  {op}
                </option>
              ))}
            </select>
            <input
              value={displayWeightValue(weight)}
              placeholder={valuePlaceholder(weight.operator)}
              disabled={operatorOmitsValue(weight.operator)}
              onChange={(e) => updateWeight(index, { ...weight, value: e.target.value })}
            />
            <input
              type="number"
              value={weight.weight}
              onChange={(e) => updateWeight(index, { ...weight, weight: Number(e.target.value) })}
            />
            <button
              type="button"
              className="button secondary"
              onClick={() =>
                setForm((current) => ({
                  ...current,
                  scoreWeights: current.scoreWeights.filter((_, i) => i !== index),
                }))
              }
            >
              Remove
            </button>
          </div>
        ))}
        <button
          type="button"
          className="button secondary"
          onClick={() =>
            setForm((current) => ({
              ...current,
              scoreWeights: [
                ...current.scoreWeights,
                {
                  label: 'component',
                  indicatorCode: current.indicators[0]?.code ?? '',
                  field: fieldsByCode.get(current.indicators[0]?.code ?? '')?.[0]?.name ?? '',
                  operator: '<',
                  value: 0,
                  weight: 1,
                } satisfies ScoreWeightSpec,
              ],
            }))
          }
        >
          Add weight
        </button>
      </section>

      {selectedCode ? (
        <section className="panel-card">
          <div className="panel-header">
            <h3>Universe replay</h3>
            <button type="button" className="button secondary" onClick={() => void runUniverseBacktest()} disabled={runningUniverse}>
              {runningUniverse ? 'Replaying…' : 'Replay universe'}
            </button>
          </div>
          {universeBacktest ? (
            <>
              <p className="muted">
                {universeBacktest.status} · {universeBacktest.tickersWithEntries}/{universeBacktest.tickerCount} tickers with
                entries · {universeBacktest.entryCount} entries · {universeBacktest.matchDays} matching days · median{' '}
                {formatPercent(universeBacktest.medianForwardReturnPct)} / mean {formatPercent(universeBacktest.meanForwardReturnPct)}{' '}
                over {universeBacktest.forwardDays}d
                {universeBacktest.completedUtc ? ` · ${formatDate(universeBacktest.completedUtc)}` : ''}
              </p>
              {universeBacktest.sampleEntries.length > 0 ? (
                <div className="table-wrap">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Symbol</th>
                        <th>Entry</th>
                        <th>Close</th>
                        <th>Fwd %</th>
                        <th>Score</th>
                      </tr>
                    </thead>
                    <tbody>
                      {universeBacktest.sampleEntries.map((hit) => (
                        <tr key={`${hit.symbol}-${hit.entryDate}`}>
                          <td>
                            <Link to={`/stocks/${encodeURIComponent(hit.symbol)}`}>{hit.symbol}</Link>
                          </td>
                          <td>{hit.entryDate}</td>
                          <td>{formatNumber(hit.close)}</td>
                          <td>{formatPercent(hit.forwardReturnPct)}</td>
                          <td>{formatNumber(hit.score, 1)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <p className="muted">No entries stored for this run yet.</p>
              )}
            </>
          ) : (
            <p className="muted">No stored universe backtest yet. Replay writes candle-dated entries and forward returns.</p>
          )}
        </section>
      ) : null}
    </div>
  )

  function updateCondition(index: number, next: Condition) {
    setConditions((current) => current.map((item, i) => (i === index ? next : item)))
  }

  function updateWeight(index: number, next: ScoreWeightSpec) {
    setForm((current) => ({
      ...current,
      scoreWeights: current.scoreWeights.map((item, i) => (i === index ? next : item)),
    }))
  }
}

function parseRule(ruleJson: string): { raw: boolean; mode: 'all' | 'any'; conditions: Condition[] } {
  try {
    const parsed = JSON.parse(ruleJson) as Record<string, unknown>
    const mode = Array.isArray(parsed.all) ? 'all' : Array.isArray(parsed.any) ? 'any' : null
    if (!mode) {
      if (typeof parsed.indicatorCode === 'string') {
        return { raw: false, mode: 'all', conditions: [toCondition(parsed)] }
      }
      return { raw: true, mode: 'all', conditions: [] }
    }
    const nodes = (parsed[mode] as unknown[]).map((node) => toCondition(node as Record<string, unknown>))
    if (nodes.some((node) => !node.indicatorCode)) {
      return { raw: true, mode, conditions: [] }
    }
    return { raw: false, mode, conditions: nodes }
  } catch {
    return { raw: true, mode: 'all', conditions: [] }
  }
}

function toCondition(node: Record<string, unknown>): Condition {
  return {
    indicatorCode: String(node.indicatorCode ?? ''),
    field: String(node.field ?? ''),
    operator: String(node.operator ?? '='),
    value: formatRuleValue(node.value),
  }
}

function formatRuleValue(value: unknown): string {
  if (Array.isArray(value)) {
    return value.join(',')
  }
  if (value && typeof value === 'object' && 'min' in value && 'max' in value) {
    const range = value as { min: unknown; max: unknown }
    return `${range.min},${range.max}`
  }
  return String(value ?? '')
}

function serializeRule(
  mode: 'all' | 'any',
  conditions: Condition[],
  fieldsByCode: Map<string, { name: string; valueType: string }[]>,
): string {
  return JSON.stringify({
    [mode]: conditions
      .filter((item) => item.indicatorCode && item.field)
      .map((item) => {
        const base = {
          indicatorCode: item.indicatorCode,
          field: item.field,
          operator: item.operator,
        }
        if (operatorOmitsValue(item.operator)) {
          return base
        }
        return {
          ...base,
          value: coerceRuleValue(
            item.value,
            item.operator,
            fieldsByCode.get(item.indicatorCode)?.find((field) => field.name === item.field)?.valueType,
          ),
        }
      }),
  })
}

function operatorOmitsValue(operator: string): boolean {
  return operator === 'rising' || operator === 'falling'
}

function operatorUsesRange(operator: string): boolean {
  return operator === 'between' || operator === 'not_between'
}

function operatorUsesList(operator: string): boolean {
  return operator === 'in' || operator === 'not_in'
}

function valuePlaceholder(operator: string): string {
  if (operatorOmitsValue(operator)) {
    return 'n/a'
  }
  if (operatorUsesRange(operator)) {
    return 'min,max'
  }
  if (operatorUsesList(operator)) {
    return 'a,b,c'
  }
  return 'value'
}

function displayWeightValue(weight: ScoreWeightSpec): string {
  if (operatorOmitsValue(weight.operator)) {
    return ''
  }
  return formatRuleValue(weight.value)
}

function coerceWeightValue(
  weight: ScoreWeightSpec,
  fieldsByCode: Map<string, { name: string; valueType: string }[]>,
): unknown {
  if (operatorOmitsValue(weight.operator)) {
    return 0
  }
  return coerceRuleValue(
    formatRuleValue(weight.value),
    weight.operator,
    fieldsByCode.get(weight.indicatorCode)?.find((field) => field.name === weight.field)?.valueType,
  )
}

function coerceRuleValue(raw: string, operator: string, valueType?: string): unknown {
  if (operatorUsesRange(operator)) {
    return parseRange(raw)
  }
  if (operatorUsesList(operator)) {
    return parseList(raw)
  }
  return coerceValue(raw, valueType)
}

function parseRange(raw: string): [number, number] | string {
  const parts = raw.split(',').map((part) => part.trim())
  if (parts.length >= 2 && parts.every((part) => part !== '' && !Number.isNaN(Number(part)))) {
    return [Number(parts[0]), Number(parts[1])]
  }
  return raw
}

function parseList(raw: string): string[] {
  return raw
    .split(',')
    .map((part) => part.trim())
    .filter(Boolean)
}

function coerceValue(raw: string, valueType?: string): string | number | boolean {
  if (valueType === 'boolean') {
    return raw === 'true' || raw === 'True'
  }
  if (valueType === 'number' || raw.trim() === '' || !Number.isNaN(Number(raw))) {
    const parsed = Number(raw)
    return Number.isNaN(parsed) ? raw : parsed
  }
  return raw
}

function buildFieldMap(
  catalog: IndicatorTypeCatalogEntry[],
  definitions: IndicatorDefinition[],
  specs: ProfileIndicatorSpec[],
): Map<string, { name: string; valueType: string }[]> {
  const map = new Map<string, { name: string; valueType: string }[]>()
  for (const spec of specs) {
    const definition = definitions.find((item) => item.code === spec.code)
    const type = spec.indicatorType ?? definition?.indicatorType
    const entry = catalog.find((item) => item.indicatorType === type)
    map.set(spec.code, entry?.fields.map((field) => ({ name: field.name, valueType: field.valueType })) ?? [])
  }
  return map
}

function suggestCode(type: string, timeframe: string, parameters: Record<string, unknown>): string {
  const tf = timeframe.toUpperCase()
  const period = Number(parameters.period ?? 0)
  switch (type) {
    case 'VOLUME':
      return `VOL_SMA_${period}_${tf}`
    case 'PRICE_RANGE':
      return `RANGE_${period}_${tf}`
    case 'ROC':
      return `ROC_${period}_${tf}`
    case 'RELATIVE_STRENGTH': {
      const bench = String(parameters.benchmarkSymbol ?? 'SPY')
        .toUpperCase()
        .replace(/[^A-Z0-9]+/g, '_')
        .replace(/^_+|_+$/g, '')
      return `RS_${bench || 'SPY'}_${period}_${tf}`
    }
    case 'BOLLINGER_BANDS':
      return `BB_${period}_${parameters.stdDevMultiplier ?? 2}_${tf}`
    case 'BMSB':
      return `BMSB_${tf}`
    case 'DRAWDOWN':
      return `DRAWDOWN_ATH_${tf}`
    default:
      return `${type}_${period}_${tf}`
  }
}

function suggestName(type: string, timeframe: string, parameters: Record<string, unknown>): string {
  if (type === 'RELATIVE_STRENGTH') {
    return `${parameters.period ?? ''} ${timeframe} vs ${String(parameters.benchmarkSymbol ?? 'SPY')}`.trim()
  }
  if (type === 'ROC') {
    return `${parameters.period ?? ''} ${timeframe} ROC`.trim()
  }
  if (type === 'DRAWDOWN') {
    return `${timeframe} drawdown from ATH`
  }
  return `${type} ${parameters.period ?? ''} ${timeframe}`.trim()
}
