<template>
  <footer class="py-4 bg-dark text-white mt-5">
    <div class="container text-center">
      <p class="m-0 fw-semibold">© {{ year }} {{ settings.settings.storeName }}</p>

      <p v-if="contactLine" class="m-0 small text-white-50">{{ contactLine }}</p>

      <p v-if="settings.settings.footerText" class="m-0 mt-2 small text-white-50">
        {{ settings.settings.footerText }}
      </p>

      <!-- SOCIAL -->
      <div v-if="hasSocial" class="mt-3 d-flex justify-content-center gap-3">
        <a
          v-if="settings.settings.instagramUrl"
          :href="settings.settings.instagramUrl"
          target="_blank"
          rel="noopener"
          class="text-white fs-5"
          aria-label="Instagram"
        >
          <i class="bi bi-instagram" aria-hidden="true"></i>
        </a>
        <a
          v-if="settings.settings.facebookUrl"
          :href="settings.settings.facebookUrl"
          target="_blank"
          rel="noopener"
          class="text-white fs-5"
          aria-label="Facebook"
        >
          <i class="bi bi-facebook" aria-hidden="true"></i>
        </a>
      </div>

      <!-- PRIVACY -->
      <p class="m-0 mt-3 small">
        <RouterLink to="/privacy" class="text-white-50">
          {{ t('privacy.link') }}
        </RouterLink>
      </p>
    </div>
  </footer>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSettingsStore } from '@/stores/settings'

const { t } = useI18n()
const settings = useSettingsStore()

const year = new Date().getFullYear()

// The footer used to read "© Farmacy / Address - (11) 11111-1111" and was never
// rendered anywhere, so nobody noticed the placeholders.
const contactLine = computed(() => {
  const s = settings.settings
  return [s.address, s.city, s.phone].filter(Boolean).join(' · ') || null
})

const hasSocial = computed(
  () => Boolean(settings.settings.instagramUrl) || Boolean(settings.settings.facebookUrl),
)
</script>
