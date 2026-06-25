import type { KnowledgeBaseUpsert } from '@/api/knowledge'

/** 分块参数范围（与后端 KnowledgeBaseValidation 一致）。 */
export const CHUNK_SIZE_RANGE = { min: 100, max: 4000 } as const

/** 检索预览参数范围（与后端校验一致）。 */
export const SEARCH_RANGE = {
  topK: { min: 1, max: 20 },
  scoreThreshold: { min: 0, max: 1, step: 0.05 },
} as const

/** 单文件上传上限（字节），与后端 DocumentsController.MaxUploadBytes 一致。 */
export const MAX_UPLOAD_BYTES = 5 * 1024 * 1024

/** 建库表单默认值。 */
export function defaultKnowledgeBaseForm(): KnowledgeBaseUpsert {
  return { name: '', description: '', embeddingModelId: 0, chunkSize: 1000, chunkOverlap: 100 }
}
