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

  /** Applied to every page request, so scrolling keeps the active filter. */
  const filter = ref<PromotionFilter>({})

  async function loadMore() {
    if (loading.value || !hasMore.value) return

    loading.value = true
    error.value = null

    try {
      const result = await getActivePromotionsPaged(page.value, pageSize.value, filter.value)
      promotions.value.push(...result.items)

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
    loadMore,
    reset,
    applyFilter,
  }
}
