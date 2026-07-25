import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'
import ContactView from '@/views/ContactView.vue'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: HomeView,
    },
    {
      path: '/contact',
      name: 'contact',
      component: ContactView,
    },
    {
      path: '/cart',
      name: 'cart',
      component: () => import('@/views/CartView.vue'),
    },
    {
      path: '/checkout',
      name: 'checkout',
      component: () => import('@/views/CheckoutView.vue'),
    },
    {
      path: '/login',
      component: () => import('@/views/AdminLoginView.vue'),
    },
    {
      path: '/admin',
      name: 'admin',
      component: () => import('@/views/AdminView.vue'),
      meta: { requiresAuth: true },
    },
  ],
})

router.beforeEach(async (to) => {
  if (!to.meta.requiresAuth) return true

  const auth = useAuthStore()

  // On a hard reload the in-memory token is gone, so try the refresh cookie
  // before deciding the visitor is anonymous.
  if (!auth.isAuthenticated) {
    await auth.restore()
  }

  // This guard only controls what the SPA renders. It is not the security
  // boundary — every write endpoint requires a valid token server-side, so
  // forging the client state reveals nothing and changes nothing.
  if (!auth.isAuthenticated || !auth.isAdmin) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }

  return true
})

export default router
