<template>
  <router-link to="/cart" class="cart-float">
    <i class="bi bi-cart-fill"></i>
    <span v-if="itemsCount > 0" class="badge">
      {{ itemsCount }}
    </span>
  </router-link>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useCartStore } from '@/stores/cart'

const cart = useCartStore()
const { itemsCount } = storeToRefs(cart)
</script>

<style scoped>
.cart-float {
  position: fixed;
  width: 56px;
  height: 56px;
  bottom: 90px;
  right: 20px;
  background-color: #212529;
  color: #fff;
  border-radius: 50%;
  text-align: center;
  font-size: 24px;
  line-height: 56px;
  z-index: 9999;
}

.badge {
  position: absolute;
  top: -5px;
  right: -5px;
  background: red;
  color: white;
  border-radius: 50%;
  padding: 4px 7px;
  font-size: 12px;
}
</style>
