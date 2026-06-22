/**
 * 错误码常量与默认文案，与后端分段约定对齐：
 * 1000-1999 通用 | 2000-2999 Provider | 3000-3999 Agent
 * 4000-4999 Chat | 5000-5999 MCP | 6000-6999 Workflow | 7000-7999 Knowledge
 *
 * 优先展示后端返回的 message；此处仅作为兜底/可本地化覆盖。
 */

export const SUCCESS_CODE = 200

/** 通用段（1000-1999），与后端 ErrorCode 枚举一一对应 */
export const CommonErrorCode = {
  InternalError: 1000,
  ParamInvalid: 1001,
  Unauthorized: 1002,
  Forbidden: 1003,
  NotFound: 1004,
  Conflict: 1005,
  TooManyRequests: 1006,
  Timeout: 1007,
} as const

/** Chat 段（4000-4999），与后端 ChatErrorCode 枚举对应 */
export const ChatErrorCode = {
  ConversationNotFound: 4001,
  AgentUnavailable: 4002,
  ModelUnavailable: 4003,
  InvalidInput: 4004,
  UpstreamLlmFailed: 4005,
  ContextOverflow: 4007,
} as const

/** 模块分段区间，用于在 UI 上归类/染色 */
export const ErrorSegment = {
  Common: [1000, 1999],
  Provider: [2000, 2999],
  Agent: [3000, 3999],
  Chat: [4000, 4999],
  Mcp: [5000, 5999],
  Workflow: [6000, 6999],
  Knowledge: [7000, 7999],
} as const

/** 兜底文案：后端未给 message 时按通用码回退 */
const fallbackMessages: Record<number, string> = {
  [CommonErrorCode.InternalError]: '系统内部错误，请稍后重试',
  [CommonErrorCode.ParamInvalid]: '请求参数有误',
  [CommonErrorCode.Unauthorized]: '未登录或登录已失效',
  [CommonErrorCode.Forbidden]: '无权访问该资源',
  [CommonErrorCode.NotFound]: '资源不存在',
  [CommonErrorCode.Conflict]: '资源状态冲突',
  [CommonErrorCode.TooManyRequests]: '请求过于频繁，请稍后再试',
  [CommonErrorCode.Timeout]: '操作超时',
  [ChatErrorCode.ConversationNotFound]: '会话不存在，可能已被删除',
  [ChatErrorCode.AgentUnavailable]: 'Agent 不存在或已停用',
  [ChatErrorCode.ModelUnavailable]: 'Agent 绑定的模型不可用',
  [ChatErrorCode.InvalidInput]: '输入内容为空或过长',
  [ChatErrorCode.UpstreamLlmFailed]: '上游模型调用失败，请重试',
  [ChatErrorCode.ContextOverflow]: '内容超出模型上下文窗口，请精简后重试',
}

export function resolveErrorMessage(code: number, message: string): string {
  if (message) {
    return message
  }
  return fallbackMessages[code] ?? `请求失败（${code}）`
}
