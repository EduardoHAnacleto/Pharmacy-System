import { ref } from 'vue'
import type { ItemPromotion } from '@/types/itemPromotion'
import { getActivePromotionsPaged, type PromotionFilter } from '@/services/itemPromotionService'

const DEFAULT_PAGE_SIZE = 12

export function useInfinitePromotions() {
  const promotions = ref<ItemPromotion[]>([])
  const page = ref(1)
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  const loading = ref(false)
  const hasMore = ref(true)
  const error = ref<string | null>(null)

  /**
   * How many promotions match the current filter, across every page.
   *
   * The API has always returned this and the hook threw it away. The grid can
   * only ever count what it has scrolled to, which is a different number and a
   * misleading one to print — "12 promoções" under an infinite list that holds
   * eighteen.
   */
  const totalItems = ref(0)

  /** Applied to every page request, so scrolling keeps the active filter. */
  const filter = ref<PromotionFilter>({})

  async function loadMore() {
    if (loading.value || !hasMore.value) return

    loading.value = true
    error.value = null

    try {
      const result = await getActivePromotionsPaged(page.value, pageSize.value, filter.value)
      promotions.value.push(...result.items)

      totalItems.value = result.totalItems
      hasMore.value = result.hasMore
      page.value++
    } catch (err: unknown) {
      if (err instanceof Error) {
        error.value = err.message
      } else {
        error.value = 'Erro ao carregar promoções'
      }
    } finally {
      loading.value = false
    }
  }

  function reset() {
    promotions.value = []
    page.value = 1
    hasMore.value = true
    error.value = null
    // Not reset to 0: the count belongs to the filter being replaced, and
    // blanking it makes the number flicker to nothing on every keystroke of a
    // debounced search. The next response overwrites it.
  }

  /**
   * Replaces the filter and reloads from the first page.
   *
   * Resetting is not optional: keeping the accumulated items while changing the
   * filter would leave the grid showing rows that no longer match it.
   */
  async function applyFilter(next: PromotionFilter) {
    filter.value = { ...next }
    reset()
    await loadMore()
  }

  return {
    promotions,
    loading,
    hasMore,
    error,
    filter,
    totalItems,
    loadMore,
    reset,
    applyFilter,
  }
}
