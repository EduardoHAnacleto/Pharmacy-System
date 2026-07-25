import { defineStore } from 'pinia'
import api from '@/services/api'
import type { ItemPromotion } from '@/types/itemPromotion'

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

  // createdByUserId / createdByUserName are not sent: the API derives them from
  // the authenticated caller's token.
}

function describeError(err: unknown, fallback: string): string {
  if (typeof err === 'object' && err !== null && 'response' in err) {
    const response = (err as { response?: { data?: unknown } }).response
    if (typeof response?.data === 'string' && response.data.length > 0) {
      return response.data
    }
  }

  return err instanceof Error ? err.message : fallback
}

export const usePromotionsStore = defineStore('promotions', {
  state: () => ({
    promotions: [] as ItemPromotion[],
    loading: false,
    error: null as string | null,
  }),

  getters: {
    activePromotions(state): ItemPromotion[] {
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
      this.error = null
      try {
        const { data } = await api.get<ItemPromotion[]>('/item-promotions/all')
        this.promotions = data
      } catch (err: unknown) {
        this.error = describeError(err, 'Erro ao carregar promoções')
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
      this.error = null

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

      try {
        const { data } = await api.post<ItemPromotion>('/item-promotions', formData, {
          headers: {
            'Content-Type': 'multipart/form-data',
          },
        })

        this.promotions.unshift(data)
        return true
      } catch (err: unknown) {
        this.error = describeError(err, 'Erro ao salvar promoção')
        return false
      }
    },

    /**
     * ==========================
     * DELETE PROMOTION
     * ==========================
     */
    async removePromotion(id: number) {
      this.error = null
      try {
        await api.delete(`/item-promotions/${id}`)
        this.promotions = this.promotions.filter((p) => p.id !== id)
        return true
      } catch (err: unknown) {
        this.error = describeError(err, 'Erro ao remover promoção')
        return false
      }
    },
  },
})
