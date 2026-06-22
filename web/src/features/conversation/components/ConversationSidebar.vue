<script setup lang="ts">
import { Plus, Delete } from '@element-plus/icons-vue'

import type { ConversationDto } from '@/api/conversation'

defineProps<{
  conversations: ConversationDto[]
  activeId: number | null
  /** 按 agentId 解析 Agent 名称（标题后缀展示）。 */
  resolveAgentName: (agentId: number) => string
}>()

const emit = defineEmits<{
  select: [id: number]
  create: []
  remove: [conversation: ConversationDto]
}>()

function relativeTime(epochMs: number): string {
  if (!epochMs) {
    return ''
  }
  const diff = Date.now() - epochMs
  const minute = 60_000
  const hour = 60 * minute
  const day = 24 * hour
  if (diff < minute) {
    return '刚刚'
  }
  if (diff < hour) {
    return `${Math.floor(diff / minute)} 分钟前`
  }
  if (diff < day) {
    return `${Math.floor(diff / hour)} 小时前`
  }
  return `${Math.floor(diff / day)} 天前`
}
</script>

<template>
  <aside class="sidebar">
    <div class="sidebar__head">
      <el-button type="primary" :icon="Plus" class="sidebar__new" @click="emit('create')">
        新建会话
      </el-button>
    </div>

    <div class="sidebar__list">
      <button
        v-for="conversation in conversations"
        :key="conversation.id"
        type="button"
        class="item"
        :class="{ 'item--active': conversation.id === activeId }"
        @click="emit('select', conversation.id)"
      >
        <span class="item__body">
          <span class="item__title">
            {{ conversation.title || '新会话'
            }}<span class="item__agent">[{{ resolveAgentName(conversation.agentId) }}]</span>
          </span>
          <span class="item__meta">{{ relativeTime(conversation.updatedAt) }}</span>
        </span>
        <el-icon class="item__del" title="删除会话" @click.stop="emit('remove', conversation)">
          <Delete />
        </el-icon>
      </button>

      <p v-if="conversations.length === 0" class="sidebar__empty">还没有会话</p>
    </div>
  </aside>
</template>

<style scoped>
.sidebar {
  display: flex;
  flex-direction: column;
  min-height: 0; /* 网格行内可收缩，下方列表才能滚动 */
  background: var(--color-bg-surface);
  border-right: 1px solid var(--color-border);
}

.sidebar__head {
  padding: var(--space-4, 16px);
  border-bottom: 1px solid var(--color-border-light);
}

.sidebar__new {
  width: 100%;
}

.sidebar__list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: var(--space-2, 8px);
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.item {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 12px;
  border: none;
  background: transparent;
  border-radius: var(--radius-md);
  text-align: left;
  cursor: pointer;
  position: relative;
  transition: var(--transition-colors);
}

.item:hover {
  background: var(--color-bg-hover);
}

.item:focus-visible {
  outline: none;
  box-shadow: var(--shadow-focus);
}

.item--active {
  background: var(--color-primary-subtle);
}

.item--active::before {
  content: '';
  position: absolute;
  left: 0;
  top: 8px;
  bottom: 8px;
  width: 3px;
  border-radius: 3px;
  background: var(--color-primary);
}

.item__body {
  flex: 1;
  min-width: 0;
}

.item__title {
  display: block;
  font-size: 13px;
  font-weight: var(--font-weight-medium, 500);
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.item__agent {
  color: var(--color-text-placeholder);
  font-weight: var(--font-weight-normal, 400);
}

.item__meta {
  display: block;
  margin-top: 3px;
  font-size: 12px;
  color: var(--color-text-placeholder);
}

.item__del {
  flex: 0 0 auto;
  color: var(--color-text-placeholder);
  opacity: 0;
  transition: var(--transition-colors);
}

.item:hover .item__del {
  opacity: 1;
}

.item__del:hover {
  color: var(--color-danger);
}

.sidebar__empty {
  margin: var(--space-6, 24px) 0;
  text-align: center;
  font-size: 13px;
  color: var(--color-text-placeholder);
}
</style>
