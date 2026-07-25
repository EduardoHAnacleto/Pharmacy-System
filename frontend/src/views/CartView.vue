<template>
  <section class="container py-5">
    <h2 class="fw-bolder mb-4">{{ t('cart.title') }}</h2>

    <!-- EMPTY CART -->
    <div v-if="cart.items.length === 0" class="text-center py-5">
      <p class="text-muted mb-3">{{ t('cart.empty') }}</p>
      <RouterLink to="/" class="btn btn-outline-primary">{{ t('cart.backToStore') }}</RouterLink>
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
                {{ formatMoney(item.price) }}
              </small>
            </div>
          </div>

          <div class="d-flex align-items-center gap-3">
            <!-- QUANTITY -->
            <div class="d-flex align-items-center">
              <button
                class="btn btn-sm btn-outline-secondary"
                :aria-label="t('cart.decrease')"
                @click="cart.decreaseQuantity(item.id)"
              >
                −
              </button>

              <span class="mx-2">{{ item.quantity }}</span>

              <button
                class="btn btn-sm btn-outline-secondary"
                :aria-label="t('cart.increase')"
                @click="cart.increaseQuantity(item.id)"
              >
                +
              </button>
            </div>

            <!-- SUBTOTAL -->
            <strong style="min-width: 90px; text-align: right">
              {{ formatMoney(item.price * item.quantity) }}
            </strong>

            <!-- REMOVE -->
            <button class="btn btn-sm btn-outline-danger" @click="cart.removeItem(item.id)">
              {{ t('common.remove') }}
            </button>
          </div>
        </div>
      </div>

      <!-- SUMMARY -->
      <div class="col-lg-4">
        <div class="card shadow-sm p-3">
          <h5 class="mb-3">{{ t('cart.summary') }}</h5>

          <!-- FULFILMENT -->
          <div class="mb-3">
            <label class="form-label fw-semibold" for="fulfillment">
              {{ t('cart.fulfillment') }}
            </label>

            <!-- Only the options the shop offers: a pick-up-only shop must not be
                 able to receive a delivery order it cannot fulfil. -->
            <select id="fulfillment" v-model="selectedDelivery" class="form-select">
              <option v-if="cart.pickupEnabled" value="pickup">{{ t('cart.pickup') }}</option>
              <option v-if="cart.deliveryEnabled" value="delivery">{{ t('cart.delivery') }}</option>
            </select>

            <small
              v-if="cart.deliveryType === 'delivery' && !cart.canDeliver"
              class="text-danger d-block mt-1"
            >
              {{ t('cart.minimumForDelivery', { amount: formatMoney(cart.minDeliveryTotal) }) }}
            </small>
          </div>

          <hr />

          <div class="d-flex justify-content-between mb-2">
            <span>{{ t('cart.products') }}</span>
            <span>{{ formatMoney(cart.productsTotal) }}</span>
          </div>

          <div
            v-if="cart.deliveryType === 'delivery' && cart.canDeliver"
            class="d-flex justify-content-between mb-2"
          >
            <span>{{ t('cart.deliveryFee') }}</span>
            <span>{{ formatMoney(cart.deliveryFee) }}</span>
          </div>

          <hr />

          <div class="d-flex justify-content-between fw-bold mb-3">
            <span>{{ t('common.total') }}</span>
            <span>{{ formatMoney(cart.finalTotal) }}</span>
          </div>

          <RouterLink
            to="/checkout"
            class="btn btn-success w-100"
            :class="{ disabled: cart.deliveryType === 'delivery' && !cart.canDeliver }"
          >
            {{ t('cart.continue') }}
          </RouterLink>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useCartStore } from '@/stores/cart'
import { track } from '@/services/analytics'
import { formatMoney } from '@/utils/format'

const { t } = useI18n()
const cart = useCartStore()

// LOCAL STATE SYNC WITH STORE
const selectedDelivery = ref(cart.deliveryType)

onMounted(() => {
  cart.loadFromStorage()
  selectedDelivery.value = cart.deliveryType

  if (cart.items.length > 0) track('cart_view')
})

// SYNC DELIVERY TYPE
watch(selectedDelivery, (value) => {
  cart.setDeliveryType(value as 'pickup' | 'delivery')
})

// A stored cart may hold a fulfilment type the shop has since turned off.
watch(
  () => [cart.pickupEnabled, cart.deliveryEnabled] as const,
  ([pickup, delivery]) => {
    if (cart.deliveryType === 'delivery' && !delivery && pickup) {
      selectedDelivery.value = 'pickup'
    } else if (cart.deliveryType === 'pickup' && !pickup && delivery) {
      selectedDelivery.value = 'delivery'
    }
  },
  { immediate: true },
)
</script>
