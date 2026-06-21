import { api } from './client'
import type { PageQuery } from './types'

/** 模型生成参数（对应后端 ModelParams）。字段可空：留空沿用模型默认。 */
export interface ModelParams {
  temperature?: number | null
  topP?: number | null
  maxTokens?: number | null
}

/** RAG 检索参数（对应后端 RetrievalParams）。 */
export interface RetrievalParams {
  topK: number
  scoreThreshold: number
}

/** Agent 配置（对应后端 AgentDto）。 */
export interface AgentDto {
  id: number
  name: string
  description: string
  modelId: number
  systemPrompt: string
  modelParams: ModelParams
  retrievalParams: RetrievalParams
  maxIterations: number
  toolIds: number[]
  knowledgeBaseIds: number[]
  enabled: boolean
  createdAt: number
  updatedAt: number
}

/** 创建/更新 Agent 请求体（对应后端 Create/UpdateAgentRequest）。 */
export interface AgentUpsert {
  name: string
  description: string
  modelId: number
  systemPrompt: string
  modelParams: ModelParams
  retrievalParams: RetrievalParams
  maxIterations: number
  toolIds: number[]
  knowledgeBaseIds: number[]
  enabled: boolean
}

export const agentApi = {
  list: (query: PageQuery) => api.getPage<AgentDto>('/agents', query),
  get: (id: number) => api.get<AgentDto>(`/agents/${id}`),
  create: (body: AgentUpsert) => api.post<AgentDto>('/agents', body),
  update: (id: number, body: AgentUpsert) => api.put<AgentDto>(`/agents/${id}`, body),
  remove: (id: number) => api.delete<boolean>(`/agents/${id}`),
  enable: (id: number) => api.post<boolean>(`/agents/${id}/enable`),
  disable: (id: number) => api.post<boolean>(`/agents/${id}/disable`),
}
