<script setup lang="ts">
import { computed } from 'vue'

import type { ChatMessage } from '../types'

const props = defineProps<{
  message: ChatMessage
  assistantLabel: string
}>()

defineEmits<{ retry: [] }>()

const isUser = computed(() => props.message.role === 'user')
const isStreaming = computed(() => props.message.status === 'streaming')
const toolRuns = computed(() => props.message.toolCalls ?? [])

/** 美化入参 JSON；非法则原样。 */
function pretty(json: string): string {
  try {
    return JSON.stringify(JSON.parse(json), null, 2)
  } catch {
    return json
  }
}
const showTokens = computed(
  () =>
    !isUser.value &&
    props.message.status === 'completed' &&
    (props.message.completionTokens ?? 0) > 0,
)
</script>

<template>
  <div class="row" :class="{ 'row--user': isUser }">
    <div class="avatar" :class="isUser ? 'avatar--user' : 'avatar--ai'" aria-hidden="true">
      {{ isUser ? '我' : assistantLabel }}
    </div>

    <div class="stack">
      <!-- 工具调用时间线（在最终答之前）；可展开看入参/返回 -->
      <details
        v-for="run in toolRuns"
        :key="run.callId"
        class="toolrun"
        :class="`toolrun--${run.status}`"
      >
        <summary class="toolrun__head">
          <span class="toolrun__ico" aria-hidden="true">🔧</span>
          <span class="toolrun__name">{{ run.name }}</span>
          <span class="toolrun__st">
            <span v-if="run.status === 'running'" class="spin" aria-hidden="true"></span>
            <template v-else-if="run.status === 'ok'">✓ 已完成</template>
            <template v-else>✕ 失败</template>
          </span>
        </summary>
        <div class="toolrun__body">
          <div class="toolrun__lbl">入参</div>
          <pre class="toolrun__code">{{ pretty(run.arguments) }}</pre>
          <template v-if="run.result">
            <div class="toolrun__lbl">返回</div>
            <pre class="toolrun__code">{{ run.result }}</pre>
          </template>
        </div>
      </details>

      <div
        v-if="isUser || !!message.content || (isStreaming && toolRuns.length === 0)"
        class="bubble"
        :class="isUser ? 'bubble--user' : 'bubble--ai'"
        :aria-live="isStreaming ? 'polite' : undefined"
      >
        <!-- 文本独立成 span，避免 pre-wrap 下模板缩进渲染成可见空白 -->
        <span class="bubble__text">{{ message.content }}</span
        ><span v-if="isStreaming" class="caret" aria-hidden="true"></span>
      </div>

      <div v-if="message.status === 'failed'" class="bubble bubble--err">
        ⚠ {{ message.error || '生成失败' }}
      </div>

      <div v-if="message.status === 'cancelled'" class="note">已停止生成</div>

      <div v-if="showTokens" class="meta">
        <span class="ok">✓ 已完成</span>
        <span>↑ {{ message.promptTokens }} · ↓ {{ message.completionTokens }}</span>
      </div>

      <button
        v-if="message.status === 'failed'"
        type="button"
        class="retry"
        @click="$emit('retry')"
      >
        ↻ 重新生成
      </button>
    </div>
  </div>
</template>

<style scoped>
.row {
  display: flex;
  gap: 12px;
  align-items: flex-start;
}

.row--user {
  flex-direction: row-reverse;
}

.avatar {
  flex: 0 0 auto;
  width: 28px;
  height: 28px;
  border-radius: var(--radius-full);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: var(--font-weight-semibold, 600);
}

.avatar--ai {
  color: #fff;
  background: linear-gradient(135deg, var(--violet-500), var(--cyan-500));
}

.avatar--user {
  color: var(--color-text-regular);
  background: var(--color-bg-active);
}

.stack {
  min-width: 0;
  max-width: 78%;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.row--user .stack {
  align-items: flex-end;
}

.bubble {
  padding: 10px 14px;
  border-radius: var(--radius-lg);
  line-height: var(--line-height-relaxed, 1.7);
  font-size: 14px;
  white-space: pre-wrap;
  word-break: break-word;
}

.bubble--ai {
  background: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-top-left-radius: 4px;
  color: var(--color-text-primary);
  box-shadow: var(--shadow-xs);
}

.bubble--user {
  background: var(--color-primary);
  color: var(--color-text-inverse, #fff);
  border-top-right-radius: 4px;
}

.bubble--err {
  background: var(--color-danger-bg);
  border: 1px solid var(--color-danger-border);
  color: var(--color-danger-text);
  border-top-left-radius: 4px;
}

/* ---- 工具调用时间线 ---- */
.toolrun {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md, 8px);
  background: var(--color-bg-surface);
  font-size: 13px;
  overflow: hidden;
}

.toolrun--error {
  border-color: var(--color-danger-border);
  background: var(--color-danger-bg);
}

.toolrun__head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 12px;
  cursor: pointer;
  list-style: none;
  user-select: none;
}

.toolrun__head::-webkit-details-marker {
  display: none;
}

.toolrun__ico {
  font-size: 13px;
}

.toolrun__name {
  font-family: var(--font-mono);
  font-size: 12px;
  color: var(--color-text-primary);
}

.toolrun__st {
  margin-left: auto;
  font-size: 12px;
  color: var(--color-success-text);
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.toolrun--running .toolrun__st {
  color: var(--color-primary-text);
}

.toolrun--error .toolrun__st {
  color: var(--color-danger-text);
}

.toolrun__body {
  padding: 0 12px 10px;
  border-top: 1px solid var(--color-border-light);
}

.toolrun__lbl {
  margin: 10px 0 4px;
  font-size: 11px;
  font-weight: var(--font-weight-medium, 500);
  color: var(--color-text-secondary);
}

.toolrun__code {
  margin: 0;
  padding: 8px 10px;
  max-height: 200px;
  overflow: auto;
  background: var(--color-bg-subtle, #f1f5f9);
  border-radius: var(--radius-sm, 6px);
  font-family: var(--font-mono);
  font-size: 12px;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-word;
}

.spin {
  width: 12px;
  height: 12px;
  border: 2px solid var(--violet-200);
  border-top-color: var(--color-primary);
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.caret {
  display: inline-block;
  width: 7px;
  height: 16px;
  vertical-align: -2px;
  margin-left: 2px;
  background: var(--color-primary);
  border-radius: 1px;
  animation: blink 1s steps(2) infinite;
}

@keyframes blink {
  50% {
    opacity: 0;
  }
}

.meta {
  display: flex;
  gap: 12px;
  font-size: 11px;
  font-family: var(--font-mono);
  color: var(--color-text-placeholder);
}

.meta .ok {
  color: var(--color-success-text);
}

.note {
  font-size: 12px;
  color: var(--color-text-placeholder);
}

.retry {
  align-self: flex-start;
  border: none;
  background: transparent;
  padding: 0;
  font-size: 13px;
  font-weight: var(--font-weight-medium, 500);
  color: var(--color-primary-text);
  cursor: pointer;
}

.retry:focus-visible {
  outline: none;
  box-shadow: var(--shadow-focus);
  border-radius: var(--radius-sm);
}

@media (prefers-reduced-motion: reduce) {
  .caret,
  .spin {
    animation: none;
  }
}
</style>
