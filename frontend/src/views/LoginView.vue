<template>
  <section class="py-5">
    <div class="container" style="max-width: 420px">
      <div class="card shadow-sm">
        <div class="card-body">
          <h3 class="fw-bolder text-center mb-4">Login Admin</h3>

          <div class="mb-3">
            <label class="form-label">Usuário</label>
            <input v-model="username" class="form-control" />
          </div>

          <div class="mb-3">
            <label class="form-label">Senha</label>
            <input type="password" v-model="password" class="form-control" />
          </div>

          <div v-if="error" class="alert alert-danger">Usuário ou senha inválidos</div>

          <button class="btn btn-dark w-100" @click="handleLogin">Entrar</button>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'

const username = ref('')
const password = ref('')
const error = ref(false)

const router = useRouter()

function handleLogin() {
  error.value = false

  router.push('/admin')
}
</script>
