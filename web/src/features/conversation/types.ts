/** 聊天面板中一条消息的视图模型（历史消息与流式生成中的消息统一用它）。 */
export type ChatMessageStatus = 'completed' | 'streaming' | 'failed' | 'cancelled'

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
}
