import { defineStore } from 'pinia'
import { useSettingsStore } from '@/stores/settings'

export type DeliveryType = 'pickup' | 'delivery'
export type PaymentMethod = 'pix' | 'onDelivery'
export type DeliveryPaymentType = 'credit' | 'debit' | 'cash'

/**
 * The checkout form.
 *
 * Everything in this store stays in the browser. The customer's name, tax id,
 * postcode and address are placed in the WhatsApp message and never sent to the
 * API — there is no field for them in any request, and no column for them in the
 * database. Only the delivery city is reported, because a sales report split by
 * city is useful and a city is not a person.
 */
export const useCheckoutStore = defineStore('checkout', {
  state: () => ({
    // GERAL CONTROL
    deliveryType: 'pickup' as DeliveryType,
    showErrors: false,

    // PAYMENT
    paymentMethod: null as PaymentMethod | null,
    deliveryPaymentType: null as DeliveryPaymentType | null,
    cashChangeFor: null as number | null,

    // UPON PICKUP
    buyerName: '',

    // DELIVERY
    fullName: '',
    cpf: '',
    cep: '',
    neighborhood: '',
    street: '',
    number: '',
    complement: '',

    // CITY CONTROL
    city: '',
  }),

  getters: {
    /**
     * Cities the shop delivers to.
     *
     * Replaces the single `allowedCity: 'Santa Terezinha de Itaipu'` that used to
     * be written into this store — which no shop could change and which quietly
     * blocked delivery for every other one.
     */
    allowedCities(): string[] {
      return useSettingsStore().deliveryCities
    },

    /** Whether the tax id field is collected at all, per shop settings. */
    collectTaxId(): boolean {
      return useSettingsStore().settings.collectTaxId
    },

    collectPostalCode(): boolean {
      return useSettingsStore().settings.collectPostalCode
    },

    isCpfValid(): boolean {
      // Nothing to validate when the shop does not ask for it — a shop outside
      // Brazil must not be blocked by a check digit that has no meaning there.
      if (!this.collectTaxId) return true

      if (!this.cpf) return false

      const cpf = this.cpf.replace(/\D/g, '')

      if (cpf.length !== 11) return false

      if (/^(\d)\1{10}$/.test(cpf)) return false

      let sum = 0
      for (let i = 0; i < 9; i++) {
        sum += Number(cpf[i]) * (10 - i)
      }

      let firstDigit = (sum * 10) % 11
      if (firstDigit === 10) firstDigit = 0
      if (firstDigit !== Number(cpf[9])) return false

      sum = 0
      for (let i = 0; i < 10; i++) {
        sum += Number(cpf[i]) * (11 - i)
      }

      let secondDigit = (sum * 10) % 11
      if (secondDigit === 10) secondDigit = 0
      if (secondDigit !== Number(cpf[10])) return false

      return true
    },

    /**
     * Whether the chosen city is served.
     *
     * A shop that lists no cities is treated as unrestricted rather than as
     * serving nowhere: refusing every order is the worse failure mode for a shop
     * that simply has not filled the field in.
     */
    isCityAllowed(): boolean {
      const allowed = this.allowedCities

      if (allowed.length === 0) return true

      return allowed.some(
        (c) => c.localeCompare(this.city, undefined, { sensitivity: 'base' }) === 0,
      )
    },

    isPickupValid(): boolean {
      return this.buyerName.trim().length > 0
    },

    isDeliveryValid(): boolean {
      return (
        this.fullName.trim().length > 0 &&
        this.isCpfValid &&
        this.isCityAllowed &&
        (!this.collectPostalCode || this.cep.trim().length > 0) &&
        this.neighborhood.trim().length > 0 &&
        this.street.trim().length > 0 &&
        this.number.trim().length > 0
      )
    },

    isFormValid(): boolean {
      return this.deliveryType === 'pickup' ? this.isPickupValid : this.isDeliveryValid
    },
  },

  actions: {
    /**
     * Preselects the city when the shop serves exactly one.
     *
     * With a single service city there is nothing to choose, so asking is friction;
     * with several the checkout renders a picker instead.
     */
    applyDefaultCity() {
      const allowed = this.allowedCities

      if (!this.city && allowed.length === 1) {
        this.city = allowed[0]!
      }
    },

    reset() {
      this.deliveryType = 'pickup'
      this.showErrors = false

      this.paymentMethod = null
      this.deliveryPaymentType = null
      this.cashChangeFor = null

      this.buyerName = ''
      this.fullName = ''
      this.cpf = ''
      this.cep = ''
      this.neighborhood = ''
      this.street = ''
      this.number = ''
      this.complement = ''
      this.city = ''
    },
  },
})
