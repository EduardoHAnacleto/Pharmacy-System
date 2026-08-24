/** Mirrors PromotionStatus on the backend. */
export type PromotionStatus = 'Draft' | 'Scheduled' | 'Active' | 'Expired' | 'Archived'

export interface ItemPromotion {
  id: number
  name: string

  price: number

  /** Null when the item has no original price to strike through. */
  priceBefore: number | null

  imageUrl: string

  dateStart: string // ISO string
  dateEnd: string // ISO string

  /**
   * True when the item may only be dispensed against a prescription.
   *
   * Nothing on the storefront enforces it — the order still completes over
   * WhatsApp, where a pharmacist handles it. What it buys is telling the
   * customer before they build a basket they cannot collect without a doctor.
   */
  requiresPrescription: boolean

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
