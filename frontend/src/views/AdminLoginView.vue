<template>
  <section class="py-5">
    <div class="container">
      <div class="row justify-content-center">
        <div class="col-md-4">
          <div class="card shadow-sm">
            <div class="card-body">
              <h4 class="mb-3 text-center">Admin Login</h4>

              <form @submit.prevent="submit">
                <!-- USERNAME -->
                <input
                  type="text"
                  class="form-control mb-2"
                  placeholder="Usuário"
                  autocomplete="username"
                  v-model="username"
                  :disabled="loading"
                />

                <!-- PASSWORD -->
                <input
                  type="password"
                  class="form-control mb-2"
                  placeholder="Senha"
                  autocomplete="current-password"
                  v-model="password"
                  :disabled="loading"
                />

                <!-- SUBMIT -->
                <button class="btn btn-primary w-100" type="submit" :disabled="loading">
                  {{ loading ? 'Entrando...' : 'Entrar' }}
                </button>
              </form>

              <!-- ERROR -->
              <div v-if="errorMessage" class="alert alert-danger mt-3 mb-0">
                {{ errorMessage }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

/* ======================
   ROUTER / STORE
====================== */
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

/* ======================
   STATE
====================== */
const username = ref('')
const password = ref('')
const errorMessage = ref('')
const loading = ref(false)

/* ======================
   LOGIN
====================== */
// Credentials are verified by the API. There is no copy of them in this bundle,
// and the attempt limit is enforced server-side rather than in localStorage
// where anyone could clear it.
async function submit() {
  errorMessage.value = ''
  loading.value = true

  try {
    const failure = await auth.login(username.value, password.value)

    if (failure) {
      errorMessage.value = failure
      password.value = ''
      return
    }

    // Only follow same-origin relative paths, so a crafted ?redirect= cannot
    // bounce a freshly authenticated admin to another site.
    const redirect = route.query.redirect
    const target = typeof redirect === 'string' && redirect.startsWith('/') ? redirect : '/admin'

    router.push(target)
  } finally {
    loading.value = false
  }
}
</script>
