/** 工作流节点/状态/条件运算符的展示元数据与图序列化助手，取值与后端对齐。 */
import type { ConditionOp, RunStatus, WfNodeType, WorkflowDefinition, WorkflowStatus } from './types'

export interface Option<T = string> {
  label: string
  value: T
}

type TagType = 'success' | 'warning' | 'danger' | 'info' | 'primary'

/** 节点类型 → 文案 + 标签色（对齐后端 WorkflowNodeType） */
export const NODE_TYPE_META: Record<WfNodeType, { label: string; type: TagType }> = {
  start: { label: '开始', type: 'primary' },
  llm: { label: '大模型', type: 'success' },
  tool: { label: '工具', type: 'warning' },
  condition: { label: '条件分支', type: 'info' },
  end: { label: '结束', type: 'primary' },
}

/** 工作流状态 → 文案 + 标签色 */
export const WORKFLOW_STATUS_META: Record<WorkflowStatus, { label: string; type: TagType }> = {
  draft: { label: '草稿', type: 'info' },
  published: { label: '已发布', type: 'success' },
}

/** 运行状态 → 文案 + 标签色 */
export const RUN_STATUS_META: Record<RunStatus, { label: string; type: TagType }> = {
  running: { label: '执行中', type: 'warning' },
  succeeded: { label: '成功', type: 'success' },
  failed: { label: '失败', type: 'danger' },
}

/** 条件运算符选项（对齐后端 ConditionOp） */
export const CONDITION_OP_OPTIONS: Option<ConditionOp>[] = [
  { label: '等于', value: 'eq' },
  { label: '不等于', value: 'ne' },
  { label: '包含', value: 'contains' },
  { label: '大于', value: 'gt' },
  { label: '小于', value: 'lt' },
]

/** condition 默认兜底出边句柄（对齐后端 ConditionNodeHandler.ElseHandle） */
export const ELSE_HANDLE = 'else'

/** 安全取节点类型元数据（未知类型回退）。 */
export function nodeTypeMeta(type: string): { label: string; type: TagType } {
  return NODE_TYPE_META[type as WfNodeType] ?? { label: type || '未知', type: 'info' }
}

/** 安全取工作流状态元数据（未知状态回退）。 */
export function workflowStatusMeta(status: string): { label: string; type: TagType } {
  return WORKFLOW_STATUS_META[status as WorkflowStatus] ?? { label: status || '未知', type: 'info' }
}

/** 安全取运行状态元数据（未知状态回退）。 */
export function runStatusMeta(status: string): { label: string; type: TagType } {
  return RUN_STATUS_META[status as RunStatus] ?? { label: status || '未知', type: 'info' }
}

/** 空画布定义 */
export function emptyDefinition(): WorkflowDefinition {
  return { version: '1', nodes: [], edges: [] }
}

/** 解析 definition JSON 文本为画布图；非法/空则回退空图（容错，不抛） */
export function parseDefinition(json: string): WorkflowDefinition {
  if (!json || !json.trim()) {
    return emptyDefinition()
  }
  try {
    const parsed = JSON.parse(json) as Partial<WorkflowDefinition>
    return {
      version: parsed.version ?? '1',
      nodes: parsed.nodes ?? [],
      edges: parsed.edges ?? [],
    }
  } catch {
    return emptyDefinition()
  }
}

/** 画布图序列化为 definition JSON 文本 */
export function stringifyDefinition(definition: WorkflowDefinition): string {
  return JSON.stringify(definition)
}

/** epoch ms → 本地时间字符串；0 视为「—」 */
export function formatEpochMs(ms: number): string {
  return ms > 0 ? new Date(ms).toLocaleString() : '—'
}
