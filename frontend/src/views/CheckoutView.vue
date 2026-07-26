<template>
  <section class="py-5">
    <div class="container px-4 px-lg-5">
      <h2 class="fw-bolder mb-4">{{ t('checkout.title') }}</h2>

      <div class="row">
        <!-- PAYMENT -->
        <div class="col-lg-7">
          <div class="card mb-4">
            <div class="card-body">
              <h5 class="mb-3">{{ t('checkout.paymentMethod') }}</h5>

              <label class="visually-hidden" for="payment-method">
                {{ t('checkout.paymentMethod') }}
              </label>
              <select id="payment-method" v-model="checkout.paymentMethod" class="form-select mb-3">
                <option disabled value="">{{ t('common.select') }}</option>
                <option value="onDelivery">{{ t('checkout.onDelivery') }}</option>
                <option value="pix">{{ t('checkout.pix') }}</option>
              </select>

              <!-- PAYMENT BY HAND -->
              <div v-if="checkout.paymentMethod === 'onDelivery'">
                <p class="text-muted small">{{ t('checkout.afterHoursNotice') }}</p>

                <label class="visually-hidden" for="delivery-payment">
                  {{ t('checkout.paymentMethod') }}
                </label>
                <select
                  id="delivery-payment"
                  v-model="checkout.deliveryPaymentType"
                  class="form-select mb-3"
                >
                  <option disabled value="">{{ t('common.select') }}</option>
                  <option value="credit">{{ t('checkout.creditCard') }}</option>
                  <option value="debit">{{ t('checkout.debitCard') }}</option>
                  <option value="cash">{{ t('checkout.cash') }}</option>
                </select>

                <div v-if="checkout.deliveryPaymentType === 'cash'" class="mb-3">
                  <label class="form-label" for="change-for">{{ t('checkout.changeFor') }}</label>
                  <input
                    id="change-for"
                    v-model.number="checkout.cashChangeFor"
                    type="number"
                    min="0"
                    class="form-control"
                  />
                </div>
              </div>

              <!-- CUSTOMER DATA -->
              <div class="card mb-4">
                <div class="card-body">
                  <h5 class="mb-3">{{ t('checkout.customerData') }}</h5>

                  <!-- NAME -->
                  <div class="mb-3">
                    <label class="form-label" for="full-name">{{ t('checkout.fullName') }}</label>
                    <input
                      id="full-name"
                      v-model="checkout.fullName"
                      type="text"
                      class="form-control"
                      :class="{ 'is-invalid': nameError }"
                      :placeholder="t('checkout.fullNamePlaceholder')"
                      autocomplete="name"
                    />
                    <div v-if="nameError" class="invalid-feedback">
                      {{ nameError }}
                    </div>
                  </div>

                  <!-- TAX ID -->
                  <div v-if="checkout.collectTaxId">
                    <label class="form-label" for="tax-id">{{ t('checkout.taxId') }}</label>
                    <input
                      id="tax-id"
                      v-model="checkout.cpf"
                      type="text"
                      class="form-control"
                      :class="{ 'is-invalid': cpfError }"
                      :placeholder="t('checkout.taxIdPlaceholder')"
                    />
                    <div v-if="cpfError" class="invalid-feedback">
                      {{ cpfError }}
                    </div>
                  </div>
                </div>
              </div>

              <!-- ADDRESS -->
              <div v-if="cart.deliveryType === 'delivery'" class="card mb-4">
                <div class="card-body">
                  <h5 class="mb-3">{{ t('checkout.deliveryAddress') }}</h5>

                  <label class="form-label" for="street">{{ t('checkout.street') }}</label>
                  <input
                    id="street"
                    v-model="checkout.street"
                    class="form-control mb-2"
                    autocomplete="address-line1"
                  />

                  <label class="form-label" for="number">{{ t('checkout.number') }}</label>
                  <input id="number" v-model="checkout.number" class="form-control mb-2" />

                  <label class="form-label" for="neighborhood">
                    {{ t('checkout.neighborhood') }}
                  </label>
                  <input
                    id="neighborhood"
                    v-model="checkout.neighborhood"
                    class="form-control mb-2"
                    autocomplete="address-level2"
                  />

                  <!-- CITY -->
                  <div v-if="cities.length > 1" class="mb-2">
                    <label class="form-label" for="city">{{ t('checkout.city') }}</label>
                    <select id="city" v-model="checkout.city" class="form-select">
                      <option disabled value="">{{ t('common.select') }}</option>
                      <option v-for="city in cities" :key="city" :value="city">{{ city }}</option>
                    </select>
                  </div>

                  <p v-else-if="checkout.city" class="small text-muted mb-2">
                    {{ t('checkout.city') }}: {{ checkout.city }}
                  </p>

                  <small
                    v-if="checkout.showErrors && !checkout.isCityAllowed"
                    class="text-danger d-block mb-2"
                  >
                    {{ t('checkout.cityNotServed') }}
                  </small>

                  <template v-if="checkout.collectPostalCode">
                    <label class="form-label" for="postal-code">
                      {{ t('checkout.postalCode') }}
                    </label>
                    <input
                      id="postal-code"
                      v-model="checkout.cep"
                      class="form-control mb-2"
                      autocomplete="postal-code"
                    />
                  </template>

                  <label class="form-label" for="complement">{{ t('checkout.complement') }}</label>
                  <input id="complement" v-model="checkout.complement" class="form-control" />
                </div>
              </div>

              <small v-if="checkout.showErrors && !isPaymentValid" class="text-danger d-block mt-2">
                {{ t('checkout.paymentInvalid') }}
              </small>
            </div>
          </div>
        </div>

        <!-- SUMMARY -->
        <div class="col-lg-5">
          <div class="card p-3 shadow-sm">
            <h5 class="mb-3">{{ t('checkout.orderSummary') }}</h5>

            <ul class="list-unstyled small mb-3">
              <li v-for="item in cart.items" :key="item.id">
                {{ item.name }} x{{ item.quantity }}
              </li>
            </ul>

            <hr />

            <div class="d-flex justify-content-between">
              <span>{{ t('common.total') }}</span>
              <strong>{{ formatMoney(cart.finalTotal) }}</strong>
            </div>

            <button class="btn btn-success w-100 mt-3" @click="openConfirmation">
              {{ t('checkout.confirmOrder') }}
            </button>

            <small v-if="!whatsAppNumber" class="text-danger d-block mt-2">
              {{ t('checkout.unavailable') }}
            </small>
          </div>
        </div>
      </div>
    </div>

    <!-- MODAL CONFIRM -->
    <div
      v-if="showModal"
      class="modal fade show"
      style="display: block"
      tabindex="-1"
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-title"
      @keydown.esc="showModal = false"
    >
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title" id="confirm-title">{{ t('checkout.confirmOrder') }}</h5>
            <button
              class="btn-close"
              :aria-label="t('common.cancel')"
              @click="showModal = false"
            ></button>
          </div>

          <div class="modal-body">
            <p>{{ t('checkout.confirmBody') }}</p>
            <p class="fw-bold">{{ t('common.total') }}: {{ formatMoney(cart.finalTotal) }}</p>
          </div>

          <div class="modal-footer">
            <button class="btn btn-secondary" @click="showModal = false">
              {{ t('common.cancel') }}
            </button>
            <button ref="confirmButton" class="btn btn-success" @click="confirmOrder">
              {{ t('common.confirm') }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="showModal" class="modal-backdrop fade show"></div>
  </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useCartStore } from '@/stores/cart'
import { useCheckoutStore } from '@/stores/checkout'
import { useSettingsStore } from '@/stores/settings'
import { track } from '@/services/analytics'
import { formatMoney } from '@/utils/format'
import api from '@/services/api'

const { t, locale } = useI18n()
const cart = useCartStore()
const checkout = useCheckoutStore()
const settings = useSettingsStore()
const router = useRouter()

const showModal = ref(false)
const confirmButton = ref<HTMLButtonElement | null>(null)
const cpfError = ref('')
const nameError = ref('')

const cities = computed(() => checkout.allowedCities)

/**
 * Destination for the order handoff, from the shop's own settings.
 *
 * Two different numbers used to be hardcoded in two components, and a
 * VITE_WHATSAPP_NUMBER build arg that nothing read. One row in the database now
 * decides it, and the button says so plainly when it is unset.
 */
const whatsAppNumber = computed(() => settings.whatsAppNumber)

onMounted(() => {
  checkout.applyDefaultCity()
})

// Settings may still be in flight on a direct navigation to /checkout.
watch(
  () => settings.loaded,
  () => checkout.applyDefaultCity(),
)

/* =========================
   VALIDATIONS
========================= */
const isPaymentValid = computed(() => {
  if (checkout.paymentMethod === 'pix') return true

  if (checkout.paymentMethod === 'onDelivery') {
    if (!checkout.deliveryPaymentType) return false
    if (checkout.deliveryPaymentType === 'cash' && !checkout.cashChangeFor) {
      return false
    }
    return true
  }

  return false
})

function validateForm(): boolean {
  let valid = true

  nameError.value = ''
  cpfError.value = ''

  if (!checkout.fullName || checkout.fullName.length < 5) {
    nameError.value = t('checkout.nameInvalid')
    valid = false
  }

  // Single source of truth: the store getter. A second copy of the check-digit
  // algorithm here is how the two versions drifted apart in the first place.
  // The getter also knows when the shop does not collect a tax id at all.
  if (!checkout.isCpfValid) {
    cpfError.value = t('checkout.taxIdInvalid')
    valid = false
  }

  if (cart.deliveryType === 'delivery' && !checkout.isCityAllowed) {
    valid = false
  }

  return valid
}

async function openConfirmation() {
  checkout.showErrors = true

  if (!whatsAppNumber.value) return

  const formValid = validateForm()
  if (!formValid) return
  if (!isPaymentValid.value) return

  // Counted here rather than on page load: reaching a valid, confirmable order is
  // the step worth measuring, not merely opening the checkout screen.
  track('checkout_started')

  showModal.value = true

  // Focus moves into the dialog so it is operable from the keyboard.
  await nextTick()
  confirmButton.value?.focus()
}

function getFormattedDateTime() {
  const now = new Date()
  const tag = locale.value

  const date = new Intl.DateTimeFormat(tag, { dateStyle: 'short' }).format(now)
  const time = new Intl.DateTimeFormat(tag, { timeStyle: 'short' }).format(now)

  return `${date} ${time}`
}

async function confirmOrder() {
  const number = whatsAppNumber.value
  if (!number) return

  // Recorded before the handoff, and deliberately without the customer: name,
  // tax id, postcode and address stay in this browser and in the WhatsApp message
  // below. The API has no field for them.
  const orderNumber = await recordOrder()

  const items = cart.items
    .map((i) => `• ${i.name} x${i.quantity} — ${formatMoney(i.price * i.quantity)}`)
    .join('\n')

  const isDelivery = cart.deliveryType === 'delivery'

  const address = isDelivery
    ? [
        `📍 *${t('checkout.deliveryAddress')}*`,
        checkout.street && `${t('checkout.street')}: ${checkout.street}`,
        checkout.number && `${t('checkout.number')}: ${checkout.number}`,
        checkout.neighborhood && `${t('checkout.neighborhood')}: ${checkout.neighborhood}`,
        checkout.city && `${t('checkout.city')}: ${checkout.city}`,
        checkout.collectPostalCode &&
          checkout.cep &&
          `${t('checkout.postalCode')}: ${checkout.cep}`,
      ]
        .filter(Boolean)
        .join('\n')
    : `📦 *${t('cart.pickup')}*`

  const payment =
    checkout.paymentMethod === 'pix'
      ? t('checkout.pix')
      : `${t('checkout.onDelivery')} (${paymentTypeLabel()})${
          checkout.deliveryPaymentType === 'cash'
            ? ` - ${t('checkout.changeFor')} ${formatMoney(checkout.cashChangeFor ?? 0)}`
            : ''
        }`

  const taxIdLine = checkout.collectTaxId ? `\n🧾 ${t('checkout.taxId')}: ${checkout.cpf}` : ''

  const message = `
🛒 *${settings.settings.storeName}*${orderNumber ? ` #${orderNumber}` : ''}
🕒 ${getFormattedDateTime()}

👤 ${t('checkout.fullName')}: ${checkout.fullName}${taxIdLine}

${address}

📦 *${t('cart.products')}:*
${items}

💳 ${t('checkout.paymentMethod')}: ${payment}
💰 ${t('common.total')}: ${formatMoney(cart.finalTotal)}
  `.trim()

  track('whatsapp_click')

  window.open(`https://wa.me/${number}?text=${encodeURIComponent(message)}`, '_blank')

  cart.clearCart()
  checkout.reset()
  showModal.value = false
  router.push('/')
}

function paymentTypeLabel(): string {
  switch (checkout.deliveryPaymentType) {
    case 'credit':
      return t('checkout.creditCard')
    case 'debit':
      return t('checkout.debitCard')
    case 'cash':
      return t('checkout.cash')
    default:
      return ''
  }
}

/**
 * Records the order for reporting.
 *
 * Returns the short reference the operator can use to match this row to the
 * WhatsApp conversation, or null when recording failed — a reporting write must
 * never stop a customer from placing their order.
 */
async function recordOrder(): Promise<string | null> {
  try {
    const { data } = await api.post<{ orderNumber: string; total: number }>('/orders', {
      fulfillmentType: cart.deliveryType,
      paymentMethod:
        checkout.paymentMethod === 'pix' ? 'pix' : (checkout.deliveryPaymentType ?? null),
      // City only. Street, number and postcode are not sent.
      deliveryCity: cart.deliveryType === 'delivery' ? checkout.city : null,
      deliveryFee: cart.deliveryType === 'delivery' && cart.canDeliver ? cart.deliveryFee : 0,
      items: cart.items.map((i) => ({
        promotionId: i.id,
        name: i.name,
        unitPrice: i.price,
        quantity: i.quantity,
      })),
    })

    return data.orderNumber
  } catch {
    return null
  }
}
</script>
