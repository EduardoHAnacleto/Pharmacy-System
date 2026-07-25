import axios from 'axios'

// Same-origin relative base URL. nginx proxies /api to the backend container in
// the built image, and vite.config.ts proxies it during development, so the
// bundle never needs to know the API's host or port.
const api = axios.create({
  baseURL: '/api',
})

export default api
