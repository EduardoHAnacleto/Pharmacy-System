<template>
  <NavBar />

  <!-- Wrapped so the footer stays at the bottom on short pages. -->
  <main class="flex-grow-1">
    <router-view />
  </main>

  <SiteFooter />

  <!--
    One floating button, not two. The cart used to sit above the WhatsApp one in
    the same corner — 56px each, stacked over the product grid, on a screen where
    grid space is the scarce thing. The cart moved into the navigation bar with
    its count; WhatsApp stays here because it is the only way an order actually
    leaves this site.
  -->
  <WhatsappFloating />
</template>

<script setup lang="ts">
import NavBar from '@/components/NavBar.vue'
import SiteFooter from '@/components/SiteFooter.vue'
import WhatsappFloating from '@/components/WhatsappFloating.vue'
</script>
