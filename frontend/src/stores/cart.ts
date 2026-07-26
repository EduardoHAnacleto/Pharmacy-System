import { defineStore } from 'pinia'
import { useSettingsStore } from '@/stores/settings'

export interface CartItem {
  id: number
  name: string
  price: number
  imageUrl: string
  quantity: number
}

type DeliveryType = 'pickup' | 'delivery'

const STORAGE_KEY = 'cart-storage'
const EXPIRATION_MS = 24 * 60 * 60 * 1000

interface StoredCart {
  items: CartItem[]
  deliveryType: DeliveryType
  expiresAt: number
}

function isStoredCart(value: unknown): value is StoredCart {
  if (typeof value !== 'object' || value === null) return false

  const candidate = value as Partial<StoredCart>

  return (
    Array.isArray(candidate.items) &&
    typeof candidate.expiresAt === 'number' &&
    (candidate.deliveryType === 'pickup' || candidate.deliveryType === 'delivery')
  )
}

export const useCartStore = defineStore('cart', {
  state: () => ({
    items: [] as CartItem[],
    deliveryType: 'pickup' as DeliveryType,
    expiresAt: 0,
  }),

  getters: {
    itemsCount: (state) => state.items.reduce((sum, item) => sum + item.quantity, 0),

    productsTotal: (state) =>
      state.items.reduce((sum, item) => sum + item.price * item.quantity, 0),

    /**
     * The shop's delivery fee.
     *
     * Read from settings rather than the literal 8 that used to sit in this
     * state: a fee written into the frontend cannot be changed by the shop that
     * charges it, and is meaningless for any other shop.
     */
    deliveryFee(): number {
      return useSettingsStore().settings.deliveryFee
    },

    minDeliveryTotal(): number {
      return useSettingsStore().settings.minDeliveryTotal
    },

    /** Fulfilment options the shop actually offers. */
    deliveryEnabled(): boolean {
      return useSettingsStore().settings.deliveryEnabled
    },

    pickupEnabled(): boolean {
      return useSettingsStore().settings.pickupEnabled
    },

    canDeliver(): boolean {
      return this.deliveryEnabled && this.productsTotal >= this.minDeliveryTotal
    },

    finalTotal(): number {
      if (this.deliveryType === 'delivery' && this.canDeliver) {
        return this.productsTotal + this.deliveryFee
      }
      return this.productsTotal
    },
  },

  actions: {
    loadFromStorage() {
      const raw = localStorage.getItem(STORAGE_KEY)
      if (!raw) return

      // Anything can be in localStorage — a truncated write, a stale shape from
      // an older release, or something a user pasted in. A corrupt cart must not
      // throw on mount and take the whole app down with it.
      let data: unknown
      try {
        data = JSON.parse(raw)
      } catch {
        localStorage.removeItem(STORAGE_KEY)
        return
      }

      if (!isStoredCart(data) || Date.now() > data.expiresAt) {
        localStorage.removeItem(STORAGE_KEY)
        return
      }

      this.items = data.items
      this.deliveryType = data.deliveryType
      this.expiresAt = data.expiresAt
    },

    saveToStorage() {
      const payload = {
        items: this.items,
        deliveryType: this.deliveryType,
        expiresAt: Date.now() + EXPIRATION_MS,
      }
      localStorage.setItem(STORAGE_KEY, JSON.stringify(payload))
    },

    addItem(product: Omit<CartItem, 'quantity'>) {
      const existing = this.items.find((i) => i.id === product.id)
      if (existing) {
        existing.quantity++
      } else {
        this.items.push({ ...product, quantity: 1 })
      }
      this.saveToStorage()
    },

    increaseQuantity(id: number) {
      const item = this.items.find((i) => i.id === id)
      if (item) item.quantity++
      this.saveToStorage()
    },

    decreaseQuantity(id: number) {
      const item = this.items.find((i) => i.id === id)
      if (item && item.quantity > 1) item.quantity--
      this.saveToStorage()
    },

    removeItem(id: number) {
      this.items = this.items.filter((i) => i.id !== id)
      this.saveToStorage()
    },

    setDeliveryType(type: DeliveryType) {
      this.deliveryType = type
      this.saveToStorage()
    },

    clearCart() {
      this.items = []
      this.deliveryType = 'pickup'
    },
  },
})
