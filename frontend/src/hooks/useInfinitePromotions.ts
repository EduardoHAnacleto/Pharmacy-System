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

  /**
   * Which set of results is current.
   *
   * Bumped whenever the filter changes. A request that was already open when
   * that happened belongs to the previous set: its response is discarded rather
   * than pushed into a grid that is now showing something else.
   */
  let generation = 0

  /** Appends the next page. Ignored while a request for this set is open. */
  async function loadMore() {
    if (loading.value || !hasMore.value) return

    await fetchPage()
  }

  async function fetchPage() {
    const mine = generation

    loading.value = true
    error.value = null

    try {
      const result = await getActivePromotionsPaged(page.value, pageSize.value, filter.value)

      // Superseded while this was in flight — the grid has moved on.
      if (mine !== generation) return

      promotions.value.push(...result.items)

      totalItems.value = result.totalItems
      hasMore.value = result.hasMore
      page.value++
    } catch (err: unknown) {
      if (mine !== generation) return

      if (err instanceof Error) {
        error.value = err.message
      } else {
        error.value = 'Erro ao carregar promoções'
      }
    } finally {
      // Only the current request owns the flag. A superseded one clearing it
      // would unlock a second concurrent fetch for the set that replaced it.
      if (mine === generation) loading.value = false
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
   * Starts again from page one, superseding anything already in flight.
   *
   * The old code called loadMore(), which returns immediately while a request
   * is open. On a chip row that fires with no debounce, a category tapped
   * during the first page's load was therefore dropped in silence: the grid
   * kept every unfiltered item and the count kept the unfiltered total, and
   * nothing retried until the visitor touched a filter again.
   */
  async function reload() {
    generation++
    reset()

    // The superseded request still owns the flag and will not clear it, since
    // it no longer belongs to the current generation.
    loading.value = false

    await fetchPage()
  }

  /**
   * Replaces the filter and reloads from the first page.
   *
   * Resetting is not optional: keeping the accumulated items while changing the
   * filter would leave the grid showing rows that no longer match it.
   */
  async function applyFilter(next: PromotionFilter) {
    filter.value = { ...next }
    await reload()
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
    reload,
    applyFilter,
  }
}
