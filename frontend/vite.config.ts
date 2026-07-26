import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueJsx from '@vitejs/plugin-vue-jsx'
import vueDevTools from 'vite-plugin-vue-devtools'

// The app talks to the API through same-origin relative paths, which nginx
// proxies in the container image (see nginx.conf). The dev server has to
// mirror those three routes so the same code works under `npm run dev`.
const backendTarget = process.env.VITE_DEV_API_TARGET ?? 'http://localhost:5000'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), vueJsx(), vueDevTools()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      '/api': { target: backendTarget, changeOrigin: true },
      '/images': { target: backendTarget, changeOrigin: true },
      '/promotionsHub': { target: backendTarget, changeOrigin: true, ws: true },
    },
  },
})
