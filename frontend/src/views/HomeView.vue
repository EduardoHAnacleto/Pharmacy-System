<template>
  <HeroBanner />

  <PromotionFilters @change="onFilterChange" />

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
import { onMounted, onBeforeUnmount, ref } from 'vue'
import { useI18n } from 'vue-i18n'

import HeroBanner from '@/components/HeroBanner.vue'
import ProductGrid from '@/components/ProductGrid.vue'
import PromotionFilters from '@/components/PromotionFilters.vue'
import { startSignalR, onPromotionsChanged } from '@/services/signalr'
import { useInfinitePromotions } from '@/hooks/useInfinitePromotions'
import type { PromotionFilter } from '@/services/itemPromotionService'

const { t } = useI18n()
const { promotions, loading, hasMore, loadMore, reset, applyFilter } = useInfinitePromotions()

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

  onPromotionsChanged(() => {
    reset()
    loadMore()
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
