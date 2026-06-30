import { api } from './client'
import type { PageQuery } from './types'
import type { RunRequest, WorkflowDto, WorkflowRunDto, WorkflowUpsert } from '@/features/workflow/types'

// 对应后端 /api/v1/workflows —— 简单拖拽工作流（definition 为 {nodes,edges} JSON 文本）
export const workflowApi = {
  list: (query: PageQuery) => api.getPage<WorkflowDto>('/workflows', query),
  get: (id: number) => api.get<WorkflowDto>(`/workflows/${id}`),
  create: (body: WorkflowUpsert) => api.post<WorkflowDto>('/workflows', body),
  update: (id: number, body: WorkflowUpsert) => api.put<WorkflowDto>(`/workflows/${id}`, body),
  remove: (id: number) => api.delete<boolean>(`/workflows/${id}`),
  // 非 CRUD 动作：发布（发布前后端跑图校验）
  publish: (id: number) => api.post<WorkflowDto>(`/workflows/${id}/publish`),
  // 试运行（同步执行，跑完返回 run；执行失败也返回 run，status=failed）
  run: (id: number, body: RunRequest) => api.post<WorkflowRunDto>(`/workflows/${id}/runs`, body),
  listRuns: (id: number, query: PageQuery) => api.getPage<WorkflowRunDto>(`/workflows/${id}/runs`, query),
  getRun: (id: number, runId: number) => api.get<WorkflowRunDto>(`/workflows/${id}/runs/${runId}`),
}
