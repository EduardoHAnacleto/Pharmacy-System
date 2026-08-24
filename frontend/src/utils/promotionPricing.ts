/**
 * WHAT A PROMOTION ACTUALLY SAVES
 *
 * The card showed a struck-through price and a new one and left the customer to
 * do the subtraction. "De R$ 12,90 por R$ 7,49" asks for arithmetic; "−42%" asks
 * for nothing, and both numbers it needs were already in the payload. This is
 * presentation, not an API change.
 */

export interface PromotionDiscount {
  /** Whole percent off, rounded. */
  percent: number
  /** Money kept, in the shop's currency. */
  saved: number
}

/**
 * The discount on a promotion, or null when there is nothing to advertise.
 *
 * Null covers three cases the card must not render a badge for, so the caller
 * has one thing to check:
 *
 *   - no old price at all, which the API now permits;
 *   - an old price at or below the new one, which is a data-entry error rather
 *     than an offer, and "−0%" or a negative badge would publish it;
 *   - a saving that rounds to nothing.
 *
 * Percent is rounded to whole numbers: someone choosing between two boxes is not
 * served by 41,9%, and the exact figure is spelled out in money alongside it.
 */
export function discountOf(
  price: number,
  priceBefore: number | null | undefined,
): PromotionDiscount | null {
  if (priceBefore === null || priceBefore === undefined) return null
  if (!Number.isFinite(price) || !Number.isFinite(priceBefore)) return null
  if (priceBefore <= price) return null

  const saved = priceBefore - price
  const percent = Math.round((saved / priceBefore) * 100)

  if (percent < 1) return null

  return { percent, saved }
}
