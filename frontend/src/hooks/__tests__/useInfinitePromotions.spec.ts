import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ItemPromotion } from '@/types/itemPromotion'
import type { PagedResult } from '@/types/pagedResult'

const getActivePromotionsPaged = vi.fn()

// The filter is forwarded too: whether a request carries the filter the visitor
// chose is exactly what the supersede test below has to assert.
vi.mock('@/services/itemPromotionService', () => ({
  getActivePromotionsPaged: (page: number, pageSize: number, filter: unknown) =>
    getActivePromotionsPaged(page, pageSize, filter),
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
    requiresPrescription: false,
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

  it('does not drop a filter chosen while the first page is still loading', async () => {
    // The ordinary case since the filters became a chip row: chips fire with no
    // debounce, and on mobile data the visitor taps a category well before the
    // unfiltered first page lands. The old guard returned early whenever a
    // request was open, so that tap was swallowed and nothing retried.
    let releaseFirst: (result: PagedResult<ItemPromotion>) => void = () => {}

    getActivePromotionsPaged.mockImplementationOnce(
      () =>
        new Promise<PagedResult<ItemPromotion>>((resolve) => {
          releaseFirst = resolve
        }),
    )
    getActivePromotionsPaged.mockResolvedValueOnce(page([promotion(9)], false))

    const { promotions, totalItems, loadMore, applyFilter } = useInfinitePromotions()

    const firstRequest = loadMore()
    const filteredRequest = applyFilter({ categoryId: 3 })

    releaseFirst(page([promotion(1), promotion(2)], true))
    await firstRequest
    await filteredRequest

    // The filtered request was actually issued, carrying the chosen category.
    expect(getActivePromotionsPaged).toHaveBeenCalledTimes(2)
    expect(getActivePromotionsPaged).toHaveBeenLastCalledWith(1, 12, { categoryId: 3 })

    // And the superseded response never reached the grid or the count, which is
    // how an unfiltered list used to survive underneath a chosen filter.
    expect(promotions.value.map((p) => p.id)).toEqual([9])
    expect(totalItems.value).toBe(1)
  })

  it('reload starts again from page one even with a request in flight', async () => {
    // What the SignalR handler does when a promotion changes. reset() followed
    // by loadMore() emptied the grid and then bailed out, and the in-flight
    // page-N response repopulated it with page N alone.
    let release: (result: PagedResult<ItemPromotion>) => void = () => {}

    getActivePromotionsPaged.mockImplementationOnce(
      () =>
        new Promise<PagedResult<ItemPromotion>>((resolve) => {
          release = resolve
        }),
    )
    getActivePromotionsPaged.mockResolvedValueOnce(page([promotion(7), promotion(8)], false))

    const { promotions, loadMore, reload } = useInfinitePromotions()

    const inFlight = loadMore()
    const reloaded = reload()

    release(page([promotion(1)], true))
    await inFlight
    await reloaded

    expect(promotions.value.map((p) => p.id)).toEqual([7, 8])
  })

  it('appends each page and advances the page number', async () => {
    getActivePromotionsPaged
      .mockResolvedValueOnce(page([promotion(1), promotion(2)], true))
      .mockResolvedValueOnce(page([promotion(3)], false))

    const { promotions, loadMore, hasMore } = useInfinitePromotions()

    await loadMore()
    expect(promotions.value.map((p) => p.id)).toEqual([1, 2])
    expect(getActivePromotionsPaged).toHaveBeenLastCalledWith(1, 12, {})

    await loadMore()
    expect(promotions.value.map((p) => p.id)).toEqual([1, 2, 3])
    expect(getActivePromotionsPaged).toHaveBeenLastCalledWith(2, 12, {})
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

    // reset() on its own still rewinds the paging, which is what this asserts.
    // The SignalR handler no longer pairs it with loadMore() — see the reload
    // test above for why that pairing lost pages when a request was open.
    await loadMore()
    expect(getActivePromotionsPaged).toHaveBeenLastCalledWith(1, 12, {})
  })
})
