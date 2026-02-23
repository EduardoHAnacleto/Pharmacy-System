<template>
  <section class="py-5">
    <div class="container px-4 px-lg-5 mt-5">
      <div class="row gx-4 gx-lg-5 row-cols-2 row-cols-md-3 row-cols-xl-4 justify-content-center">
        <!-- PRODUCTS -->
        <div class="col mb-5" v-for="item in products" :key="item.id">
          <div class="card h-100">
            <img class="card-img-top" :src="item.imageUrl" alt="Imagem do produto" />

            <div class="card-body p-4">
              <div class="text-center">
                <h5 class="fw-bolder">{{ item.name }}</h5>

                <!-- PROMOTED -->
                <div v-if="item.priceBefore">
                  <span class="text-muted text-decoration-line-through">
                    R$ {{ item.priceBefore.toFixed(2) }}
                  </span>
                  <br />
                  <span class="fw-bold"> R$ {{ item.price.toFixed(2) }} </span>
                </div>

                <!-- NOT PROMOTED -->
                <div v-else>R$ {{ item.price.toFixed(2) }}</div>

                <!-- DURATION OF PROMOTION -->
                <small v-if="item.dateStart && item.dateEnd" class="text-muted d-block mt-2">
                  Promoção válida de
                  {{ formatDate(item.dateStart) }}
                  até
                  {{ formatDate(item.dateEnd) }}
                </small>
              </div>
            </div>

            <div class="card-footer p-4 pt-0 border-top-0 bg-transparent">
              <div class="text-center">
                <button class="btn btn-outline-dark mt-auto" @click="addToCart(item)">
                  Adicionar
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- EMPTY LIST -->
        <div v-if="products.length === 0" class="text-center text-muted py-5">
          Nenhum produto disponível
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useCartStore } from '@/stores/cart'

/**
 * DTO (GET)
 */
export interface Product {
  id: number
  name: string
  price: number
  priceBefore?: number | null
  imageUrl: string
  dateStart?: string
  dateEnd?: string
}

defineProps<{
  products: Product[]
}>()

const cartStore = useCartStore()

function addToCart(item: Product) {
  cartStore.addItem({
    id: item.id,
    name: item.name,
    price: item.price,
    imageUrl: item.imageUrl,
  })
}

/**
 * UTILITIES
 */
function formatDate(date: string): string {
  return new Date(date).toLocaleDateString('pt-BR')
}
</script>
