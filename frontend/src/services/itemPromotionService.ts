import type { ItemPromotion } from '@/types/itemPromotion'
import type { PagedResult } from '@/types/pagedResult'

export async function getActivePromotionsPaged(
  page: number,
  pageSize: number,
): Promise<PagedResult<ItemPromotion>> {
  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone
  const response = await fetch(
    `/api/item-promotions/active?page=${page}&pageSize=${pageSize}&timeZone=${encodeURIComponent(timeZone)}`,
    {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json',
      },
    },
  )

  if (response.status === 204) {
    return {
      items: [],
      page,
      pageSize,
      totalItems: 0,
      hasMore: false,
    }
  }

  if (!response.ok) {
    const text = await response.text()
    throw new Error(text || 'Erro ao buscar promoções')
  }

  return (await response.json()) as PagedResult<ItemPromotion>
}
