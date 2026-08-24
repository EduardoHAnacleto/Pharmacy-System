<template>
  <HeroBanner />

  <PromotionFilters @change="onFilterChange" />

  <!-- HOW MANY -->
  <!--
    The grid was an infinite list with no idea how long it was. A count answers
    "is it worth scrolling" before the scrolling, and after a filter it is the
    fastest way to see that something was excluded.
  -->
  <div v-if="totalItems > 0" class="container px-4 px-lg-5 pt-2">
    <p class="results-count m-0">
      {{ countLabel }}
      <span v-if="endingSoonLabel" class="results-count__urgent">· {{ endingSoonLabel }}</span>
    </p>
  </div>

  <!-- START LOADING  -->
  <div v-if="loading && promotions.length === 0" class="text-center py-5">
    {{ t('home.loading') }}
  </div>

  <!-- NO RESULTS -->
  <div v-else-if="promotions.length === 0 && filtered" class="text-center text-muted py-5">
    {{ t('filters.noResults') }}
  </div>

  <!-- GRID -->
  <ProductGrid v-if="promotions.length > 0" :products="promotions" />

  <!-- LOADING MORE -->
  <div v-if="loading && promotions.length > 0 && hasMore" class="text-center py-3">
    {{ t('home.loadingMore') }}
  </div>

  <!-- SENTINEL -->
  <div ref="sentinel" style="height: 1px"></div>
</template>

<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import HeroBanner from '@/components/HeroBanner.vue'
import ProductGrid from '@/components/ProductGrid.vue'
import PromotionFilters from '@/components/PromotionFilters.vue'
import { startSignalR, onPromotionsChanged } from '@/services/signalr'
import { useInfinitePromotions } from '@/hooks/useInfinitePromotions'
import { endingSoonIn } from '@/utils/promotionUrgency'
import type { PromotionFilter } from '@/services/itemPromotionService'

const { t } = useI18n()
const { promotions, loading, hasMore, totalItems, loadMore, reload, applyFilter } =
  useInfinitePromotions()

const countLabel = computed(() =>
  totalItems.value === 1 ? t('home.countOne') : t('home.count', { count: totalItems.value }),
)

/**
 * How many of the matching promotions are in their final week.
 *
 * Only once every page is loaded. The grid can count what it holds, and until
 * the last page arrives that is a subset — the number would start low and climb
 * as the visitor scrolled, which is worse than not showing it.
 */
const endingSoonLabel = computed(() => {
  if (hasMore.value || loading.value) return null

  const now = new Date()
  const count = promotions.value.filter((p) => endingSoonIn(p.dateEnd, now) !== null).length

  if (count === 0) return null

  return count === 1 ? t('home.countEndingSoonOne') : t('home.countEndingSoon', { count })
})

const sentinel = ref<HTMLElement | null>(null)

// Distinguishes "no promotions at all" from "nothing matched" — the same empty
// grid otherwise leaves a visitor unsure whether to clear their filter.
const filtered = ref(false)
let observer: IntersectionObserver | null = null

async function onFilterChange(filter: PromotionFilter) {
  filtered.value = Boolean(
    filter.search?.trim() ||
    filter.categoryId ||
    typeof filter.minPrice === 'number' ||
    typeof filter.maxPrice === 'number',
  )

  await applyFilter(filter)
}

onMounted(async () => {
  startSignalR()

  // reload(), not reset() + loadMore(). The latter empties the grid and then
  // returns without doing anything if a page request is already open — and the
  // response to that one, when it lands, appends page N to an empty list, so
  // pages one to N-1 simply vanish until the visitor scrolls again. A promotion
  // changing while the grid is loading is exactly when this fires.
  onPromotionsChanged(() => {
    void reload()
  })

  observer = new IntersectionObserver(
    (entries) => {
      const [entry] = entries
      if (!entry) return
      if (entry.isIntersecting && hasMore.value) {
        loadMore()
      }
    },
    {
      root: null,
      rootMargin: '200px',
      threshold: 0,
    },
  )

  if (sentinel.value) {
    observer.observe(sentinel.value)
  }

  loadMore()
})

onBeforeUnmount(() => {
  if (observer && sentinel.value) {
    observer.unobserve(sentinel.value)
  }
})
</script>
