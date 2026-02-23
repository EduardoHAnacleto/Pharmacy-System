export interface ItemPromotion {
  id?: number
  nameProduct: string
  category: string
  type: string
  newPrice: number
  oldPrice?: number
  startDate?: string
  endDate?: string
  isActive: boolean
  user: string
  imageUrl?: string
}
