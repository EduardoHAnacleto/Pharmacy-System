<template>
  <section class="py-4">
    <div class="container">
      <!-- HEADER -->
      <div class="d-flex justify-content-between align-items-center mb-4">
        <h3>Administração de Promoções</h3>
        <div class="d-flex gap-2">
          <RouterLink class="btn btn-outline-primary btn-sm" to="/admin/insights">
            Insights
          </RouterLink>
          <button class="btn btn-outline-danger btn-sm" @click="logout">Sair</button>
        </div>
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

      <!-- MISSING IMAGES -->
      <div v-if="missingImageCount > 0" class="alert alert-warning">
        <strong>{{ missingImageCount }}</strong> promoção(ões) apontam para uma imagem que não está
        mais no disco. Reative-as com uma nova imagem para corrigir.
      </div>

      <!-- LIST -->
      <div class="card mb-4">
        <div class="card-body">
          <h5 class="mb-3">Promoções Cadastradas</h5>

          <table class="table table-striped align-middle">
            <thead>
              <tr>
                <th>Nome</th>
                <th>Preço</th>
                <th>Vigência</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="p in promotions" :key="p.id">
                <td>
                  {{ p.name }}
                  <span v-if="p.imageMissing" class="badge bg-warning text-dark ms-1">
                    sem imagem
                  </span>
                  <span v-if="p.sourcePromotionId" class="badge bg-light text-dark ms-1">
                    reativada de #{{ p.sourcePromotionId }}
                  </span>
                </td>
                <td>R$ {{ p.price.toFixed(2) }}</td>
                <td>{{ formatDate(p.dateStart) }} — {{ formatDate(p.dateEnd) }}</td>
                <td>
                  <span class="badge" :class="STATUS_BADGE_CLASS[p.status]">
                    {{ STATUS_LABELS[p.status] }}
                  </span>
                </td>
                <td class="text-end">
                  <!-- Archive, not delete: the row and its image survive so the
                       promotion can be run again later. -->
                  <button class="btn btn-sm btn-outline-secondary" @click="archive(p.id)">
                    Arquivar
                  </button>
                </td>
              </tr>
              <tr v-if="promotions.length === 0">
                <td colspan="5" class="text-muted text-center">Nenhuma promoção cadastrada</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- ARCHIVED LIBRARY -->
      <div class="card">
        <div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-3">
            <h5 class="mb-0">Arquivadas</h5>
            <button class="btn btn-sm btn-outline-primary" @click="loadArchived">Atualizar</button>
          </div>

          <p class="text-muted small">
            Promoções encerradas mantêm a imagem. Reativar cria uma nova promoção com a mesma arte e
            uma nova vigência, sem precisar subir a imagem de novo.
          </p>

          <table class="table table-sm align-middle">
            <thead>
              <tr>
                <th></th>
                <th>Nome</th>
                <th>Preço</th>
                <th>Arquivada em</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="p in archived" :key="p.id">
                <td style="width: 64px">
                  <img
                    v-if="!p.imageMissing"
                    :src="p.imageUrl"
                    :alt="p.name"
                    class="img-thumbnail"
                    style="max-height: 48px"
                  />
                  <span v-else class="badge bg-warning text-dark">sem imagem</span>
                </td>
                <td>{{ p.name }}</td>
                <td>R$ {{ p.price.toFixed(2) }}</td>
                <td>{{ p.archivedAt ? formatDate(p.archivedAt) : '—' }}</td>
                <td class="text-end">
                  <button
                    class="btn btn-sm btn-success"
                    :disabled="!reactivateDates.start || !reactivateDates.end"
                    @click="reactivate(p.id)"
                  >
                    Reativar
                  </button>
                </td>
              </tr>
              <tr v-if="archived.length === 0">
                <td colspan="5" class="text-muted text-center">Nenhuma promoção arquivada</td>
              </tr>
            </tbody>
          </table>

          <div class="row g-2 align-items-end">
            <div class="col-md-4">
              <label class="form-label small">Nova data inicial</label>
              <input type="date" class="form-control" v-model="reactivateDates.start" />
            </div>
            <div class="col-md-4">
              <label class="form-label small">Nova data final</label>
              <input type="date" class="form-control" v-model="reactivateDates.end" />
            </div>
            <div class="col-md-4 text-muted small">Informe a vigência antes de reativar.</div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import { useRouter } from 'vue-router'
import { usePromotionsStore } from '@/stores/promotions'
import type { PromotionCreatePayload } from '@/stores/promotions'
import { useAuthStore } from '@/stores/auth'
import { getCategories } from '@/services/itemPromotionService'
import { STATUS_BADGE_CLASS, STATUS_LABELS } from '@/types/itemPromotion'

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
const archived = computed(() => promotionsStore.archived)
const missingImageCount = computed(() => promotionsStore.missingImageCount)
const requestError = computed(() => promotionsStore.error)

/* ======================
   REACTIVATION
====================== */
const reactivateDates = ref({ start: '', end: '' })

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString('pt-BR')
}

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
    publish: isActive.value,
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

async function archive(id: number) {
  if (await promotionsStore.archivePromotion(id)) {
    await promotionsStore.loadArchived()
  }
}

async function loadArchived() {
  await promotionsStore.loadArchived()
}

async function reactivate(id: number) {
  const { start, end } = reactivateDates.value
  if (!start || !end) return

  const created = await promotionsStore.reactivatePromotion(id, {
    dateStart: new Date(start).toISOString(),
    dateEnd: new Date(end).toISOString(),
    publish: true,
  })

  if (created) {
    reactivateDates.value = { start: '', end: '' }
    await promotionsStore.loadArchived()
  }
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
  promotionsStore.loadArchived()
  promotionsStore.loadMissingImageCount()

  try {
    categories.value = await getCategories()
  } catch {
    // The form stays unusable without a category, and requestError already
    // reports save failures; nothing useful to add here.
    categories.value = []
  }
})
</script>
