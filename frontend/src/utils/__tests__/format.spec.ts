import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { formatDate, formatMoney, formatPercent } from '@/utils/format'
import { useSettingsStore } from '@/stores/settings'

/**
 * Non-breaking spaces vary between ICU versions, so comparisons normalise
 * whitespace rather than asserting an exact byte sequence — otherwise these tests
 * fail on a Node upgrade for no real reason.
 */
function normalise(value: string): string {
  return value.replace(/\s/g, ' ')
}

describe('format', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  describe('formatMoney', () => {
    it('follows the shop locale and currency', () => {
      const settings = useSettingsStore()
      settings.settings.locale = 'pt-BR'
      settings.settings.currency = 'BRL'

      expect(normalise(formatMoney(1234.5))).toBe('R$ 1.234,50')
    })

    it('formats New Zealand dollars for a New Zealand shop', () => {
      const settings = useSettingsStore()
      settings.settings.locale = 'en-NZ'
      settings.settings.currency = 'NZD'

      expect(normalise(formatMoney(1234.5))).toBe('$1,234.50')
    })

    it('accepts explicit overrides', () => {
      expect(normalise(formatMoney(10, 'en-NZ', 'NZD'))).toBe('$10.00')
    })

    it('degrades instead of throwing on a bad currency code', () => {
      // A misconfigured shop must not get a blank page where its prices go.
      expect(() => formatMoney(10, 'pt-BR', 'not-a-currency')).not.toThrow()
    })
  })

  describe('formatDate', () => {
    it('uses the shop locale', () => {
      const settings = useSettingsStore()
      settings.settings.locale = 'pt-BR'

      expect(formatDate('2026-07-25T00:00:00Z')).toBe('25/07/2026')
    })

    it('renders a placeholder for missing and unparseable values', () => {
      expect(formatDate(null)).toBe('—')
      expect(formatDate('not a date')).toBe('—')
    })
  })

  describe('formatPercent', () => {
    it('renders one decimal place', () => {
      const settings = useSettingsStore()
      settings.settings.locale = 'en-NZ'

      expect(normalise(formatPercent(0.1234))).toBe('12.3%')
    })
  })
})
