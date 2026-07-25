import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useSettingsStore } from '@/stores/settings'
import * as service from '@/services/storeSettingsService'
import { DEFAULT_SETTINGS, type StoreSettings } from '@/types/storeSettings'

function shop(overrides: Partial<StoreSettings> = {}): StoreSettings {
  return { ...DEFAULT_SETTINGS, ...overrides }
}

describe('settings store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  describe('load', () => {
    it('keeps the storefront usable when the API fails', async () => {
      vi.spyOn(service, 'getStoreSettings').mockRejectedValue(new Error('offline'))

      const settings = useSettingsStore()
      await settings.load()

      // Defaults rather than a rejected promise: a settings outage must cost the
      // branding, not the page.
      expect(settings.error).not.toBeNull()
      expect(settings.loaded).toBe(true)
      expect(settings.settings.storeName).toBe(DEFAULT_SETTINGS.storeName)
    })

    it('publishes the shop colours as CSS custom properties', async () => {
      vi.spyOn(service, 'getStoreSettings').mockResolvedValue(
        shop({ storeName: 'Padaria Central', primaryColor: '#ff0000', tagline: 'Pão quente' }),
      )

      const settings = useSettingsStore()
      await settings.load()

      expect(document.documentElement.style.getPropertyValue('--brand-primary')).toBe('#ff0000')
      expect(document.title).toBe('Padaria Central — Pão quente')
    })
  })

  describe('mapQuery', () => {
    it('falls back to the postal address', () => {
      const settings = useSettingsStore()
      settings.settings = shop({ address: 'Rua 1, 100', city: 'Foz do Iguaçu' })

      // The original markup embedded a fixed map URL pointing at the Tower of
      // Pisa; deriving it from the address is what makes that impossible.
      expect(settings.mapQuery).toBe('Rua 1, 100, Foz do Iguaçu')
    })

    it('prefers an explicit query', () => {
      const settings = useSettingsStore()
      settings.settings = shop({ address: 'Rua 1', mapQuery: 'Farmácia Central Foz' })

      expect(settings.mapQuery).toBe('Farmácia Central Foz')
    })

    it('is null when the shop has no location at all', () => {
      const settings = useSettingsStore()

      expect(settings.mapQuery).toBeNull()
    })
  })

  describe('deliveryCities', () => {
    it('falls back to the shop city', () => {
      const settings = useSettingsStore()
      settings.settings = shop({ city: 'Wellington', deliveryCities: [] })

      expect(settings.deliveryCities).toEqual(['Wellington'])
    })
  })

  describe('isOpenAt', () => {
    beforeEach(() => {
      const settings = useSettingsStore()

      settings.settings = shop({
        timeZone: 'America/Sao_Paulo',
        openingHours: {
          // Monday: split shift, closed for lunch.
          '1': [
            { open: '08:00', close: '12:00' },
            { open: '13:00', close: '18:00' },
          ],
          '6': [{ open: '08:00', close: '12:00' }],
        },
      })
    })

    it('is open inside a range', () => {
      const settings = useSettingsStore()

      // Monday 2026-07-27, 09:00 in São Paulo is 12:00 UTC.
      expect(settings.isOpenAt(new Date('2026-07-27T12:00:00Z'))).toBe(true)
    })

    it('is closed during the lunch gap', () => {
      const settings = useSettingsStore()

      // 12:30 local. A single open/close pair could not express this.
      expect(settings.isOpenAt(new Date('2026-07-27T15:30:00Z'))).toBe(false)
    })

    it('treats the closing time as exclusive', () => {
      const settings = useSettingsStore()

      // Exactly 18:00 local.
      expect(settings.isOpenAt(new Date('2026-07-27T21:00:00Z'))).toBe(false)
    })

    it('is closed on a day with no ranges', () => {
      const settings = useSettingsStore()

      // Sunday.
      expect(settings.isOpenAt(new Date('2026-07-26T12:00:00Z'))).toBe(false)
    })

    it('is closed on a public holiday', () => {
      const settings = useSettingsStore()

      expect(settings.isOpenAt(new Date('2026-07-27T12:00:00Z'), true)).toBe(false)
    })

    it('reads the clock in the shop time zone, not the visitor one', () => {
      const settings = useSettingsStore()
      settings.settings = shop({
        timeZone: 'Pacific/Auckland',
        openingHours: { '1': [{ open: '09:00', close: '17:00' }] },
      })

      // 22:00 UTC Sunday is Monday 10:00 in Auckland — open, even though by UTC
      // it is neither Monday nor business hours.
      expect(settings.isOpenAt(new Date('2026-07-26T22:00:00Z'))).toBe(true)
    })

    it('does not throw on an unusable time zone', () => {
      const settings = useSettingsStore()
      settings.settings = shop({
        timeZone: 'Not/AZone',
        openingHours: { '1': [{ open: '00:00', close: '23:59' }] },
      })

      expect(() => settings.isOpenAt(new Date('2026-07-27T12:00:00Z'))).not.toThrow()
    })
  })

  describe('hasOpeningHours', () => {
    it('is false when every day is empty', () => {
      const settings = useSettingsStore()
      settings.settings = shop({ openingHours: { '1': [], '2': [] } })

      expect(settings.hasOpeningHours).toBe(false)
    })
  })
})
