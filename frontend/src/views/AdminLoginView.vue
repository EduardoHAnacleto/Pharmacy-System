<template>
  <section class="py-5">
    <div class="container">
      <div class="row justify-content-center">
        <div class="col-md-4">
          <div class="card shadow-sm">
            <div class="card-body">
              <h4 class="mb-3 text-center">Admin Login</h4>

              <!-- USERNAME -->
              <input
                type="text"
                class="form-control mb-2"
                placeholder="Usuário"
                v-model="username"
                :disabled="isBlocked"
              />

              <!-- PASSWORD -->
              <input
                type="password"
                class="form-control mb-2"
                placeholder="Senha"
                v-model="password"
                :disabled="isBlocked"
              />

              <!-- SUBMIT -->
              <button class="btn btn-primary w-100" @click="login" :disabled="isBlocked">
                Entrar
              </button>

              <!-- ERROR -->
              <div v-if="loginError" class="alert alert-danger mt-3">
                Usuário ou senha incorretos.
              </div>

              <!-- BLOCKED -->
              <div v-if="isBlocked" class="alert alert-warning mt-3">
                Muitas tentativas inválidas.<br />
                Login bloqueado até:<br />
                <strong>{{ blockedUntilFormatted }}</strong>
              </div>

              <!-- DEV ONLY -->
              <button
                v-if="isDev && isBlocked"
                class="btn btn-sm btn-outline-secondary w-100 mt-3"
                @click="unlockLoginDev"
              >
                🔓 Desbloquear (DEV)
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'

/* ======================
   ROUTER
====================== */
const router = useRouter()

/* ======================
   DEV FLAG
====================== */
const isDev = import.meta.env.DEV

/* ======================
   CREDENTIALS (FRONT ONLY)
====================== */
const ADMIN_USERNAME = 'admin'
const ADMIN_PASSWORD = '1234'

/* ======================
   STORAGE KEYS
====================== */
const ATTEMPTS_KEY = 'admin_login_attempts'
const BLOCK_KEY = 'admin_login_block_until'

/* ======================
   STATE
====================== */
const username = ref('')
const password = ref('')
const loginError = ref(false)

const isBlocked = ref(false)
const blockedUntil = ref<number | null>(null)

/* ======================
   HELPERS
====================== */
function getAttempts(): number[] {
  return JSON.parse(localStorage.getItem(ATTEMPTS_KEY) || '[]')
}

function saveAttempts(attempts: number[]) {
  localStorage.setItem(ATTEMPTS_KEY, JSON.stringify(attempts))
}

function clearAttempts() {
  localStorage.removeItem(ATTEMPTS_KEY)
}

function setBlockedUntil(time: number) {
  localStorage.setItem(BLOCK_KEY, time.toString())
}

function clearBlock() {
  localStorage.removeItem(BLOCK_KEY)
}

/* ======================
   BLOCK CHECK
====================== */
function checkBlockStatus() {
  const value = localStorage.getItem(BLOCK_KEY)
  if (!value) return

  const time = Number(value)

  if (Date.now() < time) {
    isBlocked.value = true
    blockedUntil.value = time
  } else {
    clearBlock()
    clearAttempts()
    isBlocked.value = false
    blockedUntil.value = null
  }
}

/* ======================
   LOGIN
====================== */
function login() {
  loginError.value = false
  checkBlockStatus()

  if (isBlocked.value) return

  if (username.value === ADMIN_USERNAME && password.value === ADMIN_PASSWORD) {
    clearAttempts()
    clearBlock()

    // flag simples de auth
    localStorage.setItem('admin_authenticated', 'true')
    router.push('/admin')

    return
  }

  // INVALID LOGIN
  loginError.value = true

  const now = Date.now()
  const oneHour = 60 * 60 * 1000
  const threeHours = 3 * 60 * 60 * 1000

  let attempts = getAttempts()
  attempts = attempts.filter((t) => now - t < oneHour)
  attempts.push(now)
  saveAttempts(attempts)

  if (attempts.length >= 3) {
    const blockUntil = now + threeHours
    setBlockedUntil(blockUntil)
    isBlocked.value = true
    blockedUntil.value = blockUntil
  }
}

/* ======================
   DEV UNLOCK
====================== */
function unlockLoginDev() {
  clearBlock()
  clearAttempts()
  isBlocked.value = false
  blockedUntil.value = null
  loginError.value = false
}

/* ======================
   FORMAT
====================== */
const blockedUntilFormatted = computed(() => {
  if (!blockedUntil.value) return ''
  return new Date(blockedUntil.value).toLocaleString('pt-BR')
})

/* ======================
   LIFECYCLE
====================== */
onMounted(checkBlockStatus)
</script>
