import { api } from './client'
import type { PageQuery } from './types'

// 对应后端 /api/v1/mcp-servers —— MCP 工具接入配置
export const mcpApi = {
  list: (query: PageQuery) => api.getPage<unknown>('/mcp-servers', query),
  get: (id: number) => api.get<unknown>(`/mcp-servers/${id}`),
  create: (body: object) => api.post<unknown>('/mcp-servers', body),
  remove: (id: number) => api.delete<void>(`/mcp-servers/${id}`),
}
