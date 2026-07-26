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
            <RouterLink to="/cart" class="nav-link" active-class="active">
              {{ t('nav.cart') }}
            </RouterLink>
          </li>
          <li class="nav-item">
            <RouterLink to="/contact" class="nav-link" active-class="active">
              {{ t('nav.contact') }}
            </RouterLink>
          </li>
          <li class="nav-item">
            <RouterLink to="/login" class="nav-link" active-class="active">
              {{ t('nav.login') }}
            </RouterLink>
          </li>
        </ul>
      </div>
    </div>
  </nav>
</template>

<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSettingsStore } from '@/stores/settings'

const { t } = useI18n()
const settings = useSettingsStore()
</script>

<style scoped>
.navbar-logo {
  height: 32px;
  width: auto;
}
</style>
