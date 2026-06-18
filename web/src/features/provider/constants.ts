/** 供应商/模型相关的下拉选项与展示元数据，取值与后端 Contracts 常量一一对齐。 */

export interface Option {
  label: string
  value: string
}

/** 供应商类型（决定适配器） */
export const PROVIDER_TYPE_OPTIONS: Option[] = [
  { label: 'OpenAI / 兼容', value: 'openai' },
  { label: 'Claude (Anthropic)', value: 'claude' },
  { label: 'Ollama (本地)', value: 'ollama' },
]

/** 鉴权方式 */
export const AUTH_TYPE_OPTIONS: Option[] = [
  { label: '无鉴权', value: 'none' },
  { label: 'Bearer', value: 'bearer' },
  { label: '自定义请求头', value: 'header' },
]

/** 模型类型 */
export const MODEL_TYPE_OPTIONS: Option[] = [
  { label: '对话 (chat)', value: 'chat' },
  { label: '嵌入 (embedding)', value: 'embedding' },
]

/** pgvector 固定向量维度，嵌入模型须匹配 */
export const REQUIRED_EMBEDDING_DIMENSIONS = 1536

type TagType = 'success' | 'warning' | 'danger' | 'info' | 'primary'

/** 健康状态 → 文案与标签色 */
export const HEALTH_STATUS_META: Record<string, { label: string; type: TagType }> = {
  healthy: { label: '健康', type: 'success' },
  unhealthy: { label: '异常', type: 'danger' },
  unknown: { label: '未探测', type: 'info' },
}

export function healthMeta(status: string): { label: string; type: TagType } {
  return HEALTH_STATUS_META[status] ?? { label: status || '未知', type: 'info' }
}

function labelOf(options: Option[], value: string): string {
  return options.find((option) => option.value === value)?.label ?? value
}

export const providerTypeLabel = (value: string) => labelOf(PROVIDER_TYPE_OPTIONS, value)
export const modelTypeLabel = (value: string) => labelOf(MODEL_TYPE_OPTIONS, value)

/** epoch ms → 本地时间字符串；0 视为「从未」 */
export function formatEpochMs(ms: number): string {
  return ms > 0 ? new Date(ms).toLocaleString() : '—'
}
