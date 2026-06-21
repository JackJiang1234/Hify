/** Agent 表单的默认值与范围约束，取值与后端校验规则一一对齐。 */

import type { AgentUpsert } from '@/api/agent'

/** 工具/知识库绑定数量上限（对齐后端 AgentValidation.MaxBindings）。 */
export const MAX_BINDINGS = 50

/** 系统提示词最大长度（对齐后端 MaxSystemPromptLength）。 */
export const MAX_SYSTEM_PROMPT = 8000

/** 各数值参数的取值区间（对齐后端校验器）。 */
export const PARAM_RANGE = {
  temperature: { min: 0, max: 2, step: 0.1 },
  topP: { min: 0, max: 1, step: 0.05 },
  maxIterations: { min: 1, max: 20 },
  topK: { min: 1, max: 20 },
  scoreThreshold: { min: 0, max: 1, step: 0.05 },
} as const

/** 新建 Agent 的表单默认值。 */
export function defaultAgentForm(): AgentUpsert {
  return {
    name: '',
    description: '',
    modelId: 0,
    systemPrompt: '',
    modelParams: { temperature: null, topP: null, maxTokens: null },
    retrievalParams: { topK: 3, scoreThreshold: 0 },
    maxIterations: 5,
    toolIds: [],
    knowledgeBaseIds: [],
    enabled: true,
  }
}
