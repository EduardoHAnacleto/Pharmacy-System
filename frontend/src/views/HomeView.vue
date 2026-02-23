<template>
  <Hero />

  <!-- START LOADING  -->
  <div v-if="loading && promotions.length === 0" class="text-center py-5">
    Carregando produtos...
  </div>

  <!-- GRID -->
  <ProductGrid v-if="promotions.length > 0" :products="promotions" />

  <!-- LOADING MORE -->
  <div v-if="loading && promotions.length > 0 && hasMore" class="text-center py-3">
    Carregando mais...
  </div>

  <!-- SENTINEL -->
  <div ref="sentinel" style="height: 1px"></div>
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref } from 'vue'

import Hero from '@/components/Hero.vue'
import ProductGrid from '@/components/ProductGrid.vue'
import { startSignalR, onPromotionsChanged } from '@/services/signalr'
import { useInfinitePromotions } from '@/hooks/useInfinitePromotions'

const { promotions, loading, hasMore, loadMore, reset } = useInfinitePromotions()

const sentinel = ref<HTMLElement | null>(null)
let observer: IntersectionObserver | null = null

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
