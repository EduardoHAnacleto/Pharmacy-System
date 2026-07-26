export interface Funnel {
  promotionViews: number
  addToCart: number
  checkoutStarted: number
  whatsAppClicks: number
  orders: number
  viewToCartRate: number
  cartToCheckoutRate: number
  checkoutToHandoffRate: number
}

export interface PromotionPerformance {
  promotionId: number
  name: string
  status: string
  sourcePromotionId: number | null
  views: number
  addToCart: number
  orderedQuantity: number
  revenue: number
  conversionRate: number
}

export interface SalesSummary {
  orders: number
  revenue: number
  averageOrderValue: number
  pickupOrders: number
  deliveryOrders: number
}

export interface OperationalAlert {
  kind: string
  message: string
  promotionId: number | null
}
