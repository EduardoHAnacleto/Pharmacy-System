import { ref } from 'vue'
import type { ItemPromotion } from '@/types/itemPromotion'
import { getActivePromotionsPaged } from '@/services/itemPromotionService'

const DEFAULT_PAGE_SIZE = 12

export function useInfinitePromotions() {
  const promotions = ref<ItemPromotion[]>([])
  const page = ref(1)
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  const loading = ref(false)
  const hasMore = ref(true)
  const error = ref<string | null>(null)

  async function loadMore() {
    if (loading.value || !hasMore.value) return

    loading.value = true
    error.value = null

    try {
      const result = await getActivePromotionsPaged(page.value, pageSize.value)
      //
      console.log(result.items)
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

  return {
    promotions,
    loading,
    hasMore,
    error,
    loadMore,
    reset,
  }
}
