import { describe, expect, it } from 'vitest'
import { broadcastLink, endingSoonForBroadcast } from '@/utils/promotionBroadcast'
import type { ItemPromotion, PromotionStatus } from '@/types/itemPromotion'

function promotion(overrides: Partial<ItemPromotion> = {}): ItemPromotion {
  return {
    id: 1,
    name: 'Dipirona',
    price: 9.9,
    priceBefore: 19.9,
    imageUrl: '/images/promotions/1.png',
    dateStart: '2026-08-01T00:00:00',
    dateEnd: '2026-08-25T12:00:00',
    requiresPrescription: false,
    status: 'Active' as PromotionStatus,
    archivedAt: null,
    imageMissing: false,
    sourcePromotionId: null,
    categoryId: 1,
    productType: 'default',
    createdByUserId: 1,
    createdByUserName: 'admin',
    createdAt: '2026-08-01T00:00:00',
    updatedAt: null,
    ...overrides,
  }
}

/** 24 August 2026, midday, local — the same reading the storefront uses. */
const now = new Date(2026, 7, 24, 12)

describe('promotionBroadcast', () => {
  describe('endingSoonForBroadcast', () => {
    it('keeps only what is inside the final week', () => {
      const selected = endingSoonForBroadcast(
        [
          promotion({ id: 1, name: 'Termina amanhã', dateEnd: '2026-08-25T12:00:00' }),
          promotion({ id: 2, name: 'Termina em um mês', dateEnd: '2026-09-24T12:00:00' }),
        ],
        now,
      )

      expect(selected.map((s) => s.promotion.id)).toEqual([1])
      expect(selected[0]?.daysLeft).toBe(1)
    })

    it('puts the most urgent first', () => {
      const selected = endingSoonForBroadcast(
        [
          promotion({ id: 1, dateEnd: '2026-08-29T12:00:00' }),
          promotion({ id: 2, dateEnd: '2026-08-24T12:00:00' }),
          promotion({ id: 3, dateEnd: '2026-08-26T12:00:00' }),
        ],
        now,
      )

      expect(selected.map((s) => s.daysLeft)).toEqual([0, 2, 5])
    })

    it('breaks a tie by name, so two runs produce the same message', () => {
      const selected = endingSoonForBroadcast(
        [
          promotion({ id: 1, name: 'Omeprazol', dateEnd: '2026-08-26T12:00:00' }),
          promotion({ id: 2, name: 'Amoxicilina', dateEnd: '2026-08-26T12:00:00' }),
        ],
        now,
      )

      expect(selected.map((s) => s.promotion.name)).toEqual(['Amoxicilina', 'Omeprazol'])
    })

    it('never advertises a promotion the storefront is not showing', () => {
      // Dates alone would qualify all three. Status is the only thing that knows
      // an archived or unpublished offer must not be sent to customers.
      const selected = endingSoonForBroadcast(
        [
          promotion({ id: 1, status: 'Archived' }),
          promotion({ id: 2, status: 'Draft' }),
          promotion({ id: 3, status: 'Active' }),
        ],
        now,
      )

      expect(selected.map((s) => s.promotion.id)).toEqual([3])
    })

    it('excludes one that has already ended', () => {
      expect(
        endingSoonForBroadcast([promotion({ dateEnd: '2026-08-23T12:00:00' })], now),
      ).toHaveLength(0)
    })

    it('is empty rather than throwing on an empty catalogue', () => {
      expect(endingSoonForBroadcast([], now)).toEqual([])
    })
  })

  describe('broadcastLink', () => {
    it('carries the message and names no recipient', () => {
      // No number: WhatsApp opens its own chooser so the shopkeeper picks who
      // gets it. A number here would open a chat with the shop itself.
      const link = broadcastLink('Promoções da semana')

      expect(link).toBe('https://wa.me/?text=Promo%C3%A7%C3%B5es%20da%20semana')
    })

    it('escapes what would otherwise break the URL', () => {
      const link = broadcastLink('De R$ 12,90 por R$ 7,49 — 42% off & mais')

      expect(link).not.toContain(' ')
      expect(link).toContain('%26') // &
      expect(decodeURIComponent(link.split('text=')[1] ?? '')).toBe(
        'De R$ 12,90 por R$ 7,49 — 42% off & mais',
      )
    })
  })
})
