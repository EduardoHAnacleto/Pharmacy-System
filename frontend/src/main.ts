import './assets/main.css'
import './assets/bootstrap-shop.css'
import { createPinia } from 'pinia'
import { createApp } from 'vue'
import { useCartStore } from '@/stores/cart'
import App from './App.vue'
import router from './router'

import 'bootstrap/dist/css/bootstrap.min.css'
import 'bootstrap-icons/font/bootstrap-icons.css'
import 'bootstrap/dist/js/bootstrap.bundle.min.js'

const app = createApp(App)

app.use(createPinia())
app.use(router)

const cart = useCartStore()
cart.loadFromStorage()

app.mount('#app')
