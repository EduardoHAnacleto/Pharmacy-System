<template>
  <!--
    py-3 and no mt-5. The grid opened with 3rem of section padding plus another
    3rem of margin — 96px of nothing between the filters and the first card, on
    a screen where the first card was already below the fold.
  -->
  <section class="py-3">
    <div class="container px-4 px-lg-5">
      <div class="row gx-4 gx-lg-5 row-cols-2 row-cols-md-3 row-cols-xl-4 justify-content-center">
        <!-- PRODUCTS -->
        <div
          class="col mb-4"
          v-for="row in rows"
          :key="row.item.id"
          :data-promotion-id="row.item.id"
          ref="cards"
        >
          <div class="card h-100" :class="{ 'promo-ending': row.urgency }">
            <img
              class="card-img-top"
              :src="row.item.imageUrl"
              :alt="t('product.imageAlt', { name: row.item.name })"
              loading="lazy"
            />

            <!-- FINAL WEEK -->
            <span
              v-if="row.urgency"
              class="promo-ending__badge"
              :class="{ 'promo-ending__badge--critical': row.urgency.critical }"
            >
              <i class="bi bi-alarm-fill" aria-hidden="true"></i>
              {{ row.urgency.label }}
            </span>

            <!-- HOW MUCH OFF -->
            <!--
              Opposite corner from the countdown on purpose: the two can appear
              on the same card, and they say different things.
            -->
            <span v-if="row.discount" class="promo-discount"> −{{ row.discount.percent }}% </span>

            <!--
              p-3, not p-4. Two columns on a 390px phone leave each card about
              163px wide; 24px of padding on every side of that is a quarter of
              the card spent on air, and it pushed the second row off the fold.
            -->
            <div class="card-body p-3">
              <div class="text-center">
                <h5 class="promo-name fw-bolder">{{ row.item.name }}</h5>

                <!-- PRESCRIPTION -->
                <!--
                  Inline rather than a third corner badge: the image already
                  carries the countdown and the discount, and this is not a
                  promotional flag competing with them — it is a condition of
                  sale, and it belongs with the words.
                -->
                <span v-if="row.item.requiresPrescription" class="promo-rx">
                  <i class="bi bi-clipboard2-pulse" aria-hidden="true"></i>
                  {{ t('product.requiresPrescription') }}
                </span>

                <!-- PROMOTED -->
                <div v-if="row.item.priceBefore">
                  <span class="text-muted text-decoration-line-through">
                    {{ formatMoney(row.item.priceBefore) }}
                  </span>
                  <br />
                  <span class="fw-bold">{{ formatMoney(row.item.price) }}</span>

                  <span v-if="row.discount" class="promo-saved d-block">
                    {{ t('product.saved', { amount: formatMoney(row.discount.saved) }) }}
                  </span>
                </div>

                <!-- NOT PROMOTED -->
                <div v-else>{{ formatMoney(row.item.price) }}</div>

                <!-- DURATION OF PROMOTION -->
                <!--
                  The urgent styling replaces text-muted rather than sitting on
                  top of it: Bootstrap sets that colour with !important, so the
                  two cannot both apply and the muted grey would win.
                -->
                <small
                  v-if="row.item.dateEnd"
                  class="d-block mt-2"
                  :class="row.urgency ? 'promo-ending__validity' : 'text-muted'"
                >
                  {{ t('product.validity', { to: formatDate(row.item.dateEnd) }) }}
                </small>
              </div>
            </div>

            <div class="card-footer p-3 pt-0 border-top-0 bg-transparent">
              <div class="text-center">
                <button class="btn btn-outline-dark btn-sm mt-auto" @click="addToCart(row.item)">
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
import { endingSoonIn } from '@/utils/promotionUrgency'
import { discountOf, type PromotionDiscount } from '@/utils/promotionPricing'
import type { ItemPromotion } from '@/types/itemPromotion'

const props = defineProps<{
  products: ItemPromotion[]
}>()

const { t } = useI18n()
const cartStore = useCartStore()
const settings = useSettingsStore()

/**
 * FINAL WEEK
 *
 * A promotion about to end is the one a shopper can still act on, so it is
 * marked instead of leaving them to read an end date and work it out. Each
 * product is paired with its countdown here rather than in the template, which
 * keeps date arithmetic out of the markup and resolves the label once per
 * product instead of on every re-render of every card.
 *
 * The clock is read once for the whole grid: two promotions ending on the same
 * day must never disagree about how far away it is, which a per-card
 * `new Date()` would allow if a render straddled midnight.
 *
 * This is derived, not fetched — no API change, and it stays correct for any
 * promotion whatever its dates.
 */
interface CardRow {
  item: ItemPromotion
  /** Null unless the promotion is inside its last week. */
  urgency: { label: string; critical: boolean } | null
  /** Null when there is no old price to compare against. */
  discount: PromotionDiscount | null
}

const rows = computed<CardRow[]>(() => {
  const now = new Date()

  return props.products.map((item) => {
    const days = endingSoonIn(item.dateEnd, now)

    return {
      item,
      urgency:
        days === null
          ? null
          : {
              label: endingLabel(days),
              // Only the last day or two pulse. Every card in a full grid can
              // be inside the week, and a dozen pulsing badges is noise, not
              // emphasis.
              critical: days <= 1,
            },
      discount: discountOf(item.price, item.priceBefore),
    }
  })
})

function endingLabel(days: number): string {
  if (days === 0) return t('product.endingToday')
  if (days === 1) return t('product.endingTomorrow')

  return t('product.daysLeft', { count: days })
}

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
    requiresPrescription: item.requiresPrescription,
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
