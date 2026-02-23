import { defineStore } from 'pinia'
import api from '@/services/api'

/**
 * DTO  (GET)
 */
export interface Promotion {
  id: number
  name: string
  price: number
  priceBefore: number
  imageUrl: string

  dateStart: string
  dateEnd: string

  isActive: boolean
  categoryId: number
  productType: string

  createdByUserId: number
  createdByUserName: string
}

/**
 * DTO (POST)
 */
export interface PromotionCreatePayload {
  name: string
  price: number
  priceBefore: number
  image: File

  dateStart: string
  dateEnd: string

  isActive: boolean
  categoryId: number
  productType: string

  createdByUserId: number
  createdByUserName: string
}

export const usePromotionsStore = defineStore('promotions', {
  state: () => ({
    promotions: [] as Promotion[],
    loading: false,
  }),

  getters: {
    activePromotions(state): Promotion[] {
      return state.promotions.filter((p) => p.isActive)
    },
  },

  actions: {
    /**
     * ==========================
     * GET ALL PROMOTIONS (ADMIN)
     * ==========================
     */
    async loadPromotions() {
      this.loading = true
      try {
        const { data } = await api.get<Promotion[]>('/item-promotions/all')
        this.promotions = data
      } finally {
        this.loading = false
      }
    },

    /**
     * ==========================
     * CREATE PROMOTION (ADMIN)
     * multipart/form-data
     * ==========================
     */
    async addPromotion(payload: PromotionCreatePayload) {
      const formData = new FormData()

      formData.append('name', payload.name)
      formData.append('price', payload.price.toString())
      formData.append('priceBefore', payload.priceBefore.toString())
      formData.append('image', payload.image)

      formData.append('dateStart', payload.dateStart)
      formData.append('dateEnd', payload.dateEnd)

      formData.append('isActive', String(payload.isActive))
      formData.append('categoryId', payload.categoryId.toString())
      formData.append('productType', payload.productType)

      formData.append('createdByUserId', payload.createdByUserId.toString())
      formData.append('createdByUserName', payload.createdByUserName)

      const { data } = await api.post<Promotion>('/item-promotions', formData, {
        headers: {
          'Content-Type': 'multipart/form-data',
        },
      })

      this.promotions.unshift(data)
    },

    /**
     * ==========================
     * DELETE PROMOTION
     * ==========================
     */
    async removePromotion(id: number) {
      await api.delete(`/item-promotions/${id}`)
      this.promotions = this.promotions.filter((p) => p.id !== id)
    },

    /**
     * ==========================
     * TOGGLE ACTIVE (OPCIONAL)
     * ==========================
     */
    async togglePromotion(id: number) {
      const promo = this.promotions.find((p) => p.id === id)
      if (!promo) return

      const updatedIsActive = !promo.isActive

      await api.put(`/item-promotions/${id}`, {
        isActive: updatedIsActive,
      })

      promo.isActive = updatedIsActive
    },
  },
})
