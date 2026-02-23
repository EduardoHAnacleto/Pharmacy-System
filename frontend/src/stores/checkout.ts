import { defineStore } from 'pinia'

export type DeliveryType = 'pickup' | 'delivery'
export type PaymentMethod = 'pix' | 'onDelivery'
export type DeliveryPaymentType = 'credit' | 'debit' | 'cash'

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
    allowedCity: 'Santa Terezinha de Itaipu',
    city: 'Santa Terezinha de Itaipu',
  }),

  getters: {
    isCpfValid(): boolean {
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

    isCityAllowed(): boolean {
      return this.city === this.allowedCity
    },

    isPickupValid(): boolean {
      return this.buyerName.trim().length > 0
    },

    isDeliveryValid(): boolean {
      return (
        this.fullName.trim().length > 0 &&
        this.isCpfValid &&
        this.isCityAllowed &&
        this.cep.trim().length > 0 &&
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
    },
  },
})
