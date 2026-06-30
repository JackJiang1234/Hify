import { api } from './client'
import type { PageQuery } from './types'

/** MCP Server（脱敏；对应后端 McpServerDto，凭证仅出 apiKeyHint） */
export interface McpServerDto {
  id: number
  name: string
  transport: string
  endpoint: string
  authType: string
  authHeaderName: string
  apiKeyHint: string
  timeoutMs: number
  enabled: boolean
  status: string
  lastError: string
  lastSyncedAt: number
  toolCount: number
  createdAt: number
  updatedAt: number
}

/** 创建/更新 MCP Server 请求体（apiKey 为明文；更新时留空表示保留原凭证；传输固定 streamable_http，不在请求内） */
export interface McpServerUpsert {
  name: string
  endpoint: string
  authType: string
  authHeaderName: string
  apiKey: string
  timeoutMs: number
  enabled: boolean
}

/** MCP 工具（对应后端 McpToolDto） */
export interface McpToolDto {
  id: number
  serverId: number
  name: string
  description: string
  inputSchema: string
  available: boolean
  enabled: boolean
}

export const mcpApi = {
  list: (query: PageQuery) => api.getPage<McpServerDto>('/mcp-servers', query),
  get: (id: number) => api.get<McpServerDto>(`/mcp-servers/${id}`),
  create: (body: McpServerUpsert) => api.post<McpServerDto>('/mcp-servers', body),
  update: (id: number, body: McpServerUpsert) => api.put<McpServerDto>(`/mcp-servers/${id}`, body),
  remove: (id: number) => api.delete<boolean>(`/mcp-servers/${id}`),
  enable: (id: number) => api.post<boolean>(`/mcp-servers/${id}/enable`),
  disable: (id: number) => api.post<boolean>(`/mcp-servers/${id}/disable`),
  // 非 CRUD 动作：测试连接 / 同步工具均返回刷新后的 Server 快照
  testConnection: (id: number) => api.post<McpServerDto>(`/mcp-servers/${id}/test-connection`),
  syncTools: (id: number) => api.post<McpServerDto>(`/mcp-servers/${id}/sync-tools`),
  // 工具：列出 / 启停 / 清理已移除（返回清理数量）
  listTools: (id: number) => api.get<McpToolDto[]>(`/mcp-servers/${id}/tools`),
  pruneTools: (id: number) => api.post<number>(`/mcp-servers/${id}/tools/prune`),
  enableTool: (toolId: number) => api.post<boolean>(`/mcp-tools/${toolId}/enable`),
  disableTool: (toolId: number) => api.post<boolean>(`/mcp-tools/${toolId}/disable`),
}
