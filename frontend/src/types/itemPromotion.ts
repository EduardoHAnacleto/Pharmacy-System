export interface ItemPromotion {
  id: number
  name: string

  price: number
  priceBefore: number

  imageUrl: string

  dateStart: string // ISO string
  dateEnd: string // ISO string

  isActive: boolean

  categoryId: number
  productType: string

  createdByUserId: number
  createdByUserName: string
}
