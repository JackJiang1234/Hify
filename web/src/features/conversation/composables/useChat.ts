import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'

import { conversationApi, type MessageDto } from '@/api/conversation'
import { resolveErrorMessage } from '@/constants/error-code'
import { streamChat } from '@/composables/useSse'
import type { ChatMessage, ChatMessageStatus } from '../types'

const HISTORY_PAGE_SIZE = 20

// 乐观占位消息的临时 Id（递减负数，避免与真实 Id 冲突）。
let tempIdSeq = -1

function toChatMessage(dto: MessageDto): ChatMessage {
  return {
    id: dto.id,
    role: dto.role,
    content: dto.content,
    status: (dto.status || 'completed') as ChatMessageStatus,
    finishReason: dto.finishReason,
    promptTokens: dto.promptTokens,
    completionTokens: dto.completionTokens,
  }
}

/**
 * 当前打开会话的消息线程 + 流式发送。
 * 历史按 id 倒序分页拉取、反转为旧→新展示，支持向上加载更早；发送走 SSE 流式。
 */
export function useChat() {
  const messages = ref<ChatMessage[]>([])
  const activeId = ref<number | null>(null)
  const streaming = ref(false)
  const historyLoading = ref(false)
  const total = ref(0)
  const pagesLoaded = ref(0)

  let controller: AbortController | null = null
  let stoppedByUser = false

  const hasMore = computed(() => messages.value.length < total.value)

  async function loadPage(page: number): Promise<void> {
    const id = activeId.value
    if (id === null) {
      return
    }
    historyLoading.value = true
    try {
      const result = await conversationApi.history(id, { page, size: HISTORY_PAGE_SIZE })
      const asc = [...result.items].reverse().map(toChatMessage) // 后端 DESC → 旧到新
      if (page === 1) {
        total.value = result.total
        messages.value = asc
      } else {
        messages.value = [...asc, ...messages.value]
      }
      pagesLoaded.value = page
    } catch {
      // 拦截器已统一提示
    } finally {
      historyLoading.value = false
    }
  }

  /** 打开一个会话：重置线程并加载最新一页历史。 */
  async function open(conversationId: number): Promise<void> {
    stop()
    activeId.value = conversationId
    messages.value = []
    total.value = 0
    pagesLoaded.value = 0
    await loadPage(1)
  }

  /** 向上加载更早一页历史。 */
  async function loadEarlier(): Promise<void> {
    if (!hasMore.value || historyLoading.value) {
      return
    }
    await loadPage(pagesLoaded.value + 1)
  }

  /** 清空线程（无选中会话时）。 */
  function clear(): void {
    stop()
    activeId.value = null
    messages.value = []
    total.value = 0
    pagesLoaded.value = 0
  }

  /** 发送一条消息并流式接收回复。完成（含失败）后 resolve。 */
  async function send(content: string): Promise<void> {
    const id = activeId.value
    if (id === null || streaming.value) {
      return
    }

    const userSeed: ChatMessage = {
      id: tempIdSeq--,
      role: 'user',
      content,
      status: 'completed',
    }
    const assistantSeed: ChatMessage = {
      id: tempIdSeq--,
      role: 'assistant',
      content: '',
      status: 'streaming',
      toolCalls: [], // 预置以保证工具时间线响应式
    }
    messages.value = [...messages.value, userSeed, assistantSeed]

    // 关键：从响应式数组取回代理实例再变更——直接改原始对象不会触发渲染。
    const userMessage = messages.value[messages.value.length - 2]
    const assistant = messages.value[messages.value.length - 1]

    streaming.value = true
    stoppedByUser = false
    controller = new AbortController()

    await streamChat(
      `/conversations/${id}/messages`,
      { content },
      {
        onEvent: (event) => {
          if (event.type === 'delta') {
            assistant.content += event.text
          } else if (event.type === 'tool_call') {
            // 工具发起：追加一条运行中的工具记录。
            ;(assistant.toolCalls ??= []).push({
              callId: event.callId,
              name: event.tool,
              arguments: event.arguments,
              status: 'running',
            })
          } else if (event.type === 'tool_result') {
            // 工具完成：按 callId 回填状态与返回。
            const run = assistant.toolCalls?.find((r) => r.callId === event.callId)
            if (run) {
              run.status = event.isError ? 'error' : 'ok'
              run.result = event.result
            }
          } else if (event.type === 'done') {
            assistant.id = event.messageId
            assistant.status = 'completed'
            assistant.finishReason = event.finishReason
            assistant.promptTokens = event.promptTokens
            assistant.completionTokens = event.completionTokens
          } else {
            // 流中途错误帧：保留已生成内容，标记失败。
            assistant.status = 'failed'
            assistant.error = resolveErrorMessage(event.code, event.message)
          }
        },
        onResultError: (code, message) => {
          // 准备阶段失败（头未发出）：移除乐观气泡，toast 提示，由用户重发。
          messages.value = messages.value.filter((m) => m !== userMessage && m !== assistant)
          ElMessage.error(resolveErrorMessage(code, message))
        },
        onError: () => {
          assistant.status = stoppedByUser ? 'cancelled' : 'failed'
          if (!stoppedByUser && !assistant.error) {
            assistant.error = '网络异常，回复中断'
          }
        },
      },
      { signal: controller.signal },
    )

    streaming.value = false
    controller = null
  }

  /** 停止生成（中断流；后端把该条 assistant 落为 cancelled）。 */
  function stop(): void {
    if (controller) {
      stoppedByUser = true
      controller.abort()
    }
  }

  return {
    messages,
    activeId,
    streaming,
    historyLoading,
    hasMore,
    open,
    loadEarlier,
    send,
    stop,
    clear,
  }
}
