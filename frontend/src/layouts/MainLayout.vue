<template>
  <NavBar />
  <router-view />
  <WhatsappFloating />
  <CartFloating />
</template>

<script setup lang="ts">
import NavBar from '@/components/NavBar.vue'
import WhatsappFloating from '@/components/WhatsappFloating.vue'
import CartFloating from '@/components/CartFloating.vue'
</script>
