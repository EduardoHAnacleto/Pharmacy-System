import { defineStore } from 'pinia'

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

export const useCartStore = defineStore('cart', {
  state: () => ({
    items: [] as CartItem[],
    deliveryType: 'pickup' as DeliveryType,
    deliveryFee: 8,
    minDeliveryTotal: 30,
    expiresAt: 0,
  }),

  getters: {
    itemsCount: (state) => state.items.reduce((sum, item) => sum + item.quantity, 0),

    productsTotal: (state) =>
      state.items.reduce((sum, item) => sum + item.price * item.quantity, 0),

    canDeliver(): boolean {
      return this.productsTotal >= this.minDeliveryTotal
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

      const data = JSON.parse(raw)
      if (Date.now() > data.expiresAt) {
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
