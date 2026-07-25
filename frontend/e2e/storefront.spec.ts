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

async function stubPromotions(page: Page) {
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

test.describe('storefront', () => {
  test('lists the active promotions with both prices', async ({ page }) => {
    await stubPromotions(page)
    await page.goto('/')

    await expect(page.getByText('Dipirona 500mg')).toBeVisible()
    await expect(page.getByText('R$ 9.90')).toBeVisible()
    await expect(page.getByText('R$ 14.50')).toBeVisible()
  })

  test('adds an item to the cart and reflects it in the total', async ({ page }) => {
    await stubPromotions(page)
    await page.goto('/')

    await page.getByText('Dipirona 500mg').waitFor()
    await page
      .getByRole('button', { name: /adicionar/i })
      .first()
      .click()

    await page.goto('/cart')

    await expect(page.getByText('Dipirona 500mg')).toBeVisible()
    await expect(page.getByText(/9[.,]90/).first()).toBeVisible()
  })

  test('keeps the cart across a reload', async ({ page }) => {
    await stubPromotions(page)
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
})

test.describe('admin area', () => {
  test('redirects an anonymous visitor to the login page', async ({ page }) => {
    // The refresh call fails for an anonymous visitor, so the guard bounces them.
    await page.route('**/api/v1/auth/refresh', (route) =>
      route.fulfill({ status: 401, json: { title: 'Sessão expirada.' } }),
    )

    await page.goto('/admin')

    await expect(page).toHaveURL(/\/login/)
    await expect(page.getByPlaceholder('Usuário')).toBeVisible()
  })

  test('shows a single message for wrong credentials', async ({ page }) => {
    await page.route('**/api/v1/auth/refresh', (route) => route.fulfill({ status: 401 }))
    await page.route('**/api/v1/auth/login', (route) =>
      route.fulfill({ status: 401, body: 'Usuário ou senha incorretos.' }),
    )

    await page.goto('/login')

    await page.getByPlaceholder('Usuário').fill('admin')
    await page.getByPlaceholder('Senha').fill('wrong')
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

    await page.route('**/api/v1/auth/refresh', (route) => route.fulfill({ status: 401 }))
    await page.goto('/login')
    await page.getByPlaceholder('Usuário').waitFor()

    const bundle = scripts.join('\n')

    expect(bundle).not.toContain('ADMIN_PASSWORD')
    expect(bundle).not.toContain('admin_authenticated')
  })
})
