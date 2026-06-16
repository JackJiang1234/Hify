import { api } from './client'
import type { PageQuery } from './types'

// 对应后端 /api/v1/agents —— DTO 字段待后端契约确定后补全
export const agentApi = {
  list: (query: PageQuery) => api.getPage<unknown>('/agents', query),
  get: (id: number) => api.get<unknown>(`/agents/${id}`),
  create: (body: object) => api.post<unknown>('/agents', body),
  update: (id: number, body: object) => api.put<unknown>(`/agents/${id}`, body),
  remove: (id: number) => api.delete<void>(`/agents/${id}`),
}
