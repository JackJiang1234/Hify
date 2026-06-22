<script setup lang="ts">
import { nextTick, ref } from 'vue'
import { Promotion } from '@element-plus/icons-vue'

const props = defineProps<{
  streaming: boolean
  disabled: boolean
}>()

const emit = defineEmits<{
  send: [content: string]
  stop: []
}>()

const text = ref('')
const textarea = ref<HTMLTextAreaElement | null>(null)

function autoGrow(): void {
  const el = textarea.value
  if (!el) {
    return
  }
  el.style.height = 'auto'
  el.style.height = `${Math.min(el.scrollHeight, 160)}px`
}

function submit(): void {
  const content = text.value.trim()
  if (!content || props.streaming || props.disabled) {
    return
  }
  emit('send', content)
  text.value = ''
  void nextTick(autoGrow)
}

function onKeydown(event: KeyboardEvent): void {
  // Enter 发送；Shift+Enter 换行；Esc 停止生成。
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
    event.preventDefault()
    submit()
  } else if (event.key === 'Escape' && props.streaming) {
    event.preventDefault()
    emit('stop')
  }
}
</script>

<template>
  <div class="composer">
    <div class="inner">
      <div class="box" :class="{ 'box--disabled': disabled }">
        <textarea
          ref="textarea"
          v-model="text"
          class="ta"
          rows="1"
          :disabled="disabled"
          :placeholder="disabled ? '请先选择或新建一个会话' : '输入消息…'"
          aria-label="消息输入框"
          @input="autoGrow"
          @keydown="onKeydown"
        ></textarea>

        <div class="foot">
          <span class="hint"> <kbd>Enter</kbd> 发送 · <kbd>Shift</kbd>+<kbd>Enter</kbd> 换行 </span>

          <el-button v-if="streaming" class="action" @click="emit('stop')">
            <span class="sq" aria-hidden="true"></span>停止
          </el-button>
          <el-button
            v-else
            type="primary"
            class="action"
            :icon="Promotion"
            :disabled="disabled || text.trim().length === 0"
            @click="submit"
          >
            发送
          </el-button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.composer {
  flex: 0 0 auto;
  padding: var(--space-4, 16px) var(--space-6, 24px) var(--space-5, 20px);
  background: var(--color-bg-canvas);
}

.inner {
  max-width: 760px;
  margin: 0 auto;
}

.box {
  background: var(--color-bg-surface);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-lg);
  padding: 12px 12px 8px;
  box-shadow: var(--shadow-sm);
  transition: var(--transition-colors);
}

.box:focus-within {
  border-color: var(--color-primary);
  box-shadow: var(--shadow-focus);
}

.box--disabled {
  background: var(--color-bg-subtle);
}

.ta {
  width: 100%;
  border: none;
  outline: none;
  resize: none;
  background: transparent;
  font-family: inherit;
  font-size: 14px;
  line-height: 1.6;
  color: var(--color-text-primary);
  min-height: 24px;
}

.ta::placeholder {
  color: var(--color-text-placeholder);
}

.foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 8px;
}

.hint {
  font-size: 12px;
  color: var(--color-text-placeholder);
}

.hint kbd {
  font-family: var(--font-mono);
  font-size: 11px;
  background: var(--color-bg-subtle);
  border: 1px solid var(--color-border);
  border-bottom-width: 2px;
  border-radius: 4px;
  padding: 0 5px;
  color: var(--color-text-secondary);
}

.sq {
  display: inline-block;
  width: 9px;
  height: 9px;
  border-radius: 2px;
  background: currentColor;
  margin-right: 6px;
  vertical-align: -1px;
}
</style>
