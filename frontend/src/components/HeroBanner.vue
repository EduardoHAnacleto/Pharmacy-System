<template>
  <header class="hero">
    <!-- CONFIGURED LOGO -->
    <img
      v-if="settings.settings.logoUrl"
      :src="settings.settings.logoUrl"
      :alt="settings.settings.storeName"
      class="hero-logo"
    />

    <!-- FALLBACK -->
    <div v-else class="hero-text text-center">
      <h1 class="m-0 fw-bold">{{ settings.settings.storeName }}</h1>
      <p v-if="settings.settings.tagline" class="m-0 mt-2 lead">
        {{ settings.settings.tagline }}
      </p>
    </div>
  </header>
</template>

<script setup lang="ts">
import { useSettingsStore } from '@/stores/settings'

const settings = useSettingsStore()
</script>

<style scoped>
.hero {
  background-color: #212529;
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 2rem 0;
}

.hero-logo {
  width: min(90vw, 1400px);
  height: auto;
}

/* Falls back to the shop's name when no logo is configured, so a new shop is
   presentable before it has uploaded any artwork. */
.hero-text {
  color: #fff;
  padding: 2rem 1rem;
}
</style>
