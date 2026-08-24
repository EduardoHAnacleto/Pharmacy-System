<template>
  <footer class="py-4 bg-dark text-white mt-5">
    <div class="container text-center">
      <p class="m-0 fw-semibold">© {{ year }} {{ settings.settings.storeName }}</p>

      <p v-if="contactLine" class="m-0 small text-white-50">{{ contactLine }}</p>

      <!-- WHO IS ANSWERABLE -->
      <!--
        The four things a customer looks for before sending money to a pharmacy
        they have not used: the company's registration, and the professional
        legally answerable for what it dispenses. The footer carried none of
        them. Each line renders only when filled in, so a shop mid-setup shows
        nothing rather than an empty label.
      -->
      <p v-if="complianceLine" class="m-0 mt-2 small text-white-50">{{ complianceLine }}</p>

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

      <!-- PRIVACY, AND THE WAY IN FOR THE SHOPKEEPER -->
      <!--
        Admin sign-in lives here rather than in the navigation bar: the one
        person who needs it knows where the shop is, and a customer's menu has
        better uses for a slot.
      -->
      <p class="m-0 mt-3 small d-flex justify-content-center gap-3">
        <RouterLink to="/privacy" class="text-white-50">
          {{ t('privacy.link') }}
        </RouterLink>
        <RouterLink to="/login" class="text-white-50">
          {{ t('nav.login') }}
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

/**
 * The registration line, assembled from whatever the shop has filled in.
 *
 * The model names these generically — businessNumber holds a CNPJ here and an
 * NZBN in New Zealand — and the market-specific wording lives in the
 * translations, which is the layer that already differs per deployment.
 */
const complianceLine = computed(() => {
  const s = settings.settings
  const parts: string[] = []

  if (s.businessNumber) {
    parts.push(t('footer.businessNumber', { value: s.businessNumber }))
  }

  if (s.technicalManagerName) {
    parts.push(
      s.technicalManagerLicense
        ? t('footer.technicalManagerLicensed', {
            name: s.technicalManagerName,
            license: s.technicalManagerLicense,
          })
        : t('footer.technicalManager', { name: s.technicalManagerName }),
    )
  }

  return parts.length > 0 ? parts.join(' · ') : null
})

const hasSocial = computed(
  () => Boolean(settings.settings.instagramUrl) || Boolean(settings.settings.facebookUrl),
)
</script>
