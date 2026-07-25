import api from '@/services/api'
import type { ItemPromotion } from '@/types/itemPromotion'
import type { PagedResult } from '@/types/pagedResult'

function emptyPage(page: number, pageSize: number): PagedResult<ItemPromotion> {
  return { items: [], page, pageSize, totalItems: 0, hasMore: false }
}

export async function getActivePromotionsPaged(
  page: number,
  pageSize: number,
): Promise<PagedResult<ItemPromotion>> {
  // The API filters the promotion window in the visitor's time zone, so the
  // storefront reports it rather than letting the server assume UTC.
  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone

  // Uses the shared axios client instead of raw fetch: one place defines the
  // base URL and attaches auth, and the two styles cannot drift apart.
  const response = await api.get<PagedResult<ItemPromotion>>('/item-promotions/active', {
    params: { page, pageSize, timeZone },
  })

  if (response.status === 204 || !response.data) {
    return emptyPage(page, pageSize)
  }

  return response.data
}

export async function getCategories(): Promise<{ id: number; name: string }[]> {
  const { data } = await api.get<{ id: number; name: string }[]>('/item-promotions/categories/all')

  return data ?? []
}
