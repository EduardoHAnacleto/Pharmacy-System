import { defineStore } from 'pinia'
import axios from 'axios'
import api from '@/services/api'
import type { AuthResult } from '@/types/auth'

/**
 * Authentication state.
 *
 * The access token lives in memory only. Persisting it to localStorage would
 * expose it to any injected script, and it is not needed there: the refresh
 * token is held in an HttpOnly cookie the browser sends automatically, so a
 * page reload can recover the session via `restore()`.
 */
export const useAuthStore = defineStore('auth', {
  state: () => ({
    accessToken: null as string | null,
    username: null as string | null,
    role: null as string | null,
    /** True while an initial restore() is still in flight. */
    initialising: true,
  }),

  getters: {
    isAuthenticated: (state): boolean => state.accessToken !== null,
    isAdmin: (state): boolean => state.role === 'Admin',
  },

  actions: {
    apply(result: AuthResult) {
      this.accessToken = result.accessToken
      this.username = result.username
      this.role = result.role
    },

    clear() {
      this.accessToken = null
      this.username = null
      this.role = null
    },

    /**
     * Exchanges credentials for a session.
     * @returns an error message, or null on success.
     */
    async login(username: string, password: string): Promise<string | null> {
      try {
        const { data } = await api.post<AuthResult>('/auth/login', { username, password })
        this.apply(data)
        return null
      } catch (err: unknown) {
        this.clear()

        if (axios.isAxiosError(err)) {
          if (err.response?.status === 429) {
            return 'Muitas tentativas. Tente novamente em alguns minutos.'
          }
          if (err.response?.status === 401) {
            return 'Usuário ou senha incorretos.'
          }
        }

        return 'Não foi possível entrar. Tente novamente.'
      }
    },

    /**
     * Trades the refresh cookie for a new access token.
     * @returns true when a session was recovered.
     */
    async refresh(): Promise<boolean> {
      try {
        const { data } = await api.post<AuthResult>('/auth/refresh')
        this.apply(data)
        return true
      } catch {
        this.clear()
        return false
      }
    },

    /** Recovers a session on page load. Safe to call when not logged in. */
    async restore(): Promise<boolean> {
      try {
        return await this.refresh()
      } finally {
        this.initialising = false
      }
    },

    async logout() {
      try {
        await api.post('/auth/logout')
      } catch {
        // Revoking server-side is best effort; the local session goes either way.
      } finally {
        this.clear()
      }
    },
  },
})
