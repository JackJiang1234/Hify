import { api } from './client'
import type { PageQuery } from './types'

/** 供应商健康（对应后端 ProviderHealthDto） */
export interface ProviderHealthDto {
  status: string
  latencyMs: number
  consecutiveFailures: number
  lastError: string
  checkedAt: number
}

/** 供应商（脱敏；对应后端 ProviderDto，密钥仅出 apiKeyHint） */
export interface ProviderDto {
  id: number
  name: string
  providerType: string
  baseUrl: string
  authType: string
  authHeaderName: string
  apiKeyHint: string
  settings: string
  enabled: boolean
  health: ProviderHealthDto
  createdAt: number
  updatedAt: number
}

/** 创建/更新供应商请求体（apiKey 为明文；更新时留空表示保留原密钥） */
export interface ProviderUpsert {
  name: string
  providerType: string
  baseUrl: string
  authType: string
  authHeaderName: string
  apiKey: string
  settings: string
  enabled: boolean
}

/** 模型（对应后端 ModelDto） */
export interface ModelDto {
  id: number
  providerId: number
  name: string
  displayName: string
  modelType: string
  contextWindow: number
  maxOutputTokens: number
  embeddingDimensions: number
  supportsStreaming: boolean
  supportsTools: boolean
  supportsVision: boolean
  source: string
  enabled: boolean
  isDefault: boolean
  sortOrder: number
  createdAt: number
  updatedAt: number
}

/** 创建/更新模型请求体 */
export interface ModelUpsert {
  name: string
  displayName: string
  modelType: string
  contextWindow: number
  maxOutputTokens: number
  embeddingDimensions: number
  supportsStreaming: boolean
  supportsTools: boolean
  supportsVision: boolean
  sortOrder: number
  enabled: boolean
}

export const providerApi = {
  list: (query: PageQuery) => api.getPage<ProviderDto>('/providers', query),
  get: (id: number) => api.get<ProviderDto>(`/providers/${id}`),
  create: (body: ProviderUpsert) => api.post<ProviderDto>('/providers', body),
  update: (id: number, body: ProviderUpsert) => api.put<ProviderDto>(`/providers/${id}`, body),
  remove: (id: number) => api.delete<boolean>(`/providers/${id}`),
  enable: (id: number) => api.post<boolean>(`/providers/${id}/enable`),
  disable: (id: number) => api.post<boolean>(`/providers/${id}/disable`),
  // 非 CRUD 操作用动词路径；返回刷新后的健康快照
  testConnection: (id: number) => api.post<ProviderHealthDto>(`/providers/${id}/test-connection`),
}

export const modelApi = {
  listByProvider: (providerId: number) => api.get<ModelDto[]>(`/providers/${providerId}/models`),
  create: (providerId: number, body: ModelUpsert) =>
    api.post<ModelDto>(`/providers/${providerId}/models`, body),
  update: (id: number, body: ModelUpsert) => api.put<ModelDto>(`/models/${id}`, body),
  remove: (id: number) => api.delete<boolean>(`/models/${id}`),
  setDefault: (id: number) => api.post<boolean>(`/models/${id}/set-default`),
  enable: (id: number) => api.post<boolean>(`/models/${id}/enable`),
  disable: (id: number) => api.post<boolean>(`/models/${id}/disable`),
}
