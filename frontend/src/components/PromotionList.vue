<template>
  <section class="py-5">
    <div class="container px-4 px-lg-5 mt-5">
      <div class="row gx-4 gx-lg-5 row-cols-2 row-cols-md-3 row-cols-xl-4 justify-content-center">
        <!-- CARD -->
        <div class="col mb-5" v-for="item in promotions" :key="item.id">
          <div class="card h-100">
            <img class="card-img-top" :src="item.imageUrl" />

            <div class="card-body p-4">
              <div class="text-center">
                <h5 class="fw-bolder">{{ item.name }}</h5>

                <span class="text-muted text-decoration-line-through">
                  R$ {{ item.priceBefore.toFixed(2) }}
                </span>
                <br />

                <span class="fw-bold"> R$ {{ item.price.toFixed(2) }} </span>

                <div class="small text-muted mt-2">
                  {{ formatDate(item.dateStart) }} -
                  {{ formatDate(item.dateEnd) }}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- LOADING -->
      <div v-if="loading" class="text-center py-3">Carregando...</div>

      <!-- ERROR -->
      <div v-if="error" class="text-center text-danger py-3">
        {{ error }}
      </div>

      <!-- SENTINEL -->
      <div ref="sentinel" style="height: 1px"></div>

      <!-- END -->
      <div v-if="!hasMore && promotions.length > 0" class="text-center text-muted py-3">
        Fim das promoções
      </div>

      <!-- EMPTY -->
      <div v-if="!loading && promotions.length === 0" class="text-center text-muted py-5">
        Nenhuma promoção disponível
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { useInfinitePromotions } from '@/hooks/useInfinitePromotions'

const { promotions, loading, hasMore, error, loadMore } = useInfinitePromotions()

const sentinel = ref<HTMLElement | null>(null)
let observer: IntersectionObserver | null = null

function formatDate(date: string) {
  return new Date(date).toLocaleDateString('pt-BR')
}

onMounted(async () => {
  await loadMore()

  observer = new IntersectionObserver(
    async (entries) => {
      if (!entries[0]?.isIntersecting) return
      await loadMore()
    },
    {
      root: null,
      threshold: 1.0,
    },
  )

  if (sentinel.value) {
    observer.observe(sentinel.value)
  }
})

onBeforeUnmount(() => {
  if (observer && sentinel.value) {
    observer.unobserve(sentinel.value)
  }
})
</script>
