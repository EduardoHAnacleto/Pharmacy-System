<template>
  <header class="hero">
    <div class="container px-4 px-lg-5">
      <!--
        No shop name here. NavBar prints it two centimetres above, and repeating
        it cost 25px of the fold to tell a visitor something they had just read.
      -->
      <h1 class="hero-title">{{ t('home.heroTitle') }}</h1>

      <p class="hero-sub">{{ subtitle }}</p>

      <!-- WHAT THE SHOP PROMISES -->
      <ul v-if="promises.length > 0" class="hero-promises" :aria-label="t('home.promisesLabel')">
        <li v-for="promise in promises" :key="promise">{{ promise }}</li>
      </ul>
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingsStore } from '@/stores/settings'
import { formatMoney } from '@/utils/format'

/**
 * THE FIRST SCREEN
 *
 * This used to be the shop's logo on a dark band and nothing else — 173px of a
 * phone's 844, carrying no words, above a six-field filter form. Measured on
 * the running stack, the first product card began at 571px: more than two
 * thirds of the fold spent before anything was for sale.
 *
 * What replaces it is text, because text is what answers the question a visitor
 * arrives with. The logo is not lost: NavBar already renders it, and it is a
 * shop's identity rather than its offer.
 *
 * Every promise below is read from store_settings, so a shop that does not
 * deliver never claims to. Nothing here is hardcoded about this pharmacy.
 */
const { t } = useI18n()
const settings = useSettingsStore()

const subtitle = computed(() => settings.settings.tagline?.trim() || t('home.heroSubtitle'))

const promises = computed<string[]>(() => {
  // Nothing until the shop's own settings have actually arrived.
  //
  // main.ts starts settings.load() without awaiting it, so the first render
  // runs against DEFAULT_SETTINGS - and those carry deliveryEnabled: true with
  // deliveryFee: 0, while the backend's own default fee is 8. Reading them
  // would print "Entrega grátis" above the fold for a shop that charges, on
  // every cold load. The error case is worse: load() swallows its failure and
  // leaves the defaults in place, so the false claim would never correct
  // itself. An empty hero for a moment is the honest version.
  if (!settings.loaded || settings.error) return []

  const s = settings.settings
  const out: string[] = []

  if (s.pickupEnabled) out.push(t('home.promisePickup'))

  if (s.deliveryEnabled) {
    const cities = settings.deliveryCities

    // One city is worth naming; a list is not, and "entrega em 6 cidades" is
    // the honest summary rather than a truncated list ending in an ellipsis.
    if (cities.length === 1) {
      out.push(t('home.promiseDeliveryCity', { city: cities[0] }))
    } else if (cities.length > 1) {
      out.push(t('home.promiseDeliveryCities', { count: cities.length }))
    } else {
      out.push(t('home.promiseDelivery'))
    }

    if (s.deliveryFee <= 0) {
      out.push(t('home.promiseFreeDelivery'))
    } else if (s.minDeliveryTotal > 0) {
      out.push(t('home.promiseMinOrder', { amount: formatMoney(s.minDeliveryTotal) }))
    }
  }

  return out
})
</script>

<style scoped>
/*
 * A dark wash over the shop's own colour, rather than the colour alone.
 * primaryColor is whatever a shop typed into its settings — it can be pale
 * yellow — and white text on it would be unreadable. The overlay guarantees
 * contrast for any value while still letting the brand through.
 */
.hero {
  background:
    linear-gradient(rgba(0, 0, 0, 0.55), rgba(0, 0, 0, 0.68)), var(--brand-primary, #212529);
  color: #fff;
  padding: 1.15rem 0 1.25rem;
}

.hero-title {
  margin: 0;
  font-size: clamp(1.5rem, 5.5vw, 2.25rem);
  font-weight: 700;
  line-height: 1.12;
  text-wrap: balance;
}

.hero-sub {
  margin: 0.35rem 0 0;
  font-size: clamp(0.92rem, 2.6vw, 1.02rem);
  line-height: 1.4;
  color: rgba(255, 255, 255, 0.88);
  max-width: 44ch;
}

.hero-promises {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  margin: 0.7rem 0 0;
  padding: 0;
  list-style: none;
}

.hero-promises li {
  font-size: 0.82rem;
  line-height: 1;
  padding: 0.4rem 0.7rem;
  border-radius: 50rem;
  background: rgba(255, 255, 255, 0.14);
  border: 1px solid rgba(255, 255, 255, 0.22);
}
</style>
