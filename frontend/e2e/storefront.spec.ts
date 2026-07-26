import { test, expect, type Page } from '@playwright/test'

/**
 * Storefront flows.
 *
 * Replaces the Vite scaffold test, which asserted an `<h1>You did it!</h1>` that
 * no view has ever rendered — it would have failed the moment anyone ran it.
 *
 * The API is stubbed at the network layer so these tests exercise the SPA
 * without needing a backend, a database or seeded promotions. Server behaviour
 * is covered by the integration tests instead.
 */

const PROMOTIONS_ROUTE = '**/api/v1/item-promotions/active?*'
const SETTINGS_ROUTE = '**/api/v1/store-settings'
const CATEGORIES_ROUTE = '**/api/v1/item-promotions/categories/all'

function promotion(id: number, name: string, price: number, priceBefore: number) {
  return {
    id,
    name,
    price,
    priceBefore,
    // A 1x1 transparent GIF, so no real image files are needed.
    imageUrl: 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7',
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
  }
}

/**
 * The shop's configuration, which every page now requests before its first paint.
 *
 * Stubbed rather than left to fail so these tests describe a specific shop: the
 * currency and locale asserted below are this object's, not a default.
 */
function settings(overrides: Record<string, unknown> = {}) {
  return {
    storeName: 'Farmácia Teste',
    tagline: null,
    logoUrl: null,
    primaryColor: '#0d6efd',
    secondaryColor: '#198754',
    address: 'Rua Exemplo, 100',
    city: 'Santa Terezinha de Itaipu',
    countryCode: 'BR',
    timeZone: 'America/Sao_Paulo',
    mapQuery: null,
    phone: '(45) 3541-0000',
    whatsAppNumber: '5545999975299',
    whatsAppNumberIsManagedByEnvironment: false,
    email: 'contato@exemplo.com',
    instagramUrl: null,
    facebookUrl: null,
    currency: 'BRL',
    locale: 'pt-BR',
    deliveryEnabled: true,
    pickupEnabled: true,
    deliveryFee: 8,
    minDeliveryTotal: 30,
    deliveryCities: ['Santa Terezinha de Itaipu'],
    collectTaxId: true,
    collectPostalCode: true,
    openingHours: { '1': [{ open: '08:00', close: '18:00' }] },
    footerText: null,
    ...overrides,
  }
}

async function stubApi(page: Page, settingsOverrides: Record<string, unknown> = {}) {
  await page.route(SETTINGS_ROUTE, (route) => route.fulfill({ json: settings(settingsOverrides) }))

  await page.route(CATEGORIES_ROUTE, (route) =>
    route.fulfill({ json: [{ id: 1, name: 'Genérico' }] }),
  )

  await page.route(PROMOTIONS_ROUTE, async (route) => {
    await route.fulfill({
      json: {
        items: [
          promotion(1, 'Dipirona 500mg', 9.9, 14.5),
          promotion(2, 'Protetor Solar FPS 50', 39.9, 59.9),
        ],
        page: 1,
        pageSize: 12,
        totalItems: 2,
        hasMore: false,
      },
    })
  })
}

/** Matches a formatted amount regardless of the space Intl puts after the symbol. */
function money(amount: string) {
  return new RegExp(`R\\$\\s?${amount.replace('.', '\\.')}`)
}

test.describe('storefront', () => {
  test('lists the active promotions with both prices', async ({ page }) => {
    await stubApi(page)
    await page.goto('/')

    await expect(page.getByText('Dipirona 500mg')).toBeVisible()

    // Formatted by Intl from the shop's locale and currency, so a comma decimal
    // separator here is the assertion that pt-BR/BRL reached the formatter.
    await expect(page.getByText(money('9,90'))).toBeVisible()
    await expect(page.getByText(money('14,50'))).toBeVisible()
  })

  test('shows the shop name from its settings', async ({ page }) => {
    await stubApi(page, { storeName: 'Padaria da Esquina' })
    await page.goto('/')

    // Nothing about the shop is in the bundle: the navbar renders what the API said.
    await expect(page.getByRole('navigation').getByText('Padaria da Esquina')).toBeVisible()
  })

  test('filters the grid by search term', async ({ page }) => {
    await stubApi(page)
    await page.goto('/')

    await page.getByText('Dipirona 500mg').waitFor()

    let requestedSearch: string | null = null

    await page.route(PROMOTIONS_ROUTE, async (route) => {
      requestedSearch = new URL(route.request().url()).searchParams.get('filter.search')

      await route.fulfill({
        json: {
          items: [promotion(1, 'Dipirona 500mg', 9.9, 14.5)],
          page: 1,
          pageSize: 12,
          totalItems: 1,
          hasMore: false,
        },
      })
    })

    await page.getByLabel('Buscar').fill('dipirona')

    // Debounced, so the assertion waits for the request the typing triggers.
    await expect(async () => expect(requestedSearch).toBe('dipirona')).toPass()
    await expect(page.getByText('Protetor Solar FPS 50')).toBeHidden()
  })

  test('adds an item to the cart and reflects it in the total', async ({ page }) => {
    await stubApi(page)
    await page.goto('/')

    await page.getByText('Dipirona 500mg').waitFor()
    await page
      .getByRole('button', { name: /adicionar/i })
      .first()
      .click()

    await page.goto('/cart')

    await expect(page.getByText('Dipirona 500mg')).toBeVisible()
    await expect(page.getByText(money('9,90')).first()).toBeVisible()
  })

  test('applies the shop delivery fee and minimum, not a hardcoded one', async ({ page }) => {
    // A different shop's numbers: the cart used to carry 8 and 30 as literals.
    await stubApi(page, { deliveryFee: 12.5, minDeliveryTotal: 50 })
    await page.goto('/')

    await page.getByText('Protetor Solar FPS 50').waitFor()
    await page
      .getByRole('button', { name: /adicionar/i })
      .nth(1)
      .click()

    await page.goto('/cart')
    await page.getByLabel('Tipo de entrega').selectOption('delivery')

    // R$39.90 is below this shop's R$50 minimum, so no fee is charged.
    await expect(page.getByText(money('50,00'))).toBeVisible()
    await expect(page.getByText(money('12,50'))).toBeHidden()
  })

  test('keeps the cart across a reload', async ({ page }) => {
    await stubApi(page)
    await page.goto('/')

    await page.getByText('Dipirona 500mg').waitFor()
    await page
      .getByRole('button', { name: /adicionar/i })
      .first()
      .click()

    await page.reload()
    await page.goto('/cart')

    // Persisted in localStorage with a 24h expiry.
    await expect(page.getByText('Dipirona 500mg')).toBeVisible()
  })

  test('publishes structured data for the grid', async ({ page }) => {
    await stubApi(page)
    await page.goto('/')

    await page.getByText('Dipirona 500mg').waitFor()

    const jsonLd = await page.locator('script[type="application/ld+json"]').first().textContent()
    const parsed = JSON.parse(jsonLd ?? '{}')

    expect(parsed['@type']).toBe('ItemList')
    expect(parsed.itemListElement).toHaveLength(2)
    expect(parsed.itemListElement[0].item.offers.priceCurrency).toBe('BRL')
  })

  test('states the privacy position to the visitor', async ({ page }) => {
    await stubApi(page)
    await page.goto('/privacy')

    // The page exists because the promise it makes is the product's, and it is
    // enforced by a backend test against the mapped schema.
    await expect(page.getByRole('heading', { name: /privacidade/i })).toBeVisible()
    await expect(
      page.getByText(/nunca é enviado|não é enviado|somente neste dispositivo/i),
    ).toBeVisible()
  })
})

test.describe('admin area', () => {
  test('redirects an anonymous visitor to the login page', async ({ page }) => {
    await stubApi(page)

    // The refresh call fails for an anonymous visitor, so the guard bounces them.
    await page.route('**/api/v1/auth/refresh', (route) =>
      route.fulfill({ status: 401, json: { title: 'Sessão expirada.' } }),
    )

    await page.goto('/admin')

    await expect(page).toHaveURL(/\/login/)
    await expect(page.getByLabel('Usuário')).toBeVisible()
  })

  test('shows a single message for wrong credentials', async ({ page }) => {
    await stubApi(page)
    await page.route('**/api/v1/auth/refresh', (route) => route.fulfill({ status: 401 }))
    await page.route('**/api/v1/auth/login', (route) =>
      route.fulfill({ status: 401, body: 'Usuário ou senha incorretos.' }),
    )

    await page.goto('/login')

    await page.getByLabel('Usuário').fill('admin')
    await page.getByLabel('Senha').fill('wrong')
    await page.getByRole('button', { name: /entrar/i }).click()

    await expect(page.getByText('Usuário ou senha incorretos.')).toBeVisible()
    // Still on the login page: no client-side credential check can be bypassed.
    await expect(page).toHaveURL(/\/login/)
  })

  test('does not ship any credential in the bundle', async ({ page }) => {
    // The admin password used to be the literal string "1234" in the JS.
    const scripts: string[] = []

    page.on('response', async (response) => {
      if (response.url().endsWith('.js')) {
        scripts.push(await response.text().catch(() => ''))
      }
    })

    await stubApi(page)
    await page.route('**/api/v1/auth/refresh', (route) => route.fulfill({ status: 401 }))
    await page.goto('/login')
    await page.getByLabel('Usuário').waitFor()

    const bundle = scripts.join('\n')

    expect(bundle).not.toContain('ADMIN_PASSWORD')
    expect(bundle).not.toContain('admin_authenticated')
  })
})
