import api from '@/services/api'

export type AnalyticsEventType =
  | 'promotion_view'
  | 'add_to_cart'
  | 'cart_view'
  | 'checkout_started'
  | 'whatsapp_click'

interface QueuedEvent {
  eventType: AnalyticsEventType
  promotionId?: number
  sessionKey: string
}

const SESSION_STORAGE_KEY = 'analytics-session'
const FLUSH_AT = 10
const FLUSH_AFTER_MS = 5000

let queue: QueuedEvent[] = []
let flushTimer: ReturnType<typeof setTimeout> | null = null

/**
 * Per-visit correlation key.
 *
 * Random, opaque, and held in sessionStorage — so it dies when the tab closes,
 * is never written to a cookie, and is never derived from anything about the
 * visitor. The server discards it once the day is rolled up.
 *
 * It exists for one reason: without correlating two events to the same visit,
 * "how many of the people who saw this added it to a cart" cannot be answered —
 * there would be counters, but no funnel.
 */
function sessionKey(): string {
  const existing = sessionStorage.getItem(SESSION_STORAGE_KEY)
  if (existing) return existing

  const bytes = new Uint8Array(16)
  crypto.getRandomValues(bytes)

  const key = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')

  sessionStorage.setItem(SESSION_STORAGE_KEY, key)

  return key
}

/** Promotions already reported this visit, so scrolling past twice counts once. */
const seenPromotions = new Set<number>()

export function trackPromotionView(promotionId: number) {
  if (seenPromotions.has(promotionId)) return

  seenPromotions.add(promotionId)
  track('promotion_view', promotionId)
}

export function track(eventType: AnalyticsEventType, promotionId?: number) {
  try {
    queue.push({ eventType, promotionId, sessionKey: sessionKey() })
  } catch {
    // sessionStorage can be unavailable in private modes. Telemetry is never
    // worth breaking the storefront over.
    return
  }

  if (queue.length >= FLUSH_AT) {
    void flush()
    return
  }

  // Batched: a scrolling grid would otherwise issue one request per card.
  flushTimer ??= setTimeout(() => {
    flushTimer = null
    void flush()
  }, FLUSH_AFTER_MS)
}

export async function flush(): Promise<void> {
  if (queue.length === 0) return

  const events = queue
  queue = []

  if (flushTimer) {
    clearTimeout(flushTimer)
    flushTimer = null
  }

  try {
    await api.post('/analytics/events', { events })
  } catch {
    // Dropped on purpose rather than retried: stale telemetry is worth less than
    // an unbounded queue, and a failed beacon must not surface to the visitor.
  }
}

/**
 * Flushes what is queued when the page goes away.
 *
 * Uses sendBeacon because a normal request is cancelled during unload — which is
 * exactly when the most interesting event (the WhatsApp handoff) tends to fire.
 */
export function installUnloadFlush() {
  window.addEventListener('pagehide', () => {
    if (queue.length === 0) return

    const payload = JSON.stringify({ events: queue })
    queue = []

    try {
      navigator.sendBeacon(
        '/api/v1/analytics/events',
        new Blob([payload], {
          type: 'application/json',
        }),
      )
    } catch {
      // Nothing further to try at this point in the page's life.
    }
  })
}
