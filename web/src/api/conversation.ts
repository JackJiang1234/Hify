import { api } from './client'
import type { PageQuery } from './types'

/** 会话（对应后端 ConversationDto）。 */
export interface ConversationDto {
  id: number
  agentId: number
  title: string
  createdAt: number
  updatedAt: number
}

/** 历史消息（对应后端 MessageDto）。一期 role 仅 user/assistant。 */
export interface MessageDto {
  id: number
  conversationId: number
  role: string
  content: string
  finishReason: string
  status: string
  promptTokens: number
  completionTokens: number
  createdAt: number
}

/**
 * 对应后端 /api/v1/conversations —— 列表/历史/增删走普通 CRUD（Result/PageResult）。
 * 发消息为 SSE 流式，不走 axios，见 @/composables/useSse 的 streamChat。
 */
export const conversationApi = {
  /** 新建会话（绑定 Agent）。 */
  create: (agentId: number) => api.post<ConversationDto>('/conversations', { agentId }),
  /** 分页列出会话（按最近活跃倒序）。 */
  list: (query: PageQuery) => api.getPage<ConversationDto>('/conversations', query),
  /** 分页查询会话历史（后端按 id 倒序，最新在前）。 */
  history: (id: number, query: PageQuery) =>
    api.getPage<MessageDto>(`/conversations/${id}/messages`, query),
  /** 删除会话（级联软删消息）。 */
  remove: (id: number) => api.delete<boolean>(`/conversations/${id}`),
}
