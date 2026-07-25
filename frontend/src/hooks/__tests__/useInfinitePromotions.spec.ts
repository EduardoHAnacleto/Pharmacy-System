import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ItemPromotion } from '@/types/itemPromotion'
import type { PagedResult } from '@/types/pagedResult'

const getActivePromotionsPaged = vi.fn()

vi.mock('@/services/itemPromotionService', () => ({
  getActivePromotionsPaged: (page: number, pageSize: number) =>
    getActivePromotionsPaged(page, pageSize),
}))

const { useInfinitePromotions } = await import('@/hooks/useInfinitePromotions')

function promotion(id: number): ItemPromotion {
  return {
    id,
    name: `Promo ${id}`,
    price: 9.9,
    priceBefore: 19.9,
    imageUrl: `/images/promotions/${id}.png`,
    dateStart: '2026-01-01T00:00:00Z',
    dateEnd: '2026-12-31T00:00:00Z',
    status: 'Active',
    archivedAt: null,
    imageMissing: false,
    sourcePromotionId: null,
    categoryId: 1,
    productType: 'default',
    createdByUserId: 1,
    createdByUserName: 'admin',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
  }
}

function page(items: ItemPromotion[], hasMore: boolean): PagedResult<ItemPromotion> {
  return { items, page: 1, pageSize: 12, totalItems: items.length, hasMore }
}

describe('useInfinitePromotions', () => {
  beforeEach(() => {
    getActivePromotionsPaged.mockReset()
  })

  it('appends each page and advances the page number', async () => {
    getActivePromotionsPaged
      .mockResolvedValueOnce(page([promotion(1), promotion(2)], true))
      .mockResolvedValueOnce(page([promotion(3)], false))

    const { promotions, loadMore, hasMore } = useInfinitePromotions()

    await loadMore()
    expect(promotions.value.map((p) => p.id)).toEqual([1, 2])
    expect(getActivePromotionsPaged).toHaveBeenLastCalledWith(1, 12)

    await loadMore()
    expect(promotions.value.map((p) => p.id)).toEqual([1, 2, 3])
    expect(getActivePromotionsPaged).toHaveBeenLastCalledWith(2, 12)
    expect(hasMore.value).toBe(false)
  })

  it('stops requesting once the server reports no more pages', async () => {
    getActivePromotionsPaged.mockResolvedValue(page([promotion(1)], false))

    const { loadMore } = useInfinitePromotions()

    await loadMore()
    await loadMore()

    // The scroll observer fires repeatedly; without the hasMore guard this would
    // hammer the API at the bottom of the list.
    expect(getActivePromotionsPaged).toHaveBeenCalledTimes(1)
  })

  it('does not issue overlapping requests', async () => {
    let release: (value: PagedResult<ItemPromotion>) => void = () => {}
    getActivePromotionsPaged.mockReturnValueOnce(
      new Promise<PagedResult<ItemPromotion>>((resolve) => {
        release = resolve
      }),
    )

    const { loadMore } = useInfinitePromotions()

    const first = loadMore()
    await loadMore() // while the first is still in flight

    expect(getActivePromotionsPaged).toHaveBeenCalledTimes(1)

    release(page([promotion(1)], false))
    await first
  })

  it('surfaces the error message and clears loading', async () => {
    getActivePromotionsPaged.mockRejectedValue(new Error('Erro ao buscar promoções'))

    const { loadMore, error, loading } = useInfinitePromotions()

    await loadMore()

    expect(error.value).toBe('Erro ao buscar promoções')
    expect(loading.value).toBe(false)
  })

  it('falls back to a generic message for a non-Error rejection', async () => {
    getActivePromotionsPaged.mockRejectedValue('boom')

    const { loadMore, error } = useInfinitePromotions()

    await loadMore()

    expect(error.value).toBe('Erro ao carregar promoções')
  })

  it('reset clears the list so a realtime update reloads from page 1', async () => {
    getActivePromotionsPaged.mockResolvedValue(page([promotion(1)], true))

    const { promotions, loadMore, reset, hasMore, error } = useInfinitePromotions()

    await loadMore()
    expect(promotions.value).toHaveLength(1)

    reset()

    expect(promotions.value).toHaveLength(0)
    expect(hasMore.value).toBe(true)
    expect(error.value).toBeNull()

    // The SignalR handler calls reset() then loadMore(); it must start over.
    await loadMore()
    expect(getActivePromotionsPaged).toHaveBeenLastCalledWith(1, 12)
  })
})
