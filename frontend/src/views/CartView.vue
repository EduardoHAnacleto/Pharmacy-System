<template>
  <section class="container py-5">
    <h2 class="fw-bolder mb-4">Meu Carrinho</h2>

    <!-- EMPTY CART -->
    <div v-if="cart.items.length === 0" class="text-center py-5">
      <p class="text-muted mb-3">Seu carrinho está vazio.</p>
      <RouterLink to="/" class="btn btn-outline-primary"> Voltar para a loja </RouterLink>
    </div>

    <!-- CART WITH ITEMS -->
    <div v-else class="row">
      <!-- LIST -->
      <div class="col-lg-8">
        <div
          v-for="item in cart.items"
          :key="item.id"
          class="d-flex align-items-center justify-content-between border-bottom py-3"
        >
          <div class="d-flex align-items-center gap-3">
            <img
              :src="item.imageUrl"
              :alt="item.name"
              class="rounded"
              style="width: 80px; height: 80px; object-fit: cover"
            />

            <div>
              <h6 class="mb-1">{{ item.name }}</h6>
              <small class="text-muted">
                {{ formatPrice(item.price) }}
              </small>
            </div>
          </div>

          <div class="d-flex align-items-center gap-3">
            <!-- QUANTITY -->
            <div class="d-flex align-items-center">
              <button
                class="btn btn-sm btn-outline-secondary"
                @click="cart.decreaseQuantity(item.id)"
              >
                −
              </button>

              <span class="mx-2">{{ item.quantity }}</span>

              <button
                class="btn btn-sm btn-outline-secondary"
                @click="cart.increaseQuantity(item.id)"
              >
                +
              </button>
            </div>

            <!-- SUBTOTAL -->
            <strong style="min-width: 90px; text-align: right">
              {{ formatPrice(item.price * item.quantity) }}
            </strong>

            <!-- REMOVE -->
            <button class="btn btn-sm btn-outline-danger" @click="cart.removeItem(item.id)">
              Remover
            </button>
          </div>
        </div>
      </div>

      <!-- SUMMARY -->
      <div class="col-lg-4">
        <div class="card shadow-sm p-3">
          <h5 class="mb-3">Resumo</h5>

          <!-- DELIVERY METHOD -->
          <div class="mb-3">
            <label class="form-label fw-semibold">Tipo de entrega</label>

            <select v-model="selectedDelivery">
              <option value="pickup">Retirada no local</option>
              <option value="delivery">Entrega</option>
            </select>

            <small
              v-if="cart.deliveryType === 'delivery' && !cart.canDeliver"
              class="text-danger d-block mt-1"
            >
              Pedido mínimo para entrega: {{ formatPrice(cart.minDeliveryTotal) }}
            </small>
          </div>

          <hr />

          <div class="d-flex justify-content-between mb-2">
            <span>Produtos</span>
            <span>{{ formatPrice(cart.productsTotal) }}</span>
          </div>

          <div
            v-if="cart.deliveryType === 'delivery' && cart.canDeliver"
            class="d-flex justify-content-between mb-2"
          >
            <span>Entrega</span>
            <span>{{ formatPrice(cart.deliveryFee) }}</span>
          </div>

          <hr />

          <div class="d-flex justify-content-between fw-bold mb-3">
            <span>Total</span>
            <span>{{ formatPrice(cart.finalTotal) }}</span>
          </div>

          <RouterLink
            to="/checkout"
            class="btn btn-success w-100"
            :class="{ disabled: cart.deliveryType === 'delivery' && !cart.canDeliver }"
          >
            Continuar
          </RouterLink>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { useCartStore } from '@/stores/cart'

const cart = useCartStore()

// LOCAL STATE SYNC WITH STORE
const selectedDelivery = ref(cart.deliveryType)

onMounted(() => {
  cart.loadFromStorage()
})

// SYNC DELIVERY TYPE
watch(selectedDelivery, (value) => {
  cart.setDeliveryType(value as 'pickup' | 'delivery')
})

function formatPrice(value: number) {
  return value.toLocaleString('pt-BR', {
    style: 'currency',
    currency: 'BRL',
  })
}
</script>
