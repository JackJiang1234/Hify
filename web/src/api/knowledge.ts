import { api } from './client'
import type { PageQuery } from './types'

/** 知识库（对应后端 KnowledgeBaseDto）。 */
export interface KnowledgeBaseDto {
  id: number
  name: string
  description: string
  embeddingModelId: number
  chunkSize: number
  chunkOverlap: number
  /** 库内文档数；> 0 即已有分块，前端据此冻结嵌入模型/分块参数。 */
  documentCount: number
  createdAt: number
  updatedAt: number
}

/** 创建/更新知识库请求体（对应后端 Create/UpdateKnowledgeBaseRequest）。 */
export interface KnowledgeBaseUpsert {
  name: string
  description: string
  embeddingModelId: number
  chunkSize: number
  chunkOverlap: number
}

/** 文档（对应后端 DocumentDto）。一期仅 TXT，同步处理后 status 恒为 completed。 */
export interface DocumentDto {
  id: number
  knowledgeBaseId: number
  name: string
  fileType: string
  contentHash: string
  status: string
  charCount: number
  chunkCount: number
  errorMessage: string
  createdAt: number
  updatedAt: number
}

/** 检索命中分块（对应后端 KnowledgeChunkDto）。 */
export interface KnowledgeChunkDto {
  knowledgeBaseId: number
  documentId: number
  documentName: string
  chunkIndex: number
  content: string
  /** 相似度 [0,1]，已由余弦距离换算，越大越相关。 */
  score: number
}

/** 检索预览请求（对应后端 KnowledgeBaseSearchRequest）。 */
export interface KnowledgeSearchRequest {
  query: string
  topK: number
  scoreThreshold: number
}

// 对应后端 /api/v1/knowledge-bases
export const knowledgeApi = {
  list: (query: PageQuery) => api.getPage<KnowledgeBaseDto>('/knowledge-bases', query),
  get: (id: number) => api.get<KnowledgeBaseDto>(`/knowledge-bases/${id}`),
  create: (body: KnowledgeBaseUpsert) => api.post<KnowledgeBaseDto>('/knowledge-bases', body),
  update: (id: number, body: KnowledgeBaseUpsert) =>
    api.put<KnowledgeBaseDto>(`/knowledge-bases/${id}`, body),
  remove: (id: number) => api.delete<boolean>(`/knowledge-bases/${id}`),
  search: (id: number, body: KnowledgeSearchRequest) =>
    api.post<KnowledgeChunkDto[]>(`/knowledge-bases/${id}/search`, body),
}

// 对应后端 /api/v1/knowledge-bases/{kbId}/documents
export const documentApi = {
  list: (kbId: number, query: PageQuery) =>
    api.getPage<DocumentDto>(`/knowledge-bases/${kbId}/documents`, query),
  get: (kbId: number, docId: number) =>
    api.get<DocumentDto>(`/knowledge-bases/${kbId}/documents/${docId}`),
  /** 上传 TXT 文件（multipart，字段名须为 file，与后端 IFormFile 形参一致）。 */
  upload: (kbId: number, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return api.post<DocumentDto>(`/knowledge-bases/${kbId}/documents`, form)
  },
  remove: (kbId: number, docId: number) =>
    api.delete<boolean>(`/knowledge-bases/${kbId}/documents/${docId}`),
}
