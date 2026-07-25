import api from '@/services/api'
import type { ItemPromotion } from '@/types/itemPromotion'
import type { PagedResult } from '@/types/pagedResult'

/** Sort orders the API recognises. Anything else falls back to "ending soon". */
export type PromotionSort = 'endingSoon' | 'priceAsc' | 'priceDesc' | 'newest' | 'name'

export interface PromotionFilter {
  search?: string
  categoryId?: number | null
  minPrice?: number | null
  maxPrice?: number | null
  sort?: PromotionSort
}

export interface Category {
  id: number
  name: string
}

function emptyPage(page: number, pageSize: number): PagedResult<ItemPromotion> {
  return { items: [], page, pageSize, totalItems: 0, hasMore: false }
}

export async function getActivePromotionsPaged(
  page: number,
  pageSize: number,
  filter: PromotionFilter = {},
): Promise<PagedResult<ItemPromotion>> {
  // The API filters the promotion window in the visitor's time zone, so the
  // storefront reports it rather than letting the server assume UTC.
  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone

  // Empty filters are omitted rather than sent as blanks, so an unfiltered
  // request produces the same URL — and the same cache entry — it always did.
  const params: Record<string, string | number> = { page, pageSize, timeZone }

  if (filter.search?.trim()) params['filter.search'] = filter.search.trim()
  if (filter.categoryId) params['filter.categoryId'] = filter.categoryId
  if (typeof filter.minPrice === 'number') params['filter.minPrice'] = filter.minPrice
  if (typeof filter.maxPrice === 'number') params['filter.maxPrice'] = filter.maxPrice
  if (filter.sort && filter.sort !== 'endingSoon') params['filter.sort'] = filter.sort

  // Uses the shared axios client instead of raw fetch: one place defines the
  // base URL and attaches auth, and the two styles cannot drift apart.
  const response = await api.get<PagedResult<ItemPromotion>>('/item-promotions/active', { params })

  if (response.status === 204 || !response.data) {
    return emptyPage(page, pageSize)
  }

  return response.data
}

export async function getCategories(): Promise<Category[]> {
  const { data } = await api.get<Category[]>('/item-promotions/categories/all')

  return data ?? []
}

// ===== CATEGORY MAINTENANCE (admin) =====

export async function createCategory(name: string): Promise<Category> {
  const { data } = await api.post<Category>('/categories', { name })
  return data
}

export async function renameCategory(id: number, name: string): Promise<Category> {
  const { data } = await api.put<Category>(`/categories/${id}`, { name })
  return data
}

export async function deleteCategory(id: number): Promise<void> {
  await api.delete(`/categories/${id}`)
}
