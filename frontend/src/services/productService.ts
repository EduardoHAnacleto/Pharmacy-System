import api from './api'

export interface Product {
  id: number
  name: string
  price: number
  priceBefore: number
  image: string
  dateStart: Date
  dateEnd: Date
  isActive: boolean
}

export default {
  async getAll() {
    const { data } = await api.get<Product[]>('/products')
    return data
  },
}
