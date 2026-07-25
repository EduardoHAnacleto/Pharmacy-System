import { defineStore } from 'pinia'
import api from '@/services/api'
import type { ItemPromotion, PromotionStatus } from '@/types/itemPromotion'
import type { PagedResult } from '@/types/pagedResult'

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

  /** Whether to publish. The concrete status is derived server-side from the window. */
  publish: boolean
  categoryId: number
  productType: string

  // createdByUserId / createdByUserName are not sent: the API derives them from
  // the authenticated caller's token.
}

export interface PromotionUpdatePayload {
  name: string
  price: number
  priceBefore: number
  dateStart: string
  dateEnd: string
  publish: boolean
  categoryId: number
  productType: string
}

export interface ReactivatePayload {
  dateStart: string
  dateEnd: string
  name?: string
  price?: number
  priceBefore?: number
  publish: boolean
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
    archived: [] as ItemPromotion[],
    missingImageCount: 0,
    loading: false,
    error: null as string | null,
  }),

  getters: {
    activePromotions: (state): ItemPromotion[] =>
      state.promotions.filter((p) => p.status === 'Active'),
  },

  actions: {
    /**
     * ==========================
     * GET PROMOTIONS (ADMIN)
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
     * GET ARCHIVED (the reactivation library)
     * ==========================
     */
    async loadArchived(page = 1, pageSize = 24) {
      this.error = null
      try {
        const { data } = await api.get<PagedResult<ItemPromotion>>('/item-promotions', {
          params: { status: 'Archived' as PromotionStatus, page, pageSize },
        })
        this.archived = data.items
      } catch (err: unknown) {
        this.error = describeError(err, 'Erro ao carregar promoções arquivadas')
      }
    },

    async loadMissingImageCount() {
      try {
        const { data } = await api.get<{ count: number }>('/item-promotions/missing-images/count')
        this.missingImageCount = data.count
      } catch {
        // Advisory only; a failure here must not block the admin screen.
        this.missingImageCount = 0
      }
    },

    /**
     * ==========================
     * CREATE (multipart/form-data)
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

      formData.append('publish', String(payload.publish))
      formData.append('categoryId', payload.categoryId.toString())
      formData.append('productType', payload.productType)

      try {
        const { data } = await api.post<ItemPromotion>('/item-promotions', formData, {
          headers: { 'Content-Type': 'multipart/form-data' },
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
     * UPDATE
     * ==========================
     */
    async updatePromotion(id: number, payload: PromotionUpdatePayload) {
      this.error = null
      try {
        const { data } = await api.put<ItemPromotion>(`/item-promotions/${id}`, payload)

        const index = this.promotions.findIndex((p) => p.id === id)
        if (index >= 0) this.promotions[index] = data

        return true
      } catch (err: unknown) {
        this.error = describeError(err, 'Erro ao atualizar promoção')
        return false
      }
    },

    /**
     * ==========================
     * ARCHIVE
     * ==========================
     * Retires a promotion without destroying it. The old Remove deleted the row
     * and its image file, so a finished campaign could never be run again.
     */
    async archivePromotion(id: number) {
      this.error = null
      try {
        await api.patch<ItemPromotion>(`/item-promotions/${id}/archive`)

        this.promotions = this.promotions.filter((p) => p.id !== id)
        return true
      } catch (err: unknown) {
        this.error = describeError(err, 'Erro ao arquivar promoção')
        return false
      }
    },

    /**
     * ==========================
     * REACTIVATE
     * ==========================
     * Runs an archived promotion again under a new window, reusing its image.
     */
    async reactivatePromotion(id: number, payload: ReactivatePayload) {
      this.error = null
      try {
        const { data } = await api.post<ItemPromotion>(`/item-promotions/${id}/reactivate`, payload)

        this.promotions.unshift(data)
        return true
      } catch (err: unknown) {
        this.error = describeError(err, 'Erro ao reativar promoção')
        return false
      }
    },
  },
})
