import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'

// Same-origin relative base URL. nginx proxies /api to the backend container in
// the built image, and vite.config.ts proxies it during development, so the
// bundle never needs to know the API's host or port.
const api = axios.create({
  baseURL: '/api/v1',
  // Required for the HttpOnly refresh cookie to travel with /auth requests.
  withCredentials: true,
})

/** Requests that must never trigger a refresh attempt, to avoid a loop. */
const NO_RETRY_PATHS = ['/auth/login', '/auth/refresh', '/auth/logout']

interface RetryableConfig extends InternalAxiosRequestConfig {
  _retried?: boolean
}

// ===============================
// REQUEST: attach the access token
// ===============================
api.interceptors.request.use(async (config) => {
  // Imported lazily: the store imports this module, and resolving it at module
  // load time would be a circular import.
  const { useAuthStore } = await import('@/stores/auth')
  const auth = useAuthStore()

  if (auth.accessToken) {
    config.headers.Authorization = `Bearer ${auth.accessToken}`
  }

  return config
})

// ===============================
// RESPONSE: refresh once on 401
// ===============================
let refreshInFlight: Promise<boolean> | null = null

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetryableConfig | undefined

    const isAuthPath = NO_RETRY_PATHS.some((path) => config?.url?.startsWith(path))

    if (error.response?.status !== 401 || !config || config._retried || isAuthPath) {
      return Promise.reject(error)
    }

    config._retried = true

    const { useAuthStore } = await import('@/stores/auth')
    const auth = useAuthStore()

    // Concurrent 401s share a single refresh rather than each spending the
    // refresh token — rotation would invalidate all but the first.
    refreshInFlight ??= auth.refresh().finally(() => {
      refreshInFlight = null
    })

    const refreshed = await refreshInFlight

    if (!refreshed) {
      return Promise.reject(error)
    }

    return api.request(config)
  },
)

export default api
