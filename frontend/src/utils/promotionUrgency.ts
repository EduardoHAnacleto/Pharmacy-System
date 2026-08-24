/**
 * HOW CLOSE A PROMOTION IS TO ENDING
 *
 * A promotion in its final week is the one a shopper can still act on, so the
 * grid marks it rather than leaving them to compare an end date against today's.
 *
 * Distance is counted in calendar days, not in 24-hour blocks. A promotion
 * ending tomorrow at 09:00, read tonight at 22:00, is eleven hours away — but it
 * is still "tomorrow" to the person reading it, and rounding the elapsed
 * milliseconds would call it today. Both ends are reduced to their year, month
 * and day and compared through Date.UTC, which also sidesteps the hour a
 * daylight-saving change adds to or removes from a local day.
 *
 * The parse deliberately matches formatDate's: dateEnd arrives with no zone
 * designator, so both read it as local time. Agreeing with the date printed on
 * the same card matters more here than being right about the instant — a badge
 * reading "last day" beside a validity line ending two days later is worse than
 * both being off by the same offset.
 */

/** A promotion counts as ending soon from this many days out, inclusive. */
export const ENDING_SOON_DAYS = 7

const MS_PER_DAY = 86_400_000

/**
 * Whole calendar days from `now` until `dateEnd`.
 *
 * Returns 0 on the last day, a negative number once it has passed, and null when
 * there is no usable date — an absent or malformed dateEnd is a promotion with
 * no deadline to advertise, not one ending today.
 */
export function daysUntilEnd(
  dateEnd: string | Date | null | undefined,
  now: Date = new Date(),
): number | null {
  if (!dateEnd) return null

  const end = dateEnd instanceof Date ? dateEnd : new Date(dateEnd)
  if (Number.isNaN(end.getTime())) return null

  const endDay = Date.UTC(end.getFullYear(), end.getMonth(), end.getDate())
  const today = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate())

  return Math.round((endDay - today) / MS_PER_DAY)
}

/**
 * Days remaining, but only while the promotion is inside its final week.
 *
 * Null covers every case with nothing to advertise, so a caller has one thing to
 * check: no deadline, a deadline still far off, or one already past. That last
 * case is the reason this is not a plain `<= ENDING_SOON_DAYS` — the storefront
 * lists only active promotions, but a grid left open past midnight, or a clock a
 * few minutes out, must not shout about an offer that is already over.
 */
export function endingSoonIn(
  dateEnd: string | Date | null | undefined,
  now: Date = new Date(),
): number | null {
  const days = daysUntilEnd(dateEnd, now)

  if (days === null || days < 0 || days > ENDING_SOON_DAYS) return null

  return days
}
