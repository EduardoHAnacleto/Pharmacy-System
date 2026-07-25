<template>
  <section class="py-4">
    <div class="container">
      <!-- HEADER -->
      <div class="d-flex justify-content-between align-items-center mb-4">
        <h3 class="mb-0">Insights</h3>
        <RouterLink class="btn btn-outline-secondary btn-sm" to="/admin">
          Voltar para promoções
        </RouterLink>
      </div>

      <!-- RANGE -->
      <div class="card mb-4">
        <div class="card-body row g-2 align-items-end">
          <div class="col-md-3">
            <label class="form-label small">De</label>
            <input type="date" class="form-control" v-model="range.from" />
          </div>
          <div class="col-md-3">
            <label class="form-label small">Até</label>
            <input type="date" class="form-control" v-model="range.to" />
          </div>
          <div class="col-md-3">
            <button class="btn btn-primary" :disabled="loading" @click="load">
              {{ loading ? 'Carregando...' : 'Aplicar' }}
            </button>
          </div>
        </div>
      </div>

      <div v-if="error" class="alert alert-danger">{{ error }}</div>

      <!-- ALERTS -->
      <div v-if="alerts.length > 0" class="card mb-4">
        <div class="card-body">
          <h5 class="mb-3">Alertas operacionais</h5>
          <ul class="mb-0">
            <li v-for="(alert, index) in alerts" :key="index">{{ alert.message }}</li>
          </ul>
        </div>
      </div>

      <!-- FUNNEL -->
      <div class="card mb-4">
        <div class="card-body">
          <h5 class="mb-1">Funil</h5>
          <p class="text-muted small">
            Visitas distintas em cada etapa. Antes disso não havia visibilidade alguma: o pedido
            existia só como mensagem no celular.
          </p>

          <div class="row text-center g-3">
            <div class="col-6 col-md" v-for="step in funnelSteps" :key="step.label">
              <div class="border rounded py-3 h-100">
                <div class="fs-4 fw-bold">{{ step.value }}</div>
                <div class="small text-muted">{{ step.label }}</div>
                <div v-if="step.rate !== null" class="small text-success">
                  {{ percent(step.rate) }}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- SALES -->
      <div class="card mb-4">
        <div class="card-body">
          <h5 class="mb-1">Vendas</h5>
          <p class="text-muted small">
            Pedidos registrados sem nenhum dado pessoal — só itens, valores e tipo de entrega.
          </p>

          <div class="row text-center g-3">
            <div class="col-6 col-md-3">
              <div class="border rounded py-3">
                <div class="fs-5 fw-bold">{{ sales.orders }}</div>
                <div class="small text-muted">Pedidos</div>
              </div>
            </div>
            <div class="col-6 col-md-3">
              <div class="border rounded py-3">
                <div class="fs-5 fw-bold">{{ money(sales.revenue) }}</div>
                <div class="small text-muted">Receita</div>
              </div>
            </div>
            <div class="col-6 col-md-3">
              <div class="border rounded py-3">
                <div class="fs-5 fw-bold">{{ money(sales.averageOrderValue) }}</div>
                <div class="small text-muted">Ticket médio</div>
              </div>
            </div>
            <div class="col-6 col-md-3">
              <div class="border rounded py-3">
                <div class="fs-5 fw-bold">
                  {{ sales.pickupOrders }} / {{ sales.deliveryOrders }}
                </div>
                <div class="small text-muted">Retirada / Entrega</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- PERFORMANCE -->
      <div class="card mb-4">
        <div class="card-body">
          <h5 class="mb-1">Desempenho por promoção</h5>
          <p class="text-muted small">
            Muita visualização com conversão baixa é o sinal que vale agir: o item é procurado, mas
            algo — preço ou foto — está impedindo.
          </p>

          <div class="table-responsive">
            <table class="table table-sm align-middle">
              <thead>
                <tr>
                  <th>Promoção</th>
                  <th class="text-end">Views</th>
                  <th class="text-end">No carrinho</th>
                  <th class="text-end">Conversão</th>
                  <th class="text-end">Qtd. vendida</th>
                  <th class="text-end">Receita</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in performance" :key="row.promotionId">
                  <td>
                    {{ row.name }}
                    <span v-if="row.sourcePromotionId" class="badge bg-light text-dark ms-1">
                      reativada
                    </span>
                  </td>
                  <td class="text-end">{{ row.views }}</td>
                  <td class="text-end">{{ row.addToCart }}</td>
                  <td class="text-end">{{ percent(row.conversionRate) }}</td>
                  <td class="text-end">{{ row.orderedQuantity }}</td>
                  <td class="text-end">{{ money(row.revenue) }}</td>
                </tr>
                <tr v-if="performance.length === 0">
                  <td colspan="6" class="text-center text-muted">Sem dados no período</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- EXPORT -->
      <div class="card">
        <div class="card-body">
          <h5 class="mb-1">Exportar</h5>
          <p class="text-muted small">CSV do período selecionado, para análise fora do sistema.</p>

          <div class="d-flex flex-wrap gap-2">
            <button
              v-for="dataset in datasets"
              :key="dataset"
              class="btn btn-sm btn-outline-primary"
              :disabled="downloading === dataset"
              @click="download(dataset)"
            >
              {{ downloading === dataset ? 'Baixando...' : dataset }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import {
  defaultRange,
  downloadExport,
  getAlerts,
  getFunnel,
  getPromotionPerformance,
  getSales,
  listExportDatasets,
} from '@/services/insightsService'
import { formatMoney, formatPercent } from '@/utils/format'
import type {
  Funnel,
  OperationalAlert,
  PromotionPerformance,
  SalesSummary,
} from '@/types/analytics'

const range = ref(defaultRange())

const funnel = ref<Funnel>({
  promotionViews: 0,
  addToCart: 0,
  checkoutStarted: 0,
  whatsAppClicks: 0,
  orders: 0,
  viewToCartRate: 0,
  cartToCheckoutRate: 0,
  checkoutToHandoffRate: 0,
})

const sales = ref<SalesSummary>({
  orders: 0,
  revenue: 0,
  averageOrderValue: 0,
  pickupOrders: 0,
  deliveryOrders: 0,
})

const performance = ref<PromotionPerformance[]>([])
const alerts = ref<OperationalAlert[]>([])
const datasets = ref<string[]>([])

const loading = ref(false)
const downloading = ref<string | null>(null)
const error = ref<string | null>(null)

const funnelSteps = computed(() => [
  { label: 'Viram promoção', value: funnel.value.promotionViews, rate: null },
  {
    label: 'Adicionaram ao carrinho',
    value: funnel.value.addToCart,
    rate: funnel.value.viewToCartRate,
  },
  {
    label: 'Iniciaram checkout',
    value: funnel.value.checkoutStarted,
    rate: funnel.value.cartToCheckoutRate,
  },
  {
    label: 'Foram ao WhatsApp',
    value: funnel.value.whatsAppClicks,
    rate: funnel.value.checkoutToHandoffRate,
  },
  { label: 'Pedidos registrados', value: funnel.value.orders, rate: null },
])

// Formatting follows the shop's own locale and currency; the dashboard used to
// hardcode pt-BR and BRL, which reported a New Zealand shop's revenue in reais.
const percent = formatPercent
const money = formatMoney

async function load() {
  loading.value = true
  error.value = null

  try {
    const [funnelData, performanceData, salesData, alertsData] = await Promise.all([
      getFunnel(range.value),
      getPromotionPerformance(range.value),
      getSales(range.value),
      getAlerts(),
    ])

    funnel.value = funnelData
    performance.value = performanceData
    sales.value = salesData
    alerts.value = alertsData
  } catch {
    error.value = 'Erro ao carregar os insights.'
  } finally {
    loading.value = false
  }
}

async function download(dataset: string) {
  downloading.value = dataset
  error.value = null

  try {
    await downloadExport(dataset, range.value)
  } catch {
    error.value = `Erro ao exportar ${dataset}.`
  } finally {
    downloading.value = null
  }
}

onMounted(async () => {
  await load()

  try {
    datasets.value = await listExportDatasets()
  } catch {
    datasets.value = []
  }
})
</script>
