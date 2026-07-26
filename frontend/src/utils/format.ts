import { useSettingsStore } from '@/stores/settings'

/**
 * Currency, number and date formatting driven by the shop's own locale.
 *
 * Replaces `R$ {{ price.toFixed(2) }}` and the four hardcoded
 * `toLocaleDateString('pt-BR')` calls. A shop in New Zealand gets NZ$ and
 * day/month/year without a code change; the Brazilian pharmacy keeps exactly
 * what it had.
 *
 * `Intl` formatters are memoised because constructing one is comparatively
 * expensive and the promotion grid formats a price per card, per render.
 */
const currencyFormatters = new Map<string, Intl.NumberFormat>()
const dateFormatters = new Map<string, Intl.DateTimeFormat>()

export function formatMoney(value: number, locale?: string, currency?: string): string {
  const settings = resolve(locale, currency)
  const key = `${settings.locale}|${settings.currency}`

  let formatter = currencyFormatters.get(key)

  if (!formatter) {
    formatter = build(settings.locale, settings.currency)
    currencyFormatters.set(key, formatter)
  }

  return formatter.format(value)
}

/**
 * Builds a currency formatter, degrading rather than throwing.
 *
 * An invalid locale or currency code makes the `Intl` constructor throw, and a
 * misconfigured shop must not get a blank page where its prices should be.
 */
function build(locale: string, currency: string): Intl.NumberFormat {
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency })
  } catch {
    return new Intl.NumberFormat(undefined, { minimumFractionDigits: 2 })
  }
}

export function formatDate(value: string | Date | null | undefined, locale?: string): string {
  if (!value) return '—'

  const date = value instanceof Date ? value : new Date(value)

  if (Number.isNaN(date.getTime())) return '—'

  const resolved = resolve(locale).locale

  let formatter = dateFormatters.get(resolved)

  if (!formatter) {
    try {
      formatter = new Intl.DateTimeFormat(resolved, { dateStyle: 'short' })
    } catch {
      formatter = new Intl.DateTimeFormat(undefined, { dateStyle: 'short' })
    }
    dateFormatters.set(resolved, formatter)
  }

  return formatter.format(date)
}

export function formatPercent(rate: number, locale?: string): string {
  const resolved = resolve(locale).locale

  try {
    return new Intl.NumberFormat(resolved, {
      style: 'percent',
      minimumFractionDigits: 1,
      maximumFractionDigits: 1,
    }).format(rate)
  } catch {
    return `${(rate * 100).toFixed(1)}%`
  }
}

/**
 * Reads locale and currency from the settings store, tolerating its absence.
 *
 * Callers outside a component — and unit tests of pure formatting — may run with
 * no active Pinia instance, where asking for the store throws.
 */
function resolve(locale?: string, currency?: string): { locale: string; currency: string } {
  if (locale && currency) return { locale, currency }

  try {
    const settings = useSettingsStore().settings

    return {
      locale: locale ?? settings.locale,
      currency: currency ?? settings.currency,
    }
  } catch {
    return { locale: locale ?? 'pt-BR', currency: currency ?? 'BRL' }
  }
}
