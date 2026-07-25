<template>
  <header class="hero">
    <img :src="logo" alt="PHARMACY" class="hero-logo" />
  </header>
</template>

<script setup lang="ts">
import logo from '@/assets/logoFarma.png'
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
</style>
