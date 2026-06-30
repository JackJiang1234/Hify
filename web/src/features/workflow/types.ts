/**
 * 工作流前端类型，与后端对齐：
 * - WorkflowDto / WorkflowRunDto 对应后端 DTO（definition/inputs/trace 为 JSON 字符串，api 层 parse）
 * - WorkflowDefinition / WfNode / WfEdge 是 definition 字符串解析后的画布图结构（{nodes,edges}）
 */

export type WfNodeType = 'start' | 'llm' | 'tool' | 'condition' | 'end'

export type WorkflowStatus = 'draft' | 'published'

export type RunStatus = 'running' | 'succeeded' | 'failed'

export type ConditionOp = 'eq' | 'ne' | 'contains' | 'gt' | 'lt'

/** 工作流视图（definition 为画布 JSON 文本） */
export interface WorkflowDto {
  id: number
  name: string
  description: string
  definition: string
  status: WorkflowStatus
  createdAt: number
  updatedAt: number
}

/** 创建/更新工作流请求体（definition 为画布 JSON 文本） */
export interface WorkflowUpsert {
  name: string
  description: string
  definition: string
}

/** 执行记录视图（inputs/trace 为 JSON 文本，output 为最终输出文本） */
export interface WorkflowRunDto {
  id: number
  workflowId: number
  status: RunStatus
  inputs: string
  output: string
  trace: string
  errorMessage: string
  startedAt: number
  finishedAt: number
  createdAt: number
}

/** 试运行请求体：start 声明输入的字符串值（名 -> 值） */
export interface RunRequest {
  inputs: Record<string, string>
}

// ---- 画布图结构（definition 解析后） ----

export interface XYPosition {
  x: number
  y: number
}

export interface WfNode {
  id: string
  type: WfNodeType
  title: string
  position: XYPosition
  config: Record<string, unknown>
}

/** Vue Flow 节点的 data 载荷（标题 + 节点类型相关 config）。 */
export interface FlowNodeData {
  title: string
  config: Record<string, unknown>
}

export interface WfEdge {
  id: string
  source: string
  target: string
  /** condition 用：case 的 handle 或 'else'；其余节点为空 */
  sourceHandle?: string
}

export interface WorkflowDefinition {
  version: string
  nodes: WfNode[]
  edges: WfEdge[]
}

// ---- 各节点 config 形状（NodePanel 编辑用） ----

export interface StartInput {
  name: string
  type: string
  required: boolean
}

export interface StartConfig {
  inputs: StartInput[]
}

export interface LlmParams {
  temperature?: number
  maxTokens?: number
  topP?: number
}

export interface LlmConfig {
  modelId: number
  systemPrompt: string
  prompt: string
  params: LlmParams
}

export interface ToolConfig {
  mcpToolId: number
  args: Record<string, string>
}

export interface ConditionCase {
  handle: string
  left: string
  op: ConditionOp
  right: string
}

export interface ConditionConfig {
  cases: ConditionCase[]
}

export interface EndConfig {
  output: string
}

// ---- 逐节点执行轨迹（run.trace 解析后） ----

export interface NodeTrace {
  nodeId: string
  type: string
  status: RunStatus
  ms: number
  output?: Record<string, unknown>
  error?: string
}
