/** One continuous span the shop is open, as `HH:mm` strings. */
export interface OpeningRange {
  open: string
  close: string
}

/** Weekday number (1 = Monday, 7 = Sunday) to its open/close ranges. */
export type OpeningHours = Record<string, OpeningRange[]>

/**
 * Everything about this shop that used to be hardcoded.
 *
 * Mirrors `StoreSettingsDto` on the API. Every field is public — it is the
 * information printed on the page — so it is fetched without authentication
 * before the first render.
 */
export interface StoreSettings {
  storeName: string
  tagline: string | null
  logoUrl: string | null
  primaryColor: string
  secondaryColor: string

  address: string | null
  city: string | null
  countryCode: string
  timeZone: string
  mapQuery: string | null

  phone: string | null
  whatsAppNumber: string | null

  /**
   * True when the API is overriding the stored number from `Store:WhatsAppNumber`.
   *
   * Read-only: the admin screen shows the field as managed elsewhere rather than
   * letting an operator edit a value that will not take effect.
   */
  whatsAppNumberIsManagedByEnvironment: boolean

  email: string | null
  instagramUrl: string | null
  facebookUrl: string | null

  currency: string
  locale: string
  deliveryEnabled: boolean
  pickupEnabled: boolean
  deliveryFee: number
  minDeliveryTotal: number
  deliveryCities: string[]

  collectTaxId: boolean
  collectPostalCode: boolean

  openingHours: OpeningHours
  footerText: string | null
}

/**
 * The shape the admin form submits.
 *
 * Identical to the read model because the API takes a full replacement — the
 * settings screen edits every field on one form, so there is nothing for a
 * partial update to express.
 */
export type StoreSettingsUpdate = StoreSettings

/**
 * Used until the API responds, and as the fallback when it cannot be reached.
 *
 * A storefront that renders with neutral defaults is worth more than one that
 * shows nothing because a settings request failed, so these values are
 * deliberately generic rather than pharmacy-specific.
 */
export const DEFAULT_SETTINGS: StoreSettings = {
  storeName: 'Loja',
  tagline: null,
  logoUrl: null,
  primaryColor: '#0d6efd',
  secondaryColor: '#198754',

  address: null,
  city: null,
  countryCode: 'BR',
  timeZone: 'America/Sao_Paulo',
  mapQuery: null,

  phone: null,
  whatsAppNumber: null,
  whatsAppNumberIsManagedByEnvironment: false,
  email: null,
  instagramUrl: null,
  facebookUrl: null,

  currency: 'BRL',
  locale: 'pt-BR',
  deliveryEnabled: true,
  pickupEnabled: true,
  deliveryFee: 0,
  minDeliveryTotal: 0,
  deliveryCities: [],

  collectTaxId: true,
  collectPostalCode: true,

  openingHours: {},
  footerText: null,
}
