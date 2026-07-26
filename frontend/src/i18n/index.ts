import { createI18n } from 'vue-i18n'
import enNZ from './locales/en-NZ'
import ptBR from './locales/pt-BR'

/**
 * Locales the storefront ships with.
 *
 * pt-BR is the fallback because it is the language the existing shop runs in and
 * the one every string was written in first. en-NZ exists so the same build can
 * be sold outside Brazil.
 */
export const SUPPORTED_LOCALES = ['pt-BR', 'en-NZ'] as const

export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number]

export const DEFAULT_LOCALE: SupportedLocale = 'pt-BR'

/**
 * Maps a shop's configured locale onto one we have messages for.
 *
 * Matches on the language subtag, so a shop set to `en-AU` or plain `en` gets
 * English rather than silently falling back to Portuguese.
 */
export function resolveLocale(locale: string | undefined | null): SupportedLocale {
  if (!locale) return DEFAULT_LOCALE

  const exact = SUPPORTED_LOCALES.find((l) => l.toLowerCase() === locale.toLowerCase())
  if (exact) return exact

  const language = locale.split('-')[0]?.toLowerCase()

  return (
    SUPPORTED_LOCALES.find((l) => l.split('-')[0]?.toLowerCase() === language) ?? DEFAULT_LOCALE
  )
}

const i18n = createI18n({
  // Composition API mode. `legacy: false` is what makes `useI18n()` usable in
  // <script setup> and keeps `t` reactive when the locale changes at runtime.
  legacy: false,
  locale: DEFAULT_LOCALE,
  fallbackLocale: DEFAULT_LOCALE,
  messages: {
    'pt-BR': ptBR,
    'en-NZ': enNZ,
  },
})

/**
 * Switches the active locale, and the document's `lang` with it.
 *
 * Called once the shop's settings arrive, so the very first paint is in the
 * fallback locale and everything after it is in the shop's own.
 */
export function setLocale(locale: string | undefined | null) {
  const resolved = resolveLocale(locale)

  i18n.global.locale.value = resolved

  if (typeof document !== 'undefined') {
    document.documentElement.lang = resolved
  }
}

export default i18n
