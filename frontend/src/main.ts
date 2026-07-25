import './assets/main.css'
import './assets/bootstrap-shop.css'
import { createPinia } from 'pinia'
import { createApp } from 'vue'
import { useCartStore } from '@/stores/cart'
import { installUnloadFlush } from '@/services/analytics'
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

// Queued events are lost when the page unloads unless they are beaconed out,
// and the last event of a visit is usually the most interesting one.
installUnloadFlush()

app.mount('#app')
