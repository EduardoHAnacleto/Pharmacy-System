<template>
  <NavBar />

  <!-- Wrapped so the footer stays at the bottom on short pages. -->
  <main class="flex-grow-1">
    <router-view />
  </main>

  <SiteFooter />
  <WhatsappFloating />
  <CartFloating />
</template>

<script setup lang="ts">
import NavBar from '@/components/NavBar.vue'
import SiteFooter from '@/components/SiteFooter.vue'
import WhatsappFloating from '@/components/WhatsappFloating.vue'
import CartFloating from '@/components/CartFloating.vue'
</script>
