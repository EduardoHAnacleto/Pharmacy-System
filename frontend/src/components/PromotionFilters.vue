<template>
  <section class="py-3 border-bottom bg-body-tertiary">
    <div class="container px-4 px-lg-5">
      <form class="row g-2 align-items-end" role="search" @submit.prevent="emitFilter">
        <!-- SEARCH -->
        <div class="col-12 col-md-4">
          <label class="form-label small mb-1" for="filter-search">
            {{ t('filters.search') }}
          </label>
          <input
            id="filter-search"
            v-model="search"
            type="search"
            class="form-control"
            :placeholder="t('filters.searchPlaceholder')"
            maxlength="100"
          />
        </div>

        <!-- CATEGORY -->
        <div class="col-6 col-md-3">
          <label class="form-label small mb-1" for="filter-category">
            {{ t('filters.category') }}
          </label>
          <select id="filter-category" v-model="categoryId" class="form-select">
            <option :value="null">{{ t('filters.allCategories') }}</option>
            <option v-for="category in categories" :key="category.id" :value="category.id">
              {{ category.name }}
            </option>
          </select>
        </div>

        <!-- PRICE -->
        <div class="col-3 col-md-2">
          <label class="form-label small mb-1" for="filter-min">{{ t('filters.minPrice') }}</label>
          <input
            id="filter-min"
            v-model="minPrice"
            type="number"
            min="0"
            step="0.01"
            class="form-control"
          />
        </div>

        <div class="col-3 col-md-2">
          <label class="form-label small mb-1" for="filter-max">{{ t('filters.maxPrice') }}</label>
          <input
            id="filter-max"
            v-model="maxPrice"
            type="number"
            min="0"
            step="0.01"
            class="form-control"
          />
        </div>

        <!-- SORT -->
        <div class="col-6 col-md-2">
          <label class="form-label small mb-1" for="filter-sort">{{ t('filters.sort') }}</label>
          <select id="filter-sort" v-model="sort" class="form-select">
            <option v-for="option in SORT_OPTIONS" :key="option" :value="option">
              {{ t(`filters.sortOption.${option}`) }}
            </option>
          </select>
        </div>

        <div class="col-6 col-md-2 d-flex gap-2">
          <button class="btn btn-primary flex-grow-1" type="submit">
            {{ t('common.apply') }}
          </button>
          <button
            v-if="isFiltered"
            class="btn btn-outline-secondary"
            type="button"
            @click="clearFilter"
          >
            {{ t('filters.clear') }}
          </button>
        </div>
      </form>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { getCategories, type Category, type PromotionFilter } from '@/services/itemPromotionService'

const SORT_OPTIONS = ['endingSoon', 'priceAsc', 'priceDesc', 'newest', 'name'] as const

const { t } = useI18n()

const emit = defineEmits<{ change: [PromotionFilter] }>()

const categories = ref<Category[]>([])

const search = ref('')
const categoryId = ref<number | null>(null)
const minPrice = ref('')
const maxPrice = ref('')
const sort = ref<(typeof SORT_OPTIONS)[number]>('endingSoon')

const isFiltered = computed(
  () =>
    search.value.trim().length > 0 ||
    categoryId.value !== null ||
    minPrice.value !== '' ||
    maxPrice.value !== '' ||
    sort.value !== 'endingSoon',
)

onMounted(async () => {
  try {
    categories.value = await getCategories()
  } catch {
    // Without categories the dropdown offers only "all", which still leaves the
    // rest of the filter usable.
    categories.value = []
  }
})

/**
 * Search is debounced; the dropdowns apply immediately.
 *
 * Typing would otherwise issue a request per keystroke, while a dropdown change
 * is a deliberate single action and waiting on it just feels broken.
 */
let debounce: ReturnType<typeof setTimeout> | null = null

watch(search, () => {
  if (debounce) clearTimeout(debounce)
  debounce = setTimeout(emitFilter, 400)
})

watch([categoryId, sort], emitFilter)

function emitFilter() {
  if (debounce) {
    clearTimeout(debounce)
    debounce = null
  }

  emit('change', {
    search: search.value,
    categoryId: categoryId.value,
    minPrice: toNumber(minPrice.value),
    maxPrice: toNumber(maxPrice.value),
    sort: sort.value,
  })
}

function clearFilter() {
  search.value = ''
  categoryId.value = null
  minPrice.value = ''
  maxPrice.value = ''
  sort.value = 'endingSoon'

  emitFilter()
}

/** An empty or unparseable box means "no bound", not zero. */
function toNumber(value: string): number | null {
  if (value.trim() === '') return null

  const parsed = Number(value)

  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null
}
</script>
