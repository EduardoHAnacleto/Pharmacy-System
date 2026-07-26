<template>
  <section class="py-4">
    <div class="container">
      <!-- HEADER -->
      <div class="d-flex justify-content-between align-items-center mb-4">
        <h3>{{ t('admin.title') }}</h3>
        <div class="d-flex gap-2">
          <RouterLink class="btn btn-outline-primary btn-sm" to="/admin/insights">
            {{ t('nav.insights') }}
          </RouterLink>
          <RouterLink class="btn btn-outline-primary btn-sm" to="/admin/settings">
            {{ t('nav.settings') }}
          </RouterLink>
          <button class="btn btn-outline-danger btn-sm" @click="logout">
            {{ t('admin.logout') }}
          </button>
        </div>
      </div>

      <!-- FORM -->
      <div class="card mb-4">
        <div class="card-body">
          <h5 class="mb-3">
            {{ editingId ? t('admin.editPromotion', { id: editingId }) : t('admin.newPromotion') }}
          </h5>

          <div class="row">
            <div class="col-md-6 mb-2">
              <label class="form-label small" for="promotion-name">
                {{ t('admin.promotionName') }}
              </label>
              <input id="promotion-name" class="form-control" v-model="form.name" maxlength="100" />
            </div>

            <div class="col-md-6 mb-2">
              <label class="form-label small" for="promotion-category">
                {{ t('admin.category') }}
              </label>
              <select id="promotion-category" class="form-select" v-model="form.categoryId">
                <option :value="null" disabled>{{ t('common.select') }}</option>
                <option v-for="category in categories" :key="category.id" :value="category.id">
                  {{ category.name }}
                </option>
              </select>
            </div>

            <div class="col-md-3 mb-2">
              <label class="form-label small" for="promotion-price">{{ t('admin.price') }}</label>
              <input
                id="promotion-price"
                type="number"
                step="0.01"
                min="0"
                class="form-control"
                v-model.number="form.price"
              />
            </div>

            <div class="col-md-3 mb-2">
              <label class="form-label small" for="promotion-price-before">
                {{ t('admin.priceBefore') }}
              </label>
              <input
                id="promotion-price-before"
                type="number"
                step="0.01"
                min="0"
                class="form-control"
                v-model.number="form.priceBefore"
              />
            </div>

            <div class="col-md-3 mb-2">
              <label class="form-label small" for="promotion-start">
                {{ t('admin.dateStart') }}
              </label>
              <input
                id="promotion-start"
                type="date"
                class="form-control"
                v-model="form.dateStart"
              />
            </div>

            <div class="col-md-3 mb-2">
              <label class="form-label small" for="promotion-end">{{ t('admin.dateEnd') }}</label>
              <input id="promotion-end" type="date" class="form-control" v-model="form.dateEnd" />
            </div>

            <!-- Hidden while editing: the image belongs to the media asset and is
                 replaced through reactivation, not through an update. -->
            <div v-if="!editingId" class="col-md-6 mb-2">
              <label class="form-label small" for="promotion-image">{{ t('admin.image') }}</label>
              <input
                id="promotion-image"
                type="file"
                accept="image/*"
                class="form-control"
                @change="handleImageUpload"
              />
            </div>

            <div class="col-md-6 mb-2 d-flex align-items-center">
              <img
                v-if="imagePreview"
                :src="imagePreview"
                :alt="form.name || t('admin.image')"
                class="img-thumbnail"
                style="max-height: 400px"
              />
            </div>
          </div>

          <div class="form-check my-2">
            <input
              id="promotion-publish"
              class="form-check-input"
              type="checkbox"
              v-model="form.publish"
            />
            <label class="form-check-label" for="promotion-publish">{{ t('admin.publish') }}</label>
          </div>

          <div class="d-flex gap-2 mt-2">
            <button class="btn btn-success" @click="submit">
              {{ t('admin.savePromotion') }}
            </button>
            <button v-if="editingId" class="btn btn-outline-secondary" @click="cancelEdit">
              {{ t('common.cancel') }}
            </button>
          </div>

          <div v-if="formError" class="alert alert-danger mt-3">
            {{ t('admin.formInvalid') }}
          </div>

          <div v-if="requestError" class="alert alert-danger mt-3">
            {{ requestError }}
          </div>
        </div>
      </div>

      <!-- MISSING IMAGES -->
      <div v-if="missingImageCount > 0" class="alert alert-warning">
        {{ t('admin.missingImages', { count: missingImageCount }) }}
      </div>

      <!-- LIST -->
      <div class="card mb-4">
        <div class="card-body">
          <h5 class="mb-3">{{ t('admin.registered') }}</h5>

          <div class="table-responsive">
            <table class="table table-striped align-middle">
              <thead>
                <tr>
                  <th>{{ t('admin.promotionName') }}</th>
                  <th>{{ t('admin.price') }}</th>
                  <th>{{ t('admin.validity') }}</th>
                  <th>{{ t('admin.status') }}</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="p in promotions" :key="p.id">
                  <td>
                    {{ p.name }}
                    <span v-if="p.imageMissing" class="badge bg-warning text-dark ms-1">
                      {{ t('admin.noImage') }}
                    </span>
                    <span v-if="p.sourcePromotionId" class="badge bg-light text-dark ms-1">
                      {{ t('admin.reactivatedFrom', { id: p.sourcePromotionId }) }}
                    </span>
                  </td>
                  <td>{{ formatMoney(p.price) }}</td>
                  <td>{{ formatDate(p.dateStart) }} — {{ formatDate(p.dateEnd) }}</td>
                  <td>
                    <span class="badge" :class="STATUS_BADGE_CLASS[p.status]">
                      {{ STATUS_LABELS[p.status] }}
                    </span>
                  </td>
                  <td class="text-end">
                    <div class="d-flex gap-1 justify-content-end">
                      <button class="btn btn-sm btn-outline-primary" @click="startEdit(p)">
                        {{ t('admin.edit') }}
                      </button>
                      <button
                        class="btn btn-sm btn-outline-secondary"
                        :disabled="!hasNewWindow"
                        :title="hasNewWindow ? undefined : t('admin.windowRequired')"
                        @click="duplicate(p.id)"
                      >
                        {{ t('admin.duplicate') }}
                      </button>
                      <!-- Archive, not delete: the row and its image survive so the
                           promotion can be run again later. -->
                      <button class="btn btn-sm btn-outline-danger" @click="archive(p.id)">
                        {{ t('admin.archive') }}
                      </button>
                    </div>
                  </td>
                </tr>
                <tr v-if="promotions.length === 0">
                  <td colspan="5" class="text-muted text-center">{{ t('admin.noPromotions') }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- ARCHIVED LIBRARY -->
      <div class="card mb-4">
        <div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-3">
            <h5 class="mb-0">{{ t('admin.archivedTitle') }}</h5>
            <button class="btn btn-sm btn-outline-primary" @click="loadArchived">
              {{ t('admin.refresh') }}
            </button>
          </div>

          <p class="text-muted small">{{ t('admin.archivedIntro') }}</p>

          <div class="table-responsive">
            <table class="table table-sm align-middle">
              <thead>
                <tr>
                  <th></th>
                  <th>{{ t('admin.promotionName') }}</th>
                  <th>{{ t('admin.price') }}</th>
                  <th>{{ t('admin.archivedAt') }}</th>
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
                    <span v-else class="badge bg-warning text-dark">{{ t('admin.noImage') }}</span>
                  </td>
                  <td>{{ p.name }}</td>
                  <td>{{ formatMoney(p.price) }}</td>
                  <td>{{ formatDate(p.archivedAt) }}</td>
                  <td class="text-end">
                    <button
                      class="btn btn-sm btn-success"
                      :disabled="!hasNewWindow"
                      @click="reactivate(p.id)"
                    >
                      {{ t('admin.reactivate') }}
                    </button>
                  </td>
                </tr>
                <tr v-if="archived.length === 0">
                  <td colspan="5" class="text-muted text-center">{{ t('admin.noArchived') }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="row g-2 align-items-end">
            <div class="col-md-4">
              <label class="form-label small" for="reactivate-start">
                {{ t('admin.newDateStart') }}
              </label>
              <input
                id="reactivate-start"
                type="date"
                class="form-control"
                v-model="reactivateDates.start"
              />
            </div>
            <div class="col-md-4">
              <label class="form-label small" for="reactivate-end">
                {{ t('admin.newDateEnd') }}
              </label>
              <input
                id="reactivate-end"
                type="date"
                class="form-control"
                v-model="reactivateDates.end"
              />
            </div>
            <div class="col-md-4 text-muted small">{{ t('admin.windowRequired') }}</div>
          </div>
        </div>
      </div>

      <!-- CATEGORIES -->
      <div class="card">
        <div class="card-body">
          <h5 class="mb-3">{{ t('admin.categoriesTitle') }}</h5>
          <p class="text-muted small">{{ t('admin.categoriesIntro') }}</p>

          <div v-if="categoryError" class="alert alert-danger">{{ categoryError }}</div>

          <div class="row g-2 align-items-end mb-3">
            <div class="col-md-6">
              <label class="form-label small" for="new-category">
                {{ t('admin.newCategory') }}
              </label>
              <input
                id="new-category"
                v-model="newCategoryName"
                class="form-control"
                maxlength="30"
                @keyup.enter="addCategory"
              />
            </div>
            <div class="col-md-3">
              <button
                class="btn btn-primary w-100"
                :disabled="newCategoryName.trim().length === 0"
                @click="addCategory"
              >
                {{ t('admin.add') }}
              </button>
            </div>
          </div>

          <ul class="list-group">
            <li
              v-for="category in categories"
              :key="category.id"
              class="list-group-item d-flex align-items-center gap-2"
            >
              <input
                v-model="categoryDrafts[category.id]"
                class="form-control form-control-sm"
                maxlength="30"
                :aria-label="t('admin.category')"
              />
              <button
                class="btn btn-sm btn-outline-primary"
                :disabled="!isRenamed(category)"
                @click="rename(category)"
              >
                {{ t('admin.rename') }}
              </button>
              <button class="btn btn-sm btn-outline-danger" @click="remove(category)">
                {{ t('admin.deleteCategory') }}
              </button>
            </li>
            <li v-if="categories.length === 0" class="list-group-item text-muted text-center">
              {{ t('admin.noCategories') }}
            </li>
          </ul>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { usePromotionsStore } from '@/stores/promotions'
import type { PromotionCreatePayload, PromotionUpdatePayload } from '@/stores/promotions'
import { useAuthStore } from '@/stores/auth'
import {
  createCategory,
  deleteCategory,
  getCategories,
  renameCategory,
  type Category,
} from '@/services/itemPromotionService'
import { formatDate, formatMoney } from '@/utils/format'
import { STATUS_BADGE_CLASS, STATUS_LABELS, type ItemPromotion } from '@/types/itemPromotion'

/* ======================
   ROUTER / STORE
====================== */
const { t } = useI18n()
const router = useRouter()
const promotionsStore = usePromotionsStore()
const authStore = useAuthStore()

/* ======================
   FORM STATE
====================== */
const form = reactive({
  name: '',
  price: null as number | null,
  priceBefore: null as number | null,
  dateStart: '',
  dateEnd: '',
  publish: true,
  categoryId: null as number | null,
})

const editingId = ref<number | null>(null)
const formError = ref(false)

/* ======================
   IMAGE STATE
====================== */
const imageFile = ref<File | null>(null)
const imagePreview = ref<string | undefined>(undefined)

/* ======================
   DATA
====================== */
const categories = ref<Category[]>([])
const promotions = computed(() => promotionsStore.promotions)
const archived = computed(() => promotionsStore.archived)
const missingImageCount = computed(() => promotionsStore.missingImageCount)
const requestError = computed(() => promotionsStore.error)

/* ======================
   REACTIVATION / DUPLICATION
====================== */
const reactivateDates = ref({ start: '', end: '' })

const hasNewWindow = computed(
  () => reactivateDates.value.start !== '' && reactivateDates.value.end !== '',
)

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
    !form.name ||
    form.price === null ||
    form.priceBefore === null ||
    form.categoryId === null ||
    !form.dateStart ||
    !form.dateEnd
  ) {
    return false
  }

  // An image is required to create, but not to edit: an update leaves the
  // existing media asset alone.
  if (!editingId.value && !imageFile.value) return false

  return form.price < form.priceBefore
})

/* ======================
   ACTIONS
====================== */
async function submit() {
  formError.value = false

  if (!isFormValid.value) {
    formError.value = true
    return
  }

  const saved = editingId.value ? await saveEdit(editingId.value) : await create()

  // Only clear the form when the promotion actually reached the server,
  // otherwise a failed save silently discards everything the user typed.
  if (saved) resetForm()
}

async function create() {
  const payload: PromotionCreatePayload = {
    name: form.name,
    price: form.price!,
    priceBefore: form.priceBefore!,
    image: imageFile.value!,
    dateStart: form.dateStart,
    dateEnd: form.dateEnd,
    publish: form.publish,
    categoryId: form.categoryId!,
    productType: 'default',
  }

  return promotionsStore.addPromotion(payload)
}

async function saveEdit(id: number) {
  const payload: PromotionUpdatePayload = {
    name: form.name,
    price: form.price!,
    priceBefore: form.priceBefore!,
    dateStart: new Date(form.dateStart).toISOString(),
    dateEnd: new Date(form.dateEnd).toISOString(),
    publish: form.publish,
    categoryId: form.categoryId!,
    productType: 'default',
  }

  return promotionsStore.updatePromotion(id, payload)
}

/**
 * Loads a promotion into the form.
 *
 * Correcting a wrong price used to mean archiving and recreating, which lost the
 * promotion's own history and forced a re-upload of the image.
 */
function startEdit(promotion: ItemPromotion) {
  editingId.value = promotion.id

  form.name = promotion.name
  form.price = promotion.price
  form.priceBefore = promotion.priceBefore
  form.dateStart = dateInputValue(promotion.dateStart)
  form.dateEnd = dateInputValue(promotion.dateEnd)
  form.publish = promotion.status !== 'Draft'
  form.categoryId = promotion.categoryId ?? null

  imageFile.value = null
  imagePreview.value = promotion.imageMissing ? undefined : promotion.imageUrl

  window.scrollTo({ top: 0, behavior: 'smooth' })
}

function cancelEdit() {
  resetForm()
}

/** `<input type="date">` accepts only `yyyy-MM-dd`. */
function dateInputValue(value: string): string {
  const date = new Date(value)

  return Number.isNaN(date.getTime()) ? '' : date.toISOString().slice(0, 10)
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
  const payload = windowPayload()
  if (!payload) return

  if (await promotionsStore.reactivatePromotion(id, payload)) {
    reactivateDates.value = { start: '', end: '' }
    await promotionsStore.loadArchived()
  }
}

async function duplicate(id: number) {
  const payload = windowPayload()
  if (!payload) return

  if (await promotionsStore.duplicatePromotion(id, payload)) {
    reactivateDates.value = { start: '', end: '' }
  }
}

function windowPayload() {
  const { start, end } = reactivateDates.value
  if (!start || !end) return null

  return {
    dateStart: new Date(start).toISOString(),
    dateEnd: new Date(end).toISOString(),
    publish: true,
  }
}

/* ======================
   CATEGORIES
====================== */
const newCategoryName = ref('')
const categoryDrafts = reactive<Record<number, string>>({})
const categoryError = ref<string | null>(null)

function isRenamed(category: Category): boolean {
  const draft = categoryDrafts[category.id]?.trim() ?? ''

  return draft.length > 0 && draft !== category.name
}

async function addCategory() {
  const name = newCategoryName.value.trim()
  if (!name) return

  categoryError.value = null

  try {
    await createCategory(name)
    newCategoryName.value = ''
    await loadCategories()
  } catch (err) {
    categoryError.value = describe(err)
  }
}

async function rename(category: Category) {
  categoryError.value = null

  try {
    await renameCategory(category.id, categoryDrafts[category.id]!.trim())
    await loadCategories()
  } catch (err) {
    categoryError.value = describe(err)
  }
}

async function remove(category: Category) {
  if (!window.confirm(t('admin.confirmDeleteCategory', { name: category.name }))) return

  categoryError.value = null

  try {
    await deleteCategory(category.id)
    await loadCategories()
  } catch (err) {
    // The API refuses to delete a category still in use and says how many
    // promotions are in the way, which is exactly what the operator needs.
    categoryError.value = describe(err)
  }
}

async function loadCategories() {
  try {
    categories.value = await getCategories()

    for (const category of categories.value) {
      categoryDrafts[category.id] = category.name
    }
  } catch {
    // The promotion form stays unusable without a category, and requestError
    // already reports save failures; nothing useful to add here.
    categories.value = []
  }
}

function describe(err: unknown): string {
  const data = (err as { response?: { data?: unknown } }).response?.data

  return typeof data === 'string' && data.length > 0 ? data : t('admin.saveFailed')
}

/* ======================
   RESET
====================== */
function resetForm() {
  editingId.value = null
  formError.value = false

  form.name = ''
  form.price = null
  form.priceBefore = null
  form.dateStart = ''
  form.dateEnd = ''
  form.publish = true
  form.categoryId = null

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

  await loadCategories()
})
</script>
