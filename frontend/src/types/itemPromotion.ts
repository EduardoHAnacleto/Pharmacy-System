/** Mirrors PromotionStatus on the backend. */
export type PromotionStatus = 'Draft' | 'Scheduled' | 'Active' | 'Expired' | 'Archived'

export interface ItemPromotion {
  id: number
  name: string

  price: number
  priceBefore: number

  imageUrl: string

  dateStart: string // ISO string
  dateEnd: string // ISO string

  /** Replaces the old isActive boolean, which collapsed four states into false. */
  status: PromotionStatus

  archivedAt: string | null

  /** True when the image file behind this promotion is known to be gone. */
  imageMissing: boolean

  /** Set when this promotion was reactivated from an archived one. */
  sourcePromotionId: number | null

  categoryId: number
  productType: string

  createdByUserId: number
  createdByUserName: string

  createdAt: string
  updatedAt: string | null
}

export const STATUS_LABELS: Record<PromotionStatus, string> = {
  Draft: 'Rascunho',
  Scheduled: 'Agendada',
  Active: 'Ativa',
  Expired: 'Expirada',
  Archived: 'Arquivada',
}

export const STATUS_BADGE_CLASS: Record<PromotionStatus, string> = {
  Draft: 'bg-secondary',
  Scheduled: 'bg-info',
  Active: 'bg-success',
  Expired: 'bg-warning text-dark',
  Archived: 'bg-dark',
}
