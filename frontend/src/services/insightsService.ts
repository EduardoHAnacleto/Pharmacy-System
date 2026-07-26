import api from '@/services/api'
import type {
  Funnel,
  OperationalAlert,
  PromotionPerformance,
  SalesSummary,
} from '@/types/analytics'

export interface DateRange {
  from: string
  to: string
}

/** Defaults to the last 30 days, matching the API's own default. */
export function defaultRange(): DateRange {
  const to = new Date()
  const from = new Date(to)
  from.setDate(from.getDate() - 30)

  return { from: iso(from), to: iso(to) }
}

function iso(date: Date): string {
  return date.toISOString().slice(0, 10)
}

export async function getFunnel(range: DateRange): Promise<Funnel> {
  const { data } = await api.get<Funnel>('/analytics/funnel', { params: range })
  return data
}

export async function getPromotionPerformance(range: DateRange): Promise<PromotionPerformance[]> {
  const { data } = await api.get<PromotionPerformance[]>('/analytics/promotions', {
    params: { ...range, limit: 50 },
  })
  return data
}

export async function getSales(range: DateRange): Promise<SalesSummary> {
  const { data } = await api.get<SalesSummary>('/analytics/sales', { params: range })
  return data
}

export async function getAlerts(): Promise<OperationalAlert[]> {
  const { data } = await api.get<OperationalAlert[]>('/analytics/alerts')
  return data
}

export async function listExportDatasets(): Promise<string[]> {
  const { data } = await api.get<string[]>('/exports')
  return data
}

/**
 * Downloads an export.
 *
 * Fetched through the authenticated axios client rather than a plain link: the
 * endpoint requires a bearer token, which a browser navigation would not send.
 */
export async function downloadExport(dataset: string, range: DateRange): Promise<void> {
  const response = await api.get(`/exports/${dataset}`, {
    params: range,
    responseType: 'blob',
  })

  const url = URL.createObjectURL(response.data as Blob)

  try {
    const link = document.createElement('a')
    link.href = url
    link.download = `${dataset}_${range.from}_${range.to}.csv`
    link.click()
  } finally {
    URL.revokeObjectURL(url)
  }
}
