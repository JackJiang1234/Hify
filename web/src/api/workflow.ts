import { api } from './client'
import type { PageQuery } from './types'

// 对应后端 /api/v1/workflows —— JSON 配置执行（非可视化拖拽）
export const workflowApi = {
  list: (query: PageQuery) => api.getPage<unknown>('/workflows', query),
  get: (id: number) => api.get<unknown>(`/workflows/${id}`),
  create: (body: object) => api.post<unknown>('/workflows', body),
  update: (id: number, body: object) => api.put<unknown>(`/workflows/${id}`, body),
  remove: (id: number) => api.delete<void>(`/workflows/${id}`),
}
