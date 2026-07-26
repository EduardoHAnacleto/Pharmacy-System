import { defineStore } from 'pinia'
import { getStoreSettings, updateStoreSettings } from '@/services/storeSettingsService'
import {
  DEFAULT_SETTINGS,
  type OpeningRange,
  type StoreSettings,
  type StoreSettingsUpdate,
} from '@/types/storeSettings'

/**
 * The shop's configuration, loaded once at bootstrap.
 *
 * This store is what generalises the project: branding, contact details,
 * currency, delivery rules, opening hours and which checkout fields to collect
 * all arrive from the API instead of being written into the code. A second shop
 * needs a different row in `store_settings`, not a different build.
 */
export const useSettingsStore = defineStore('settings', {
  state: () => ({
    settings: { ...DEFAULT_SETTINGS } as StoreSettings,
    loaded: false,
    loading: false,
    error: null as string | null,
  }),

  getters: {
    /** Digits-only WhatsApp number, or null when the shop has not set one. */
    whatsAppNumber(): string | null {
      return this.settings.whatsAppNumber || null
    },

    /**
     * Free-form map query, falling back to the postal address.
     *
     * Without a fallback the map would silently point nowhere for a shop that
     * filled in its address but not the map field — which is how the original
     * code ended up embedding the Tower of Pisa.
     */
    mapQuery(): string | null {
      const settings = this.settings
      const explicit = settings.mapQuery?.trim()

      if (explicit) return explicit

      const parts = [settings.address, settings.city].filter(Boolean)

      return parts.length > 0 ? parts.join(', ') : null
    },

    /** Cities the shop delivers to, falling back to its own city. */
    deliveryCities(): string[] {
      const configured = this.settings.deliveryCities.filter((c) => c.trim().length > 0)

      if (configured.length > 0) return configured

      return this.settings.city ? [this.settings.city] : []
    },

    /**
     * Whether the shop is open at `at`, in the shop's own time zone.
     *
     * Time zone conversion goes through `Intl.DateTimeFormat` rather than a fixed
     * offset: the previous code added a hardcoded 180 minutes for São Paulo, which
     * is both wrong for every other shop and wrong for São Paulo whenever the
     * offset changes.
     */
    isOpenAt() {
      return (at: Date, isHoliday = false): boolean => {
        // A holiday has no separate schedule to consult, so treat it as closed
        // rather than guessing a shop keeps normal hours on a public holiday.
        if (isHoliday) return false

        const { weekday, minutes } = localParts(at, this.settings.timeZone)
        const ranges = this.settings.openingHours[String(weekday)] ?? []

        return ranges.some((range) => withinRange(range, minutes))
      }
    },

    /** The shop's ranges for the weekday of `at`, for display. */
    scheduleFor() {
      return (at: Date): OpeningRange[] => {
        const { weekday } = localParts(at, this.settings.timeZone)
        return this.settings.openingHours[String(weekday)] ?? []
      }
    },

    /** True when the shop has published any opening hours at all. */
    hasOpeningHours(): boolean {
      return Object.values(this.settings.openingHours).some((ranges) => ranges.length > 0)
    },
  },

  actions: {
    /**
     * Loads the settings and applies the branding.
     *
     * Never rejects: a storefront that renders with default colours beats one that
     * fails to mount because a configuration request timed out.
     */
    async load() {
      this.loading = true
      this.error = null

      try {
        this.settings = await getStoreSettings()
      } catch {
        this.error = 'Não foi possível carregar as configurações da loja.'
      } finally {
        this.loaded = true
        this.loading = false
        this.applyBranding()
      }
    },

    async save(update: StoreSettingsUpdate) {
      this.settings = await updateStoreSettings(update)
      this.applyBranding()
    },

    /**
     * Publishes the shop's colours as CSS custom properties.
     *
     * Set on the root element so plain CSS picks them up, which keeps theming out
     * of the component tree entirely — no component needs to know a colour is
     * configurable.
     */
    applyBranding() {
      if (typeof document === 'undefined') return

      const root = document.documentElement

      root.style.setProperty('--brand-primary', this.settings.primaryColor)
      root.style.setProperty('--brand-secondary', this.settings.secondaryColor)

      document.title = this.settings.tagline
        ? `${this.settings.storeName} — ${this.settings.tagline}`
        : this.settings.storeName
    },
  },
})

/** Weekday (1 = Monday) and minutes past midnight, in the given time zone. */
function localParts(at: Date, timeZone: string): { weekday: number; minutes: number } {
  let parts: Intl.DateTimeFormatPart[]

  try {
    parts = new Intl.DateTimeFormat('en-GB', {
      timeZone,
      weekday: 'short',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).formatToParts(at)
  } catch {
    // An unknown time zone would otherwise throw on every render. The API
    // validates the value, so this only happens for a hand-edited row.
    return { weekday: isoWeekday(at.getDay()), minutes: at.getHours() * 60 + at.getMinutes() }
  }

  const find = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((p) => p.type === type)?.value ?? ''

  const weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']
  const index = weekdays.indexOf(find('weekday'))

  // Hour 24 is how en-GB renders midnight in hour12: false.
  const hour = Number(find('hour')) % 24
  const minute = Number(find('minute'))

  return {
    weekday: index >= 0 ? index + 1 : isoWeekday(at.getDay()),
    minutes: hour * 60 + minute,
  }
}

/** JavaScript's 0 = Sunday to ISO's 7 = Sunday. */
function isoWeekday(day: number): number {
  return day === 0 ? 7 : day
}

function withinRange(range: OpeningRange, minutes: number): boolean {
  const open = toMinutes(range.open)
  const close = toMinutes(range.close)

  if (open === null || close === null) return false

  return minutes >= open && minutes < close
}

function toMinutes(value: string): number | null {
  const match = /^(\d{1,2}):(\d{2})$/.exec(value)
  if (!match) return null

  return Number(match[1]) * 60 + Number(match[2])
}
