import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCartStore } from '@/stores/cart'

const STORAGE_KEY = 'cart-storage'

function item(overrides: Partial<{ id: number; name: string; price: number }> = {}) {
  return {
    id: 1,
    name: 'Dipirona',
    price: 10,
    imageUrl: '/images/promotions/a.png',
    ...overrides,
  }
}

describe('cart store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  describe('totals', () => {
    it('sums quantity times price', () => {
      const cart = useCartStore()

      cart.addItem(item({ id: 1, price: 10 }))
      cart.addItem(item({ id: 1, price: 10 }))
      cart.addItem(item({ id: 2, price: 5.5 }))

      expect(cart.itemsCount).toBe(3)
      expect(cart.productsTotal).toBe(25.5)
    })

    it('adds the delivery fee only for delivery orders above the minimum', () => {
      const cart = useCartStore()

      cart.addItem(item({ price: 40 }))

      cart.deliveryType = 'pickup'
      expect(cart.finalTotal).toBe(40)

      cart.deliveryType = 'delivery'
      expect(cart.finalTotal).toBe(48)
    })

    it('does not charge delivery when the order is below the minimum', () => {
      const cart = useCartStore()

      // R$30 minimum, R$8 fee.
      cart.addItem(item({ price: 10 }))
      cart.deliveryType = 'delivery'

      expect(cart.canDeliver).toBe(false)
      // Charging the fee on an order that cannot be delivered would overcharge.
      expect(cart.finalTotal).toBe(10)
    })

    it('treats the minimum as inclusive', () => {
      const cart = useCartStore()

      cart.addItem(item({ price: 30 }))
      cart.deliveryType = 'delivery'

      expect(cart.canDeliver).toBe(true)
      expect(cart.finalTotal).toBe(38)
    })

    it('reports zero for an empty cart', () => {
      const cart = useCartStore()

      expect(cart.itemsCount).toBe(0)
      expect(cart.productsTotal).toBe(0)
      expect(cart.finalTotal).toBe(0)
    })
  })

  describe('persistence', () => {
    it('round-trips through localStorage', () => {
      const cart = useCartStore()
      cart.addItem(item({ id: 3, name: 'Paracetamol', price: 12 }))
      cart.deliveryType = 'delivery'
      cart.saveToStorage()

      setActivePinia(createPinia())
      const restored = useCartStore()
      restored.loadFromStorage()

      expect(restored.items).toHaveLength(1)
      expect(restored.items[0]!.name).toBe('Paracetamol')
      expect(restored.deliveryType).toBe('delivery')
    })

    it('discards a cart past its expiry', () => {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          items: [item()],
          deliveryType: 'pickup',
          expiresAt: Date.now() - 1000,
        }),
      )

      const cart = useCartStore()
      cart.loadFromStorage()

      expect(cart.items).toHaveLength(0)
      expect(localStorage.getItem(STORAGE_KEY)).toBeNull()
    })

    it('survives corrupt JSON instead of throwing on mount', () => {
      // A truncated write used to throw here and take the whole app down.
      localStorage.setItem(STORAGE_KEY, '{"items":[{"id":1,')

      const cart = useCartStore()

      expect(() => cart.loadFromStorage()).not.toThrow()
      expect(cart.items).toHaveLength(0)
      expect(localStorage.getItem(STORAGE_KEY)).toBeNull()
    })

    it('rejects a stored shape from an older release', () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({ products: [], expires: 1 }))

      const cart = useCartStore()
      cart.loadFromStorage()

      expect(cart.items).toHaveLength(0)
    })

    it('rejects a valid-JSON payload that is not an object', () => {
      localStorage.setItem(STORAGE_KEY, '"just a string"')

      const cart = useCartStore()

      expect(() => cart.loadFromStorage()).not.toThrow()
      expect(cart.items).toHaveLength(0)
    })

    it('sets an expiry roughly 24 hours out', () => {
      vi.useFakeTimers()
      const now = new Date('2026-07-25T12:00:00Z')
      vi.setSystemTime(now)

      const cart = useCartStore()
      cart.addItem(item())
      cart.saveToStorage()

      const stored = JSON.parse(localStorage.getItem(STORAGE_KEY)!)
      expect(stored.expiresAt).toBe(now.getTime() + 24 * 60 * 60 * 1000)

      vi.useRealTimers()
    })
  })
})
