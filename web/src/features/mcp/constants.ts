/** MCP Server / 工具相关的下拉选项与展示元数据，取值与后端 Contracts 常量一一对齐。 */

export interface Option {
  label: string
  value: string
}

/** 鉴权方式（与 provider 一致：none / bearer / header） */
export const AUTH_TYPE_OPTIONS: Option[] = [
  { label: '无鉴权', value: 'none' },
  { label: 'Bearer', value: 'bearer' },
  { label: '自定义请求头', value: 'header' },
]

/** 传输类型（一期固定 Streamable HTTP） */
export const TRANSPORT_STREAMABLE_HTTP = 'streamable_http'

type TagType = 'success' | 'warning' | 'danger' | 'info' | 'primary'

/** 连接状态 → 文案与标签色（对齐后端 McpServerStatuses） */
export const MCP_STATUS_META: Record<string, { label: string; type: TagType }> = {
  connected: { label: '已连接', type: 'success' },
  error: { label: '连接异常', type: 'danger' },
  unknown: { label: '未探测', type: 'info' },
}

export function statusMeta(status: string): { label: string; type: TagType } {
  return MCP_STATUS_META[status] ?? { label: status || '未知', type: 'info' }
}

export function authTypeLabel(value: string): string {
  return AUTH_TYPE_OPTIONS.find((option) => option.value === value)?.label ?? value
}

export function transportLabel(value: string): string {
  return value === TRANSPORT_STREAMABLE_HTTP ? 'Streamable HTTP' : value
}

/** epoch ms → 本地时间字符串；0 视为「从未」 */
export function formatEpochMs(ms: number): string {
  return ms > 0 ? new Date(ms).toLocaleString() : '—'
}
