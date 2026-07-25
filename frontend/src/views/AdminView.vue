<template>
  <section class="py-4">
    <div class="container">
      <!-- HEADER -->
      <div class="d-flex justify-content-between align-items-center mb-4">
        <h3>Administração de Promoções</h3>
        <button class="btn btn-outline-danger btn-sm" @click="logout">Sair</button>
      </div>

      <!-- FORM -->
      <div class="card mb-4">
        <div class="card-body">
          <h5 class="mb-3">Nova Promoção</h5>

          <div class="row">
            <div class="col-md-6 mb-2">
              <input class="form-control" placeholder="Nome" v-model="name" />
            </div>

            <div class="col-md-6 mb-2">
              <select class="form-select" v-model="categoryId">
                <option :value="null" disabled>Categoria</option>
                <option v-for="category in categories" :key="category.id" :value="category.id">
                  {{ category.name }}
                </option>
              </select>
            </div>

            <div class="col-md-3 mb-2">
              <input
                type="number"
                class="form-control"
                placeholder="Preço"
                v-model.number="price"
              />
            </div>

            <div class="col-md-3 mb-2">
              <input
                type="number"
                class="form-control"
                placeholder="Preço Antes"
                v-model.number="priceBefore"
              />
            </div>

            <div class="col-md-6 mb-2">
              <input type="date" class="form-control" v-model="dateStart" />
            </div>

            <div class="col-md-6 mb-2">
              <input type="date" class="form-control" v-model="dateEnd" />
            </div>

            <div class="col-md-6 mb-2">
              <input type="file" class="form-control" @change="handleImageUpload" />
            </div>

            <div class="col-md-6 mb-2 d-flex align-items-center">
              <img
                v-if="imagePreview"
                :src="imagePreview"
                class="img-thumbnail"
                style="max-height: 400px"
              />
            </div>
          </div>

          <div class="form-check my-2">
            <input class="form-check-input" type="checkbox" v-model="isActive" />
            <label class="form-check-label">Ativa</label>
          </div>

          <button class="btn btn-success mt-2" @click="addPromotion">Salvar Promoção</button>

          <div v-if="formError" class="alert alert-danger mt-3">
            Preencha todos os campos corretamente.
          </div>

          <div v-if="requestError" class="alert alert-danger mt-3">
            {{ requestError }}
          </div>
        </div>
      </div>

      <!-- LIST -->
      <div class="card">
        <div class="card-body">
          <h5 class="mb-3">Promoções Cadastradas</h5>

          <table class="table table-striped">
            <thead>
              <tr>
                <th>Nome</th>
                <th>Preço</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="p in promotions" :key="p.id">
                <td>{{ p.name }}</td>
                <td>R$ {{ p.price.toFixed(2) }}</td>
                <td>
                  <span class="badge" :class="p.isActive ? 'bg-success' : 'bg-secondary'">
                    {{ p.isActive ? 'Ativa' : 'Inativa' }}
                  </span>
                </td>
                <td class="text-end">
                  <button class="btn btn-sm btn-danger" @click="removePromotion(p.id)">
                    Remover
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { usePromotionsStore } from '@/stores/promotions'
import type { PromotionCreatePayload } from '@/stores/promotions'
import { useAuthStore } from '@/stores/auth'
import { getCategories } from '@/services/itemPromotionService'

/* ======================
   ROUTER / STORE
====================== */
const router = useRouter()
const promotionsStore = usePromotionsStore()
const authStore = useAuthStore()

/* ======================
   FORM STATE
====================== */
const name = ref('')
const price = ref<number | null>(null)
const priceBefore = ref<number | null>(null)
const dateStart = ref('')
const dateEnd = ref('')
const isActive = ref(true)
const categoryId = ref<number | null>(null)
const formError = ref(false)

/* ======================
   CATEGORIES
====================== */
const categories = ref<{ id: number; name: string }[]>([])

/* ======================
   IMAGE STATE
====================== */
const imageFile = ref<File | null>(null)
const imagePreview = ref<string | undefined>(undefined)

/* ======================
   DATA
====================== */
const promotions = computed(() => promotionsStore.promotions)
const requestError = computed(() => promotionsStore.error)

/* ======================
   IMAGE
====================== */
function handleImageUpload(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  imageFile.value = file

  const reader = new FileReader()
  reader.onload = () => {
    imagePreview.value = reader.result as string
  }
  reader.readAsDataURL(file)
}

/* ======================
   VALIDATION
====================== */
const isFormValid = computed(() => {
  if (
    !name.value ||
    price.value === null ||
    priceBefore.value === null ||
    categoryId.value === null ||
    !imageFile.value ||
    !dateStart.value ||
    !dateEnd.value
  ) {
    return false
  }

  return price.value < priceBefore.value
})

/* ======================
   ACTIONS
====================== */
async function addPromotion() {
  formError.value = false

  if (!isFormValid.value) {
    formError.value = true
    return
  }

  const payload: PromotionCreatePayload = {
    name: name.value,
    price: price.value!,
    priceBefore: priceBefore.value!,
    image: imageFile.value!,
    dateStart: dateStart.value,
    dateEnd: dateEnd.value,
    isActive: isActive.value,
    categoryId: categoryId.value!,
    productType: 'default',
  }

  // Only clear the form when the promotion actually reached the server,
  // otherwise a failed save silently discards everything the user typed.
  const saved = await promotionsStore.addPromotion(payload)
  if (saved) {
    resetForm()
  }
}

async function removePromotion(id: number) {
  await promotionsStore.removePromotion(id)
}

/* ======================
   RESET
====================== */
function resetForm() {
  name.value = ''
  price.value = null
  priceBefore.value = null
  dateStart.value = ''
  dateEnd.value = ''
  isActive.value = true
  categoryId.value = null
  imageFile.value = null
  imagePreview.value = undefined
}

/* ======================
   LOGOUT
====================== */
async function logout() {
  // Revokes the refresh token server-side as well, so the cookie cannot be
  // replayed for a new session.
  await authStore.logout()
  router.push('/login')
}

/* ======================
   LIFECYCLE
====================== */
onMounted(async () => {
  promotionsStore.loadPromotions()

  try {
    categories.value = await getCategories()
  } catch {
    // The form stays unusable without a category, and requestError already
    // reports save failures; nothing useful to add here.
    categories.value = []
  }
})
</script>
