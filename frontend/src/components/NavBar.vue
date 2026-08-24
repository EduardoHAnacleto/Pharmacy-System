<template>
  <nav class="navbar navbar-expand-lg navbar-light bg-light">
    <div class="container px-4 px-lg-5">
      <RouterLink to="/" class="navbar-brand d-flex align-items-center gap-2">
        <img
          v-if="settings.settings.logoUrl"
          :src="settings.settings.logoUrl"
          :alt="settings.settings.storeName"
          class="navbar-logo"
        />
        <span>{{ settings.settings.storeName }}</span>
      </RouterLink>

      <button
        class="navbar-toggler"
        type="button"
        data-bs-toggle="collapse"
        data-bs-target="#navbarSupportedContent"
        aria-controls="navbarSupportedContent"
        aria-expanded="false"
        :aria-label="t('nav.home')"
      >
        <span class="navbar-toggler-icon"></span>
      </button>

      <div class="collapse navbar-collapse" id="navbarSupportedContent">
        <ul class="navbar-nav me-auto mb-2 mb-lg-0 ms-lg-4">
          <li class="nav-item">
            <RouterLink to="/" class="nav-link" active-class="active">
              {{ t('nav.home') }}
            </RouterLink>
          </li>
          <li class="nav-item">
            <RouterLink to="/cart" class="nav-link cart-link" active-class="active">
              {{ t('nav.cart') }}
              <!--
                The count used to live only on a floating button stacked above
                the WhatsApp one, both covering the grid. Here it is attached to
                the word it counts, and the corner is free.
              -->
              <span v-if="itemsCount > 0" class="cart-count" :aria-label="cartCountLabel">
                {{ itemsCount }}
              </span>
            </RouterLink>
          </li>
          <li class="nav-item">
            <RouterLink to="/contact" class="nav-link" active-class="active">
              {{ t('nav.contact') }}
            </RouterLink>
          </li>
          <!--
            No admin sign-in here. Four items in a customer's menu and one of
            them opened the shop's back office — a slot spent on the one person
            who already knows the address, and an invitation to everyone else to
            try it. It moved to the footer; the route is unchanged.
          -->
        </ul>
      </div>
    </div>
  </nav>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { storeToRefs } from 'pinia'
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useCartStore } from '@/stores/cart'
import { useSettingsStore } from '@/stores/settings'

const { t } = useI18n()
const settings = useSettingsStore()

const { itemsCount } = storeToRefs(useCartStore())

// The badge shows a bare number; this is what a screen reader announces. It is
// the only place the count is put into a sentence, so it is the only place that
// has to get "1 item" right rather than reading out "1 itens".
const cartCountLabel = computed(() =>
  itemsCount.value === 1 ? t('nav.cartCountOne') : t('nav.cartCount', { count: itemsCount.value }),
)
</script>

<style scoped>
.navbar-logo {
  height: 32px;
  width: auto;
}

.cart-link {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
}

/*
 * Not the hardcoded red the floating badge used: a count is information, not an
 * alarm, and red on a shop that has chosen its own palette was the one colour
 * nobody picked.
 */
.cart-count {
  display: inline-grid;
  place-items: center;
  min-width: 1.35rem;
  height: 1.35rem;
  padding: 0 0.35rem;
  border-radius: 50rem;
  background: var(--brand-primary, #0d6efd);
  color: #fff;
  font-size: 0.75rem;
  font-weight: 700;
  line-height: 1;
  font-variant-numeric: tabular-nums;
}
</style>
