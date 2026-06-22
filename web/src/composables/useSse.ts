/**
 * 对话流式封装：用原生 fetch + ReadableStream（axios 不适合流式响应），对齐后端 SSE 120s 超时。
 *
 * 后端两种返回：
 *   1. 准备阶段失败（会话/Agent/模型/上游初始错误）：HTTP 200 + JSON Result（非流），由 onResultError 处理。
 *   2. 进入流式：text/event-stream，逐帧 `data: {json}\n\n`，解析为 ChatEvent 交 onEvent。
 */

/** 与后端 ChatEventSerializer 对齐的流式事件。 */
export type ChatEvent =
  | { type: 'delta'; text: string }
  | {
      type: 'done'
      messageId: number
      finishReason: string
      promptTokens: number
      completionTokens: number
    }
  | { type: 'error'; code: number; message: string }

export interface ChatStreamHandlers {
  /** 收到一个已解析的流式事件。 */
  onEvent: (event: ChatEvent) => void
  /** 准备阶段失败：后端返回 JSON Result（非流），携带四位业务码。 */
  onResultError?: (code: number, message: string) => void
  /** 网络/超时/中断等传输层错误。 */
  onError?: (error: unknown) => void
  /** 流正常结束（连接关闭）。 */
  onDone?: () => void
}

export interface ChatStreamOptions {
  /** 流式超时（毫秒），默认 120s，与后端一致。 */
  timeoutMs?: number
  /** 外部中断信号（停止生成 / 组件卸载）。 */
  signal?: AbortSignal
}

const SSE_TIMEOUT_MS = 120_000

/** 从一个完整 SSE 事件块中取出 data 负载（多 data: 行按 \n 拼接）。 */
function extractData(frame: string): string | null {
  const dataLines = frame
    .split('\n')
    .filter((line) => line.startsWith('data:'))
    .map((line) => line.slice(line.startsWith('data: ') ? 6 : 5))
  return dataLines.length > 0 ? dataLines.join('\n') : null
}

/**
 * 发起一次流式对话。POST 到 `/conversations/{id}/messages`，按帧解析事件。
 */
export async function streamChat(
  url: string,
  body: object,
  handlers: ChatStreamHandlers,
  options: ChatStreamOptions = {},
): Promise<void> {
  const controller = new AbortController()
  const timeout = options.timeoutMs ?? SSE_TIMEOUT_MS
  const timer = window.setTimeout(() => controller.abort(), timeout)
  options.signal?.addEventListener('abort', () => controller.abort(), { once: true })

  const base = import.meta.env.VITE_API_BASE_URL

  try {
    const response = await fetch(`${base}${url}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
      body: JSON.stringify(body),
      signal: controller.signal,
    })

    if (!response.ok || !response.body) {
      throw new Error(`请求失败：${response.status}`)
    }

    // 准备阶段失败：后端返回 JSON Result（非流），按业务错误处理。
    const contentType = response.headers.get('Content-Type') ?? ''
    if (contentType.includes('application/json')) {
      const result = (await response.json()) as { code: number; message: string }
      handlers.onResultError?.(result.code, result.message)
      return
    }

    const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()
    let buffer = ''
    for (;;) {
      const { value, done } = await reader.read()
      if (done) {
        break
      }
      buffer += value
      // 按空行切分完整事件块；不完整的尾块留在 buffer 等下一片。
      let separator = buffer.indexOf('\n\n')
      while (separator !== -1) {
        const frame = buffer.slice(0, separator)
        buffer = buffer.slice(separator + 2)
        const data = extractData(frame)
        if (data) {
          handlers.onEvent(JSON.parse(data) as ChatEvent)
        }
        separator = buffer.indexOf('\n\n')
      }
    }
    handlers.onDone?.()
  } catch (error) {
    handlers.onError?.(error)
  } finally {
    window.clearTimeout(timer)
  }
}
