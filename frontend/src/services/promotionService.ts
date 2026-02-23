import api from './api'
import type { ItemPromotion } from '@/types/itemPromotion'
import type { PagedResult } from '@/types/pagedResult'

export default {
  // =========================
  // ACTIVE PAGED (HOME)
  // =========================
  async getActive(page: number, pageSize: number) {
    const { data } = await api.get<PagedResult<ItemPromotion>>(
      `/item-promotions/active?page=${page}&pageSize=${pageSize}`,
    )
    return data
  },

  // =========================
  // GET BY ID
  // =========================
  async getById(id: number) {
    const { data } = await api.get<ItemPromotion>(`/item-promotions/${id}`)
    return data
  },

  // =========================
  // CREATE (ADMIN)
  // =========================
  async create(formData: FormData) {
    const { data } = await api.post<ItemPromotion>('/item-promotions', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    })

    return data
  },

  // =========================
  // DELETE (ADMIN)
  // =========================
  async remove(id: number) {
    await api.delete(`/item-promotions/${id}`)
  },
}
