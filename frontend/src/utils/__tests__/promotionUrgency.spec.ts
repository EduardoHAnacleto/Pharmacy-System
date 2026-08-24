import { describe, expect, it } from 'vitest'
import { ENDING_SOON_DAYS, daysUntilEnd, endingSoonIn } from '@/utils/promotionUrgency'

/**
 * Dates are built from local components rather than parsed from strings, so a
 * machine's timezone cannot decide which calendar day a fixture lands on and
 * these assertions mean the same thing in Sao Paulo and in Auckland.
 */
function at(year: number, month: number, day: number, hour = 12, minute = 0): Date {
  return new Date(year, month - 1, day, hour, minute)
}

describe('promotionUrgency', () => {
  describe('daysUntilEnd', () => {
    it('is zero on the last day', () => {
      expect(daysUntilEnd(at(2026, 8, 23, 23), at(2026, 8, 23, 1))).toBe(0)
    })

    it('counts calendar days, not elapsed hours', () => {
      // Eleven hours apart, but a shopper reading this tonight is right to call
      // it tomorrow — rounding the milliseconds would call it today.
      expect(daysUntilEnd(at(2026, 8, 24, 9), at(2026, 8, 23, 22))).toBe(1)
    })

    it('ignores the time of day at both ends', () => {
      const early = daysUntilEnd(at(2026, 8, 30, 0, 1), at(2026, 8, 23, 23, 59))
      const late = daysUntilEnd(at(2026, 8, 30, 23, 59), at(2026, 8, 23, 0, 1))

      expect(early).toBe(7)
      expect(late).toBe(7)
    })

    it('goes negative once the end date has passed', () => {
      expect(daysUntilEnd(at(2026, 8, 20), at(2026, 8, 23))).toBe(-3)
    })

    it('crosses a month boundary', () => {
      expect(daysUntilEnd(at(2026, 9, 2), at(2026, 8, 30))).toBe(3)
    })

    it('reads the shape the API actually returns', () => {
      // dateEnd arrives with no zone designator, exactly as the promotions
      // endpoint serialises it.
      expect(daysUntilEnd('2026-08-25T05:21:56', at(2026, 8, 23))).toBe(2)
    })

    it('is null when there is no usable date', () => {
      // Absent or malformed is a promotion with no deadline to advertise, which
      // must not be mistaken for one ending today.
      expect(daysUntilEnd(null, at(2026, 8, 23))).toBeNull()
      expect(daysUntilEnd(undefined, at(2026, 8, 23))).toBeNull()
      expect(daysUntilEnd('', at(2026, 8, 23))).toBeNull()
      expect(daysUntilEnd('not a date', at(2026, 8, 23))).toBeNull()
    })
  })

  describe('endingSoonIn', () => {
    it('reports the remaining days inside the final week', () => {
      expect(endingSoonIn(at(2026, 8, 23), at(2026, 8, 23))).toBe(0)
      expect(endingSoonIn(at(2026, 8, 24), at(2026, 8, 23))).toBe(1)
      expect(endingSoonIn(at(2026, 8, 30), at(2026, 8, 23))).toBe(ENDING_SOON_DAYS)
    })

    it('is null one day outside the window', () => {
      expect(endingSoonIn(at(2026, 8, 31), at(2026, 8, 23))).toBeNull()
    })

    it('is null once the promotion is over', () => {
      // A grid left open past midnight, or a clock a few minutes out, must not
      // shout about an offer that has already ended.
      expect(endingSoonIn(at(2026, 8, 22), at(2026, 8, 23))).toBeNull()
    })

    it('is null without a date', () => {
      expect(endingSoonIn(null, at(2026, 8, 23))).toBeNull()
    })
  })
})
