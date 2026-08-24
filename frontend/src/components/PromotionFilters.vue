<template>
  <section class="py-3 border-bottom bg-body-tertiary">
    <div class="container px-4 px-lg-5">
      <!-- CATEGORIES -->
      <!--
        A row of chips rather than a <select>. Category is the one filter a
        pharmacy customer actually reaches for, and a dropdown hides every
        option behind a tap that first asks them to guess what is inside.
      -->
      <div
        v-if="categories.length > 0"
        class="chips"
        role="group"
        :aria-label="t('filters.category')"
      >
        <button
          type="button"
          class="chip"
          :class="{ 'chip--on': categoryId === null }"
          :aria-pressed="categoryId === null"
          @click="selectCategory(null)"
        >
          {{ t('filters.allCategories') }}
        </button>
        <button
          v-for="category in categories"
          :key="category.id"
          type="button"
          class="chip"
          :class="{ 'chip--on': categoryId === category.id }"
          :aria-pressed="categoryId === category.id"
          @click="selectCategory(category.id)"
        >
          {{ category.name }}
        </button>
      </div>

      <form class="filter-row" role="search" @submit.prevent="emitFilter">
        <label class="visually-hidden" for="filter-search">{{ t('filters.search') }}</label>
        <input
          id="filter-search"
          v-model="search"
          type="search"
          class="form-control"
          :placeholder="t('filters.searchPlaceholder')"
          maxlength="100"
        />

        <!--
          Price and sort move behind a disclosure. Six controls held 244px of a
          phone's first screen to serve the two that get used; the rest are for
          the visitor who already knows exactly what they want, and they can ask.
        -->
        <details class="more" ref="moreEl">
          <summary class="more-summary">{{ t('filters.more') }}</summary>

          <div class="more-panel">
            <div class="more-field">
              <label class="form-label small mb-1" for="filter-min">
                {{ t('filters.minPrice') }}
              </label>
              <input
                id="filter-min"
                v-model="minPrice"
                type="number"
                min="0"
                step="0.01"
                class="form-control"
              />
            </div>

            <div class="more-field">
              <label class="form-label small mb-1" for="filter-max">
                {{ t('filters.maxPrice') }}
              </label>
              <input
                id="filter-max"
                v-model="maxPrice"
                type="number"
                min="0"
                step="0.01"
                class="form-control"
              />
            </div>

            <div class="more-field">
              <label class="form-label small mb-1" for="filter-sort">
                {{ t('filters.sort') }}
              </label>
              <select id="filter-sort" v-model="sort" class="form-select">
                <option v-for="option in SORT_OPTIONS" :key="option" :value="option">
                  {{ t(`filters.sortOption.${option}`) }}
                </option>
              </select>
            </div>
          </div>
        </details>

        <button
          v-if="isFiltered"
          class="btn btn-outline-secondary"
          type="button"
          @click="clearFilter"
        >
          {{ t('filters.clear') }}
        </button>
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

const moreEl = ref<HTMLDetailsElement | null>(null)

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
    // Without categories the chip row is simply not rendered, which leaves
    // search and the disclosure working.
    categories.value = []
  }
})

/**
 * Everything applies on its own; there is no Apply button any more.
 *
 * Search and the price boxes are typed into, so they wait for a pause. The chips
 * and the sort dropdown are single deliberate actions, and making those wait
 * reads as the page ignoring the tap.
 *
 * The old form had an Apply button that only ever mattered for the two price
 * boxes — search and the dropdowns already applied themselves — so it spent a
 * slot on the least used control in the row.
 */
let debounce: ReturnType<typeof setTimeout> | null = null

function debounced() {
  if (debounce) clearTimeout(debounce)
  debounce = setTimeout(emitFilter, 400)
}

watch(search, debounced)
watch([minPrice, maxPrice], debounced)
watch([categoryId, sort], () => emitFilter())

function selectCategory(id: number | null) {
  categoryId.value = id
}

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

  // Collapse the disclosure too: leaving it open with nothing in it invites the
  // reader to look for the filter they just cleared.
  if (moreEl.value) moreEl.value.open = false

  emitFilter()
}

/** An empty or unparseable box means "no bound", not zero. */
function toNumber(value: string): number | null {
  if (value.trim() === '') return null

  const parsed = Number(value)

  return Number.isFinite(parsed) ? parsed : null
}
</script>

<style scoped>
.chips {
  display: flex;
  gap: 0.4rem;
  overflow-x: auto;
  padding-bottom: 0.55rem;
  margin-bottom: 0.6rem;
  /* Room for the focus ring, which a scroll container would otherwise clip. */
  padding-top: 3px;
  scrollbar-width: thin;
}

.chip {
  flex: 0 0 auto;
  font-size: 0.86rem;
  line-height: 1;
  padding: 0.5rem 0.85rem;
  border-radius: 50rem;
  border: 1px solid var(--bs-border-color, #ced4da);
  background: var(--bs-body-bg, #fff);
  color: inherit;
  white-space: nowrap;
  cursor: pointer;
}

.chip:hover {
  border-color: var(--brand-primary, #0d6efd);
}

.chip--on {
  background: var(--brand-primary, #0d6efd);
  border-color: var(--brand-primary, #0d6efd);
  color: #fff;
  font-weight: 600;
}

.filter-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.filter-row .form-control[type='search'] {
  flex: 1 1 12rem;
  min-width: 0;
}

.more {
  flex: 0 0 auto;
}

.more-summary {
  cursor: pointer;
  font-size: 0.88rem;
  padding: 0.5rem 0.2rem;
  white-space: nowrap;
  user-select: none;
}

/*
 * The panel is full width and starts a new row, so opening it pushes the grid
 * down instead of squeezing the search box.
 */
.more-panel {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
  gap: 0.6rem;
  width: 100%;
  padding-top: 0.7rem;
}

.more[open] {
  flex: 1 1 100%;
}
</style>
