<template>
  <section class="py-5">
    <div class="container px-4 px-lg-5">
      <h2 class="fw-bolder mb-4">Pagamento</h2>

      <div class="row">
        <!-- PAYMENT -->
        <div class="col-lg-7">
          <div class="card mb-4">
            <div class="card-body">
              <h5 class="mb-3">Forma de Pagamento</h5>

              <select v-model="checkout.paymentMethod" class="form-select mb-3">
                <option disabled value="">Selecione</option>
                <option value="onDelivery">Na entrega</option>
                <option value="pix">Pix</option>
              </select>

              <!-- PAYMENT BY HAND -->
              <div v-if="checkout.paymentMethod === 'onDelivery'">
                <h5 class="mb-3">
                  Pedidos para entrega após as 18 horas serão realizadas no próximo dia.
                </h5>
                <select v-model="checkout.deliveryPaymentType" class="form-select mb-3">
                  <option disabled value="">Selecione</option>
                  <option value="credit">Cartão de Crédito</option>
                  <option value="debit">Cartão de Débito</option>
                  <option value="cash">Dinheiro</option>
                </select>

                <div v-if="checkout.deliveryPaymentType === 'cash'" class="mb-3">
                  <label class="form-label">Troco para quanto?</label>
                  <input
                    v-model.number="checkout.cashChangeFor"
                    type="number"
                    class="form-control"
                  />
                </div>
              </div>

              <!-- CUSTOMER DATA -->
              <div class="card mb-4">
                <div class="card-body">
                  <h5 class="mb-3">Dados do Cliente</h5>

                  <!-- NAME -->
                  <div class="mb-3">
                    <label class="form-label">Nome completo</label>
                    <input
                      v-model="checkout.fullName"
                      type="text"
                      class="form-control"
                      :class="{ 'is-invalid': nameError }"
                      placeholder="Digite seu nome completo"
                    />
                    <div v-if="nameError" class="invalid-feedback">
                      {{ nameError }}
                    </div>
                  </div>

                  <!-- CPF -->
                  <div>
                    <label class="form-label">CPF</label>
                    <input
                      v-model="checkout.cpf"
                      type="text"
                      class="form-control"
                      :class="{ 'is-invalid': cpfError }"
                      placeholder="000.000.000-00"
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
                  <h5 class="mb-3">Endereço de Entrega</h5>

                  <input v-model="checkout.street" class="form-control mb-2" placeholder="Rua" />
                  <input v-model="checkout.number" class="form-control mb-2" placeholder="Número" />
                  <input
                    v-model="checkout.neighborhood"
                    class="form-control mb-2"
                    placeholder="Bairro"
                  />
                  <input v-model="checkout.cep" class="form-control mb-2" placeholder="CEP" />
                  <input
                    v-model="checkout.complement"
                    class="form-control"
                    placeholder="Complemento"
                  />
                </div>
              </div>
              <!-- PIX -->
              <!--<div v-if="checkout.paymentMethod === 'pix'" class="alert alert-info">
                Você será redirecionado para o pagamento via Pix após a confirmação.
              </div>-->

              <small v-if="checkout.showErrors && !isPaymentValid" class="text-danger d-block mt-2">
                Selecione corretamente a forma de pagamento.
              </small>
            </div>
          </div>
        </div>

        <!-- SUMMARY -->
        <div class="col-lg-5">
          <div class="card p-3 shadow-sm">
            <h5 class="mb-3">Resumo do Pedido</h5>

            <ul class="list-unstyled small mb-3">
              <li v-for="item in cart.items" :key="item.id">
                {{ item.name }} x{{ item.quantity }}
              </li>
            </ul>

            <hr />

            <div class="d-flex justify-content-between">
              <span>Total</span>
              <strong>R$ {{ cart.finalTotal.toFixed(2) }}</strong>
            </div>

            <button class="btn btn-success w-100 mt-3" @click="abrirConfirmacao">
              Confirmar Pedido
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- MODAL CONFIRM -->
    <div v-if="showModal" class="modal fade show" style="display: block" tabindex="-1">
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title">Confirmar Pedido</h5>
            <button class="btn-close" @click="showModal = false"></button>
          </div>

          <div class="modal-body">
            <p>
              Deseja confirmar o envio do pedido? (Confirme e envie a mensagem do WhatsApp que
              aparecerá)
            </p>
            <p class="fw-bold">Total: R$ {{ cart.finalTotal.toFixed(2) }}</p>
          </div>

          <div class="modal-footer">
            <button class="btn btn-secondary" @click="showModal = false">Cancelar</button>
            <button class="btn btn-success" @click="confirmarPedido">Confirmar</button>
          </div>
        </div>
      </div>
    </div>

    <div v-if="showModal" class="modal-backdrop fade show"></div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useCartStore } from '@/stores/cart'
import { useCheckoutStore } from '@/stores/checkout'

const cart = useCartStore()
const checkout = useCheckoutStore()
const router = useRouter()

const showModal = ref(false)
const cpfError = ref('')
const nameError = ref('')

/* =========================
   VALIDATION CPF
========================= */
function validateCPF(cpf: string): boolean {
  cpf = cpf.replace(/\D/g, '')

  if (cpf.length !== 11) return false
  if (/^(\d)\1+$/.test(cpf)) return false

  let sum = 0
  for (let i = 0; i < 9; i++) {
    sum += parseInt(cpf.charAt(i)) * (10 - i)
  }

  let firstDigit = 11 - (sum % 11)
  if (firstDigit >= 10) firstDigit = 0
  if (firstDigit !== parseInt(cpf.charAt(9))) return false

  sum = 0
  for (let i = 0; i < 10; i++) {
    sum += parseInt(cpf.charAt(i)) * (11 - i)
  }

  let secondDigit = 11 - (sum % 11)
  if (secondDigit >= 10) secondDigit = 0

  return secondDigit === parseInt(cpf.charAt(10))
}

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
    nameError.value = 'Informe um nome válido'
    valid = false
  }

  if (!checkout.cpf || !validateCPF(checkout.cpf)) {
    cpfError.value = 'CPF inválido'
    valid = false
  }

  return valid
}

function abrirConfirmacao() {
  checkout.showErrors = true

  const formValid = validateForm()
  if (!formValid) return
  if (!isPaymentValid.value) return

  showModal.value = true
}

function getFormattedDateTime() {
  const now = new Date()

  const date = now.toLocaleDateString('pt-BR')
  const time = now.toLocaleTimeString('pt-BR', {
    hour: '2-digit',
    minute: '2-digit',
  })

  return `${date} às ${time}`
}

function confirmarPedido() {
  const itens = cart.items
    .map((i) => `• ${i.name} x${i.quantity} — R$ ${(i.price * i.quantity).toFixed(2)}`)
    .join('\n')

  const isDelivery = cart.deliveryType === 'delivery'

  const endereco = isDelivery
    ? [
        '📍 *Endereço de Entrega*',
        checkout.street && `Rua: ${checkout.street}`,
        checkout.number && `Número: ${checkout.number}`,
        checkout.neighborhood && `Bairro: ${checkout.neighborhood}`,
        checkout.cep && `CEP: ${checkout.cep}`,
      ]
        .filter(Boolean)
        .join('\n')
    : '📦 *Retirada no local*'

  const pagamento =
    checkout.paymentMethod === 'pix'
      ? 'Pix'
      : `Na entrega (${checkout.deliveryPaymentType})${
          checkout.deliveryPaymentType === 'cash'
            ? ` - Troco para R$ ${checkout.cashChangeFor}`
            : ''
        }`

  const orderDateTime = getFormattedDateTime()

  const message = `
🛒 *NOVO PEDIDO*
🕒 ${orderDateTime}

👤 Nome: ${checkout.fullName}
🧾 CPF: ${checkout.cpf}

${endereco}

📦 *Itens:*
${itens}

💳 Pagamento: ${pagamento}
💰 Total: R$ ${cart.finalTotal.toFixed(2)}
  `.trim()

  window.open(`https://wa.me/5545999975299?text=${encodeURIComponent(message)}`, '_blank')

  cart.clearCart()
  checkout.reset()
  showModal.value = false
  router.push('/')
}
</script>
