import type { ItemPromotion } from '@/types/itemPromotion'
import { endingSoonIn } from '@/utils/promotionUrgency'

/**
 * WHICH PROMOTIONS ARE WORTH TELLING PEOPLE ABOUT
 *
 * The shop already knows which offers are running out — the storefront marks
 * them on the card. This is the same question asked from the other side of the
 * counter: what should the shopkeeper send to their customers today.
 *
 * Selection only. Wording, money formatting and the link belong to the caller,
 * which has the shop's locale and the translations; keeping them out of here is
 * what makes the rule testable without a Pinia instance or an i18n bundle.
 */

export interface BroadcastItem {
  promotion: ItemPromotion
  /** Whole calendar days left, 0 on the last day. */
  daysLeft: number
}

/**
 * Live promotions in their final week, soonest first.
 *
 * Archived and draft rows are excluded even when their dates would qualify: a
 * promotion that is not on the storefront must not be advertised, and status is
 * the only thing that knows the difference.
 */
export function endingSoonForBroadcast(
  promotions: ItemPromotion[],
  now: Date = new Date(),
): BroadcastItem[] {
  return promotions
    .filter((promotion) => promotion.status === 'Active')
    .map((promotion) => ({ promotion, daysLeft: endingSoonIn(promotion.dateEnd, now) }))
    .filter((item): item is BroadcastItem => item.daysLeft !== null)
    .sort((a, b) => a.daysLeft - b.daysLeft || a.promotion.name.localeCompare(b.promotion.name))
}

/**
 * A wa.me link carrying a pre-written message and no recipient.
 *
 * No number on purpose: this is the shopkeeper broadcasting from their own
 * phone, so WhatsApp should open its own chooser and let them pick the contact,
 * group or list. Putting the shop's own number here would open a chat with
 * itself, which is what the storefront's floating button is for.
 */
export function broadcastLink(message: string): string {
  return `https://wa.me/?text=${encodeURIComponent(message)}`
}
