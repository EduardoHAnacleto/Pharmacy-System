<template>
  <section class="py-5">
    <div class="container px-4 px-lg-5 mt-5">
      <div class="row gx-4 gx-lg-5 row-cols-2 row-cols-md-3 row-cols-xl-4 justify-content-center">
        <!-- PRODUCTS -->
        <div
          class="col mb-5"
          v-for="item in products"
          :key="item.id"
          :data-promotion-id="item.id"
          ref="cards"
        >
          <div class="card h-100">
            <img
              class="card-img-top"
              :src="item.imageUrl"
              :alt="t('product.imageAlt', { name: item.name })"
              loading="lazy"
            />

            <div class="card-body p-4">
              <div class="text-center">
                <h5 class="fw-bolder">{{ item.name }}</h5>

                <!-- PROMOTED -->
                <div v-if="item.priceBefore">
                  <span class="text-muted text-decoration-line-through">
                    {{ formatMoney(item.priceBefore) }}
                  </span>
                  <br />
                  <span class="fw-bold">{{ formatMoney(item.price) }}</span>
                </div>

                <!-- NOT PROMOTED -->
                <div v-else>{{ formatMoney(item.price) }}</div>

                <!-- DURATION OF PROMOTION -->
                <small v-if="item.dateStart && item.dateEnd" class="text-muted d-block mt-2">
                  {{
                    t('product.validity', {
                      from: formatDate(item.dateStart),
                      to: formatDate(item.dateEnd),
                    })
                  }}
                </small>
              </div>
            </div>

            <div class="card-footer p-4 pt-0 border-top-0 bg-transparent">
              <div class="text-center">
                <button class="btn btn-outline-dark mt-auto" @click="addToCart(item)">
                  {{ t('product.add') }}
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- EMPTY LIST -->
        <div v-if="products.length === 0" class="text-center text-muted py-5">
          {{ t('product.empty') }}
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useCartStore } from '@/stores/cart'
import { useSettingsStore } from '@/stores/settings'
import { useJsonLd } from '@/composables/useJsonLd'
import { track, trackPromotionView } from '@/services/analytics'
import { formatDate, formatMoney } from '@/utils/format'
import type { ItemPromotion } from '@/types/itemPromotion'

const props = defineProps<{
  products: ItemPromotion[]
}>()

const { t } = useI18n()
const cartStore = useCartStore()
const settings = useSettingsStore()

/**
 * STRUCTURED DATA
 *
 * The grid described as a schema.org ItemList of Products with Offers, so a
 * search engine reads each promotion's price and validity instead of inferring
 * them from markup — and so a shared link can show a rich result.
 */
const structuredData = computed(() => ({
  '@context': 'https://schema.org',
  '@type': 'ItemList',
  itemListElement: props.products.map((item, index) => ({
    '@type': 'ListItem',
    position: index + 1,
    item: {
      '@type': 'Product',
      name: item.name,
      image: item.imageUrl,
      offers: {
        '@type': 'Offer',
        price: item.price,
        priceCurrency: settings.settings.currency,
        availability: 'https://schema.org/InStock',
        priceValidUntil: item.dateEnd,
      },
    },
  })),
}))

useJsonLd(structuredData)

function addToCart(item: ItemPromotion) {
  cartStore.addItem({
    id: item.id,
    name: item.name,
    price: item.price,
    imageUrl: item.imageUrl,
  })

  track('add_to_cart', item.id)
}

/**
 * IMPRESSIONS
 *
 * Reported when a card actually enters the viewport, not when it is fetched:
 * "views" that count cards nobody scrolled to would make the view-to-cart rate
 * meaningless, and that rate is the number worth acting on.
 */
const cards = useTemplateRef<HTMLElement[]>('cards')
const observer = ref<IntersectionObserver | null>(null)

function observeCards() {
  if (typeof IntersectionObserver === 'undefined') return

  observer.value ??= new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue

        const id = Number((entry.target as HTMLElement).dataset.promotionId)
        if (Number.isFinite(id)) trackPromotionView(id)

        // Once counted, stop watching it.
        observer.value?.unobserve(entry.target)
      }
    },
    { threshold: 0.5 },
  )

  for (const card of cards.value ?? []) {
    observer.value.observe(card)
  }
}

// The grid grows as more pages load, so newly rendered cards need observing too.
watch(() => props.products.length, observeCards, { flush: 'post', immediate: true })

onBeforeUnmount(() => {
  observer.value?.disconnect()
  observer.value = null
})
</script>
