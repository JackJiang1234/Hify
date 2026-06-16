import { api } from './client'
import type { PageQuery } from './types'

// 对应后端 /api/v1/conversations —— 列表/历史走普通 CRUD
export const conversationApi = {
  list: (query: PageQuery) => api.getPage<unknown>('/conversations', query),
  get: (id: number) => api.get<unknown>(`/conversations/${id}`),
  remove: (id: number) => api.delete<void>(`/conversations/${id}`),
}

// 流式发送消息见 @/composables/useSse —— SSE 不走 axios 实例
