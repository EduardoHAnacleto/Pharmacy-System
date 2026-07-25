import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCheckoutStore } from '@/stores/checkout'
import { useSettingsStore } from '@/stores/settings'

/**
 * The checkout store now reads the shop's rules — service cities and which
 * checkout fields to collect — from the settings store, so these tests configure
 * a shop rather than relying on values that used to be hardcoded.
 */
function configureShop(overrides: Partial<{ cities: string[]; taxId: boolean; postal: boolean }>) {
  const settings = useSettingsStore()

  settings.settings.deliveryCities = overrides.cities ?? []
  settings.settings.collectTaxId = overrides.taxId ?? true
  settings.settings.collectPostalCode = overrides.postal ?? true
  settings.loaded = true

  return settings
}

describe('checkout store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    configureShop({ cities: ['Santa Terezinha de Itaipu'] })
  })

  describe('isCpfValid', () => {
    // The store getter is the single source of truth: the checkout view used to
    // carry a second implementation of the check-digit algorithm.
    it.each(['529.982.247-25', '52998224725', '111.444.777-35'])('accepts %s', (cpf) => {
      const checkout = useCheckoutStore()
      checkout.cpf = cpf

      expect(checkout.isCpfValid).toBe(true)
    })

    it.each([
      ['', 'empty'],
      ['123', 'too short'],
      ['529982247250', 'too long'],
      ['529.982.247-24', 'wrong check digit'],
      ['529.982.247-15', 'wrong first check digit'],
      ['abcdefghijk', 'not numeric'],
    ])('rejects %s (%s)', (cpf) => {
      const checkout = useCheckoutStore()
      checkout.cpf = cpf

      expect(checkout.isCpfValid).toBe(false)
    })

    it.each([
      '00000000000',
      '11111111111',
      '22222222222',
      '33333333333',
      '44444444444',
      '55555555555',
      '66666666666',
      '77777777777',
      '88888888888',
      '99999999999',
    ])('rejects the repeated-digit sequence %s', (cpf) => {
      // These satisfy the check-digit maths but are never issued.
      const checkout = useCheckoutStore()
      checkout.cpf = cpf

      expect(checkout.isCpfValid).toBe(false)
    })
  })

  describe('isCityAllowed', () => {
    it('accepts a configured service city', () => {
      const checkout = useCheckoutStore()
      checkout.applyDefaultCity()

      expect(checkout.city).toBe('Santa Terezinha de Itaipu')
      expect(checkout.isCityAllowed).toBe(true)
    })

    it('rejects anywhere else', () => {
      const checkout = useCheckoutStore()
      checkout.city = 'Outra Cidade'

      expect(checkout.isCityAllowed).toBe(false)
    })

    it('accepts any of several configured cities', () => {
      configureShop({ cities: ['Foz do Iguaçu', 'Santa Terezinha de Itaipu'] })

      const checkout = useCheckoutStore()
      checkout.city = 'Foz do Iguaçu'

      expect(checkout.isCityAllowed).toBe(true)
    })

    it('ignores case and accents when matching', () => {
      const checkout = useCheckoutStore()
      checkout.city = 'santa terezinha de itaipu'

      expect(checkout.isCityAllowed).toBe(true)
    })

    it('does not preselect a city when the shop serves several', () => {
      configureShop({ cities: ['Foz do Iguaçu', 'Santa Terezinha de Itaipu'] })

      const checkout = useCheckoutStore()
      checkout.applyDefaultCity()

      // With a choice to make, the checkout must ask rather than guess.
      expect(checkout.city).toBe('')
    })

    it('treats a shop with no configured cities as unrestricted', () => {
      // Refusing every order because the field was left blank is the worse
      // failure mode for a shop that simply has not filled it in.
      configureShop({ cities: [] })

      const checkout = useCheckoutStore()
      checkout.city = 'Anywhere'

      expect(checkout.isCityAllowed).toBe(true)
    })
  })

  describe('checkout fields controlled by settings', () => {
    it('skips tax id validation for a shop that does not collect one', () => {
      configureShop({ cities: ['Wellington'], taxId: false })

      const checkout = useCheckoutStore()

      // A shop outside Brazil must not be blocked by a CPF check digit.
      expect(checkout.cpf).toBe('')
      expect(checkout.isCpfValid).toBe(true)
    })

    it('still requires a valid tax id when the shop collects one', () => {
      const checkout = useCheckoutStore()
      checkout.cpf = '529.982.247-24'

      expect(checkout.isCpfValid).toBe(false)
    })

    it('does not require a postcode when the shop does not collect one', () => {
      configureShop({ cities: ['Wellington'], taxId: false, postal: false })

      const checkout = useCheckoutStore()
      checkout.fullName = 'Jane Doe'
      checkout.city = 'Wellington'
      checkout.neighborhood = 'Te Aro'
      checkout.street = 'Cuba Street'
      checkout.number = '12'

      expect(checkout.isDeliveryValid).toBe(true)
    })
  })

  describe('isPickupValid', () => {
    it('requires a buyer name', () => {
      const checkout = useCheckoutStore()

      expect(checkout.isPickupValid).toBe(false)

      checkout.buyerName = 'Maria'
      expect(checkout.isPickupValid).toBe(true)
    })

    it('does not accept whitespace as a name', () => {
      const checkout = useCheckoutStore()
      checkout.buyerName = '   '

      expect(checkout.isPickupValid).toBe(false)
    })
  })
})
