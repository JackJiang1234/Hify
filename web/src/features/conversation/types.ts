/** 聊天面板中一条消息的视图模型（历史消息与流式生成中的消息统一用它）。 */
export type ChatMessageStatus = 'completed' | 'streaming' | 'failed' | 'cancelled'

/** 一次工具调用的运行态（流式工具循环中累积；可展开看入参/返回）。 */
export interface ToolRun {
  /** 调用关联 Id（对应后端 tool_call/tool_result 的 callId）。 */
  callId: string
  /** 工具名。 */
  name: string
  /** 入参 JSON（原样字符串）。 */
  arguments: string
  /** 运行状态：调用中 / 成功 / 失败。 */
  status: 'running' | 'ok' | 'error'
  /** 工具返回内容（完成后填充，已截断）。 */
  result?: string
}

export interface ChatMessage {
  /** 消息 Id；流式占位（尚未落库）时为 0。 */
  id: number
  /** 角色：user | assistant。 */
  role: string
  /** 内容（流式时逐字累积）。 */
  content: string
  /** 状态。 */
  status: ChatMessageStatus
  /** 结束原因（仅 assistant 完成时）。 */
  finishReason?: string
  /** 输入 token 用量。 */
  promptTokens?: number
  /** 输出 token 用量。 */
  completionTokens?: number
  /** 失败提示（流中途错误帧）。 */
  error?: string
  /** 本轮工具调用时间线（仅 assistant；无工具时为空/缺省）。 */
  toolCalls?: ToolRun[]
}
