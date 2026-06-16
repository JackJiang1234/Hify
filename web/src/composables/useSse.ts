/**
 * SSE 流式封装：用原生 fetch + ReadableStream，axios 不适合流式响应。
 * 对齐后端 SSE 流式 120s 超时。调用方负责拼接增量 token。
 */

export interface SseHandlers {
  onMessage: (chunk: string) => void
  onError?: (error: unknown) => void
  onDone?: () => void
}

export interface SseOptions {
  /** 流式超时（毫秒），默认 120s，与后端一致 */
  timeoutMs?: number
  signal?: AbortSignal
}

const SSE_TIMEOUT_MS = 120_000

export async function streamSse(
  url: string,
  body: object,
  handlers: SseHandlers,
  options: SseOptions = {},
): Promise<void> {
  const controller = new AbortController()
  const timeout = options.timeoutMs ?? SSE_TIMEOUT_MS
  const timer = window.setTimeout(() => controller.abort(), timeout)

  // 外部传入的 signal 也能触发中断
  options.signal?.addEventListener('abort', () => controller.abort(), { once: true })

  const base = import.meta.env.VITE_API_BASE_URL

  try {
    const response = await fetch(`${base}${url}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      signal: controller.signal,
    })

    if (!response.ok || !response.body) {
      throw new Error(`SSE 请求失败：${response.status}`)
    }

    const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()
    for (;;) {
      const { value, done } = await reader.read()
      if (done) {
        break
      }
      if (value) {
        handlers.onMessage(value)
      }
    }
    handlers.onDone?.()
  } catch (error) {
    handlers.onError?.(error)
  } finally {
    window.clearTimeout(timer)
  }
}
