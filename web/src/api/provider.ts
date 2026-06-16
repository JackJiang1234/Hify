import { api } from './client'
import type { PageQuery } from './types'

/** 模型提供商（与后端 /api/v1/providers 对齐）—— 参考用，字段以实际契约为准 */
export interface ProviderDto {
  id: number
  name: string
  type: string
  baseUrl: string
  enabled: boolean
}

export interface ProviderUpsert {
  name: string
  type: string
  baseUrl: string
  apiKey: string
}

export const providerApi = {
  list: (query: PageQuery) => api.getPage<ProviderDto>('/providers', query),
  get: (id: number) => api.get<ProviderDto>(`/providers/${id}`),
  create: (body: ProviderUpsert) => api.post<ProviderDto>('/providers', body),
  update: (id: number, body: ProviderUpsert) => api.put<ProviderDto>(`/providers/${id}`, body),
  remove: (id: number) => api.delete<void>(`/providers/${id}`),
  // 非 CRUD 操作用动词路径
  testConnection: (id: number) => api.post<boolean>(`/providers/${id}/test-connection`),
}
