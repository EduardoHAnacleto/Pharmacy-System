import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import i18n from '@/i18n'
import ProductGrid from '@/components/ProductGrid.vue'
import { formatMoney } from '@/utils/format'
import type { ItemPromotion } from '@/types/itemPromotion'

// Impressions and click tracking are not what these assertions are about, and
// the real module talks to the API.
vi.mock('@/services/analytics', () => ({
  track: vi.fn(),
  trackPromotionView: vi.fn(),
}))

function promotion(overrides: Partial<ItemPromotion> = {}): ItemPromotion {
  return {
    id: 1,
    name: 'Dipirona',
    price: 9.9,
    priceBefore: 19.9,
    imageUrl: '/images/promotions/1.png',
    dateStart: '2026-01-01T00:00:00Z',
    dateEnd: '2099-12-31T00:00:00Z',
    status: 'Active',
    archivedAt: null,
    imageMissing: false,
    sourcePromotionId: null,
    categoryId: 1,
    productType: 'default',
    createdByUserId: 1,
    createdByUserName: 'admin',
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    ...overrides,
  }
}

function mountGrid(products: ItemPromotion[]) {
  return mount(ProductGrid, {
    props: { products },
    global: { plugins: [i18n] },
  })
}

describe('ProductGrid pricing', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('strikes through the original price when the item is discounted', () => {
    const wrapper = mountGrid([promotion()])

    const struck = wrapper.find('.text-decoration-line-through')

    expect(struck.exists()).toBe(true)
    expect(struck.text()).toBe(formatMoney(19.9))
    expect(wrapper.text()).toContain(formatMoney(9.9))
  })

  it('shows a single price when the item has no original price', () => {
    // This branch of the card was unreachable until the API accepted a
    // promotion without a PriceBefore: as a non-nullable decimal an omitted
    // field bound to 0, failed [Range(0.01, ...)], and every item that existed
    // was therefore discounted.
    const wrapper = mountGrid([promotion({ priceBefore: null })])

    expect(wrapper.find('.text-decoration-line-through').exists()).toBe(false)
    expect(wrapper.text()).toContain(formatMoney(9.9))
  })
})
