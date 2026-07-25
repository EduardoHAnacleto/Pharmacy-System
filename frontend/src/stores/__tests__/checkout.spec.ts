import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCheckoutStore } from '@/stores/checkout'

describe('checkout store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
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
    it('accepts the configured service city', () => {
      const checkout = useCheckoutStore()

      expect(checkout.city).toBe(checkout.allowedCity)
      expect(checkout.isCityAllowed).toBe(true)
    })

    it('rejects anywhere else', () => {
      const checkout = useCheckoutStore()
      checkout.city = 'Outra Cidade'

      expect(checkout.isCityAllowed).toBe(false)
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
