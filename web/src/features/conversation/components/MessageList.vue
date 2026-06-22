<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'

import MessageBubble from './MessageBubble.vue'
import type { ChatMessage } from '../types'

const props = defineProps<{
  messages: ChatMessage[]
  hasMore: boolean
  historyLoading: boolean
  assistantLabel: string
}>()

const emit = defineEmits<{
  loadEarlier: []
  retry: [message: ChatMessage]
}>()

const scroller = ref<HTMLElement | null>(null)

function isNearBottom(): boolean {
  const el = scroller.value
  if (!el) {
    return true
  }
  return el.scrollHeight - el.scrollTop - el.clientHeight < 120
}

function scrollToBottom(): void {
  const el = scroller.value
  if (el) {
    el.scrollTop = el.scrollHeight
  }
}

// 新增消息：滚到底。
watch(
  () => props.messages.length,
  () => {
    void nextTick(scrollToBottom)
  },
)

// 流式增量：仅当用户停留在底部附近时跟随滚动，避免打断向上翻看。
watch(
  () => props.messages[props.messages.length - 1]?.content,
  () => {
    if (isNearBottom()) {
      void nextTick(scrollToBottom)
    }
  },
)
</script>

<template>
  <div ref="scroller" class="list">
    <div class="inner">
      <div v-if="hasMore" class="earlier">
        <el-button text :loading="historyLoading" @click="emit('loadEarlier')"
          >加载更早消息</el-button
        >
      </div>

      <MessageBubble
        v-for="message in messages"
        :key="message.id"
        :message="message"
        :assistant-label="assistantLabel"
        @retry="emit('retry', message)"
      />
    </div>
  </div>
</template>

<style scoped>
.list {
  flex: 1;
  min-height: 0; /* 关键：否则 flex 子项按内容撑高，不会滚动 */
  overflow-y: auto;
  padding: var(--space-6, 24px) 0;
}

.inner {
  max-width: 760px;
  margin: 0 auto;
  padding: 0 var(--space-6, 24px);
  display: flex;
  flex-direction: column;
  gap: var(--space-6, 24px);
}

.earlier {
  display: flex;
  justify-content: center;
}
</style>
