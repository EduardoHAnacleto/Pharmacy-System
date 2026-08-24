import { describe, expect, it } from 'vitest'
import { discountOf } from '@/utils/promotionPricing'

describe('promotionPricing', () => {
  describe('discountOf', () => {
    it('reports percent and money kept', () => {
      // The real catalogue: 12,90 down to 7,49.
      const discount = discountOf(7.49, 12.9)

      expect(discount?.percent).toBe(42)

      // toBeCloseTo, not toBe: 12.9 - 7.49 is not exactly 5.41 in binary
      // floating point. The value is only ever passed to formatMoney, which
      // rounds to the currency's two places, so the tail never reaches a screen.
      expect(discount?.saved).toBeCloseTo(5.41, 2)
    })

    it('rounds the percent to whole numbers', () => {
      // 26,68% — a shopper comparing two boxes is not served by the decimals,
      // and the exact saving is printed in money next to it.
      expect(discountOf(109.9, 149.9)?.percent).toBe(27)
    })

    it('is null without an old price', () => {
      // The API allows a promotion with no previous price, and a card for one
      // must show a plain price rather than a badge.
      expect(discountOf(9.9, null)).toBeNull()
      expect(discountOf(9.9, undefined)).toBeNull()
    })

    it('is null when the old price does not exceed the new one', () => {
      // Equal is not a discount, and higher is a data-entry error. Publishing
      // either as "−0%" or a negative badge would put the mistake on the shelf.
      expect(discountOf(10, 10)).toBeNull()
      expect(discountOf(10, 8)).toBeNull()
    })

    it('is null when the saving rounds away to nothing', () => {
      // 0,4% off — a badge here is noise, not an offer.
      expect(discountOf(99.6, 100)).toBeNull()
    })

    it('is null on values that are not numbers', () => {
      expect(discountOf(Number.NaN, 10)).toBeNull()
      expect(discountOf(10, Number.NaN)).toBeNull()
    })

    it('handles a very large discount', () => {
      expect(discountOf(1, 100)).toEqual({ percent: 99, saved: 99 })
    })
  })
})
