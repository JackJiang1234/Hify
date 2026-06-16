import { api } from './client'
import type { PageQuery } from './types'

// 对应后端 /api/v1/knowledge-bases —— 文档上传、分块、检索测试待补全
export const knowledgeApi = {
  list: (query: PageQuery) => api.getPage<unknown>('/knowledge-bases', query),
  get: (id: number) => api.get<unknown>(`/knowledge-bases/${id}`),
  create: (body: object) => api.post<unknown>('/knowledge-bases', body),
  remove: (id: number) => api.delete<void>(`/knowledge-bases/${id}`),
}
