import api from '@/services/api'
import type { StoreSettings, StoreSettingsUpdate } from '@/types/storeSettings'

export async function getStoreSettings(): Promise<StoreSettings> {
  const { data } = await api.get<StoreSettings>('/store-settings')
  return data
}

export async function updateStoreSettings(settings: StoreSettingsUpdate): Promise<StoreSettings> {
  const { data } = await api.put<StoreSettings>('/store-settings', settings)
  return data
}
