import { defineStore } from 'pinia'
import productService, { type Product } from '@/services/productService'

export const useProductsStore = defineStore('products', {
  state: () => ({
    products: [] as Product[],
    loading: false,
  }),

  actions: {
    async loadProducts() {
      this.loading = true
      try {
        this.products = await productService.getAll()
      } finally {
        this.loading = false
      }
    },
  },
})
