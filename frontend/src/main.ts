import './assets/main.css'
import './assets/bootstrap-shop.css'
import { createPinia } from 'pinia'
import { createApp } from 'vue'
import { useCartStore } from '@/stores/cart'
import { useSettingsStore } from '@/stores/settings'
import { installUnloadFlush } from '@/services/analytics'
import i18n, { setLocale } from '@/i18n'
import App from './App.vue'
import router from './router'

import 'bootstrap/dist/css/bootstrap.min.css'
import 'bootstrap-icons/font/bootstrap-icons.css'
import 'bootstrap/dist/js/bootstrap.bundle.min.js'

const app = createApp(App)

app.use(createPinia())
app.use(i18n)
app.use(router)

const cart = useCartStore()
cart.loadFromStorage()

// Queued events are lost when the page unloads unless they are beaconed out,
// and the last event of a visit is usually the most interesting one.
installUnloadFlush()

// The shop's own configuration: name, branding, currency, delivery rules,
// opening hours. Started before mount but deliberately not awaited — blocking the
// first paint on a network round trip would show a blank page whenever the API is
// slow, whereas reactivity fills the real values in as soon as they land.
const settings = useSettingsStore()

void settings.load().then(() => setLocale(settings.settings.locale))

app.mount('#app')
