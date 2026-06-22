<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ChatDotRound, Delete } from '@element-plus/icons-vue'

import { agentApi, type AgentDto } from '@/api/agent'
import type { ConversationDto } from '@/api/conversation'
import { useChatModels } from '@/features/agent/composables/useChatModels'
import { useConversations } from '../composables/useConversations'
import { useChat } from '../composables/useChat'
import type { ChatMessage } from '../types'
import ConversationSidebar from '../components/ConversationSidebar.vue'
import MessageList from '../components/MessageList.vue'
import ChatComposer from '../components/ChatComposer.vue'
import NewConversationDialog from '../components/NewConversationDialog.vue'

const { items: conversations, load: loadConversations, create, remove } = useConversations()
const chat = useChat()
const { byId: modelsById, load: loadModels } = useChatModels()

const agentsById = ref<Map<number, AgentDto>>(new Map())
const dialogVisible = ref(false)

async function loadAgents(): Promise<void> {
  try {
    const result = await agentApi.list({ page: 1, size: 100 })
    agentsById.value = new Map(result.items.map((agent) => [agent.id, agent]))
  } catch {
    // 拦截器已统一提示
  }
}

const activeConversation = computed<ConversationDto | null>(
  () => conversations.value.find((c) => c.id === chat.activeId.value) ?? null,
)
const activeAgent = computed<AgentDto | null>(() =>
  activeConversation.value
    ? (agentsById.value.get(activeConversation.value.agentId) ?? null)
    : null,
)
const agentName = computed(() => activeAgent.value?.name ?? 'Agent')
const assistantLabel = computed(() => agentName.value.slice(0, 1) || 'AI')
const modelName = computed(() => {
  const modelId = activeAgent.value?.modelId
  return modelId ? (modelsById.value.get(modelId)?.name ?? '') : ''
})

function agentNameOf(agentId: number): string {
  return agentsById.value.get(agentId)?.name ?? '未知 Agent'
}

function selectConversation(id: number): void {
  if (id !== chat.activeId.value) {
    void chat.open(id)
  }
}

function openNewDialog(): void {
  dialogVisible.value = true
}

async function onCreate(agentId: number): Promise<void> {
  const conversation = await create(agentId)
  if (conversation) {
    await chat.open(conversation.id)
  }
}

async function onRemove(conversation: ConversationDto): Promise<void> {
  try {
    await ElMessageBox.confirm(`确认删除会话「${conversation.title || '新会话'}」？`, '提示', {
      type: 'warning',
    })
  } catch {
    return // 取消
  }
  const ok = await remove(conversation.id)
  if (ok) {
    ElMessage.success('已删除')
    if (chat.activeId.value === conversation.id) {
      chat.clear()
    }
  }
}

async function handleSend(content: string): Promise<void> {
  await chat.send(content)
  // 首条消息会回填标题、并刷新最近活跃排序。
  await loadConversations()
}

function onRetry(message: ChatMessage): void {
  const index = chat.messages.value.indexOf(message)
  const previousUser = chat.messages.value
    .slice(0, index)
    .reverse()
    .find((m) => m.role === 'user')
  if (previousUser) {
    void handleSend(previousUser.content)
  }
}

onMounted(() => {
  void loadConversations()
  void loadAgents()
  void loadModels()
})
</script>

<template>
  <div class="conv">
    <ConversationSidebar
      :conversations="conversations"
      :active-id="chat.activeId.value"
      :resolve-agent-name="agentNameOf"
      @select="selectConversation"
      @create="openNewDialog"
      @remove="onRemove"
    />

    <section class="chat">
      <template v-if="activeConversation">
        <header class="chat__head">
          <div class="chat__avatar" aria-hidden="true">{{ assistantLabel }}</div>
          <div class="chat__name">{{ agentName }}</div>
          <span v-if="modelName" class="chat__model">{{ modelName }}</span>
          <span class="chat__spacer"></span>
          <el-button text :icon="Delete" title="删除会话" @click="onRemove(activeConversation)" />
        </header>

        <MessageList
          :messages="chat.messages.value"
          :has-more="chat.hasMore.value"
          :history-loading="chat.historyLoading.value"
          :assistant-label="assistantLabel"
          @load-earlier="chat.loadEarlier"
          @retry="onRetry"
        />

        <ChatComposer
          :streaming="chat.streaming.value"
          :disabled="false"
          @send="handleSend"
          @stop="chat.stop"
        />
      </template>

      <div v-else class="empty">
        <div class="empty__glyph">
          <el-icon :size="26"><ChatDotRound /></el-icon>
        </div>
        <h3 class="empty__title">开始你的第一段对话</h3>
        <p class="empty__text">选择一个 Agent，它的模型、提示词与知识库会用于本次对话。</p>
        <el-button type="primary" @click="openNewDialog">新建会话</el-button>
      </div>
    </section>

    <NewConversationDialog v-model:visible="dialogVisible" @confirm="onCreate" />
  </div>
</template>

<style scoped>
/* 抵消 el-main 的 24px 内边距，让对话界面全屏铺满（高度 = 视口 - 顶栏）。 */
.conv {
  margin: calc(-1 * var(--space-6));
  height: calc(100vh - var(--layout-header-height));
  display: grid;
  grid-template-columns: 256px 1fr;
  /* 行高精确等于容器，不随内容撑高，子项才能在内部滚动而非溢出 */
  grid-template-rows: minmax(0, 1fr);
  overflow: hidden;
}

.chat {
  display: flex;
  flex-direction: column;
  min-width: 0;
  min-height: 0; /* 允许收缩到容器高度，内部消息区才会出滚动条 */
  background: var(--color-bg-canvas);
}

.chat__head {
  flex: 0 0 52px;
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: 0 var(--space-5);
  background: var(--color-bg-surface);
  border-bottom: 1px solid var(--color-border);
}

.chat__avatar {
  flex: 0 0 auto;
  width: 30px;
  height: 30px;
  border-radius: var(--radius-full);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 13px;
  font-weight: var(--font-weight-semibold, 600);
  background: linear-gradient(135deg, var(--violet-500), var(--cyan-500));
}

.chat__name {
  font-weight: var(--font-weight-semibold, 600);
  font-size: var(--font-size-md, 16px);
  color: var(--color-text-primary);
}

.chat__model {
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--cyan-700);
  background: var(--cyan-50);
  border: 1px solid var(--cyan-200);
  padding: 1px 7px;
  border-radius: var(--radius-full);
}

.chat__spacer {
  flex: 1;
}

.empty {
  margin: auto;
  text-align: center;
  max-width: 300px;
  padding: var(--space-6);
}

.empty__glyph {
  width: 56px;
  height: 56px;
  margin: 0 auto var(--space-4);
  border-radius: var(--radius-xl);
  background: var(--color-primary-subtle);
  color: var(--color-primary);
  display: flex;
  align-items: center;
  justify-content: center;
}

.empty__title {
  margin: 0;
  font-size: var(--font-size-md, 16px);
  font-weight: var(--font-weight-semibold, 600);
  color: var(--color-text-primary);
}

.empty__text {
  margin: 6px 0 var(--space-4);
  font-size: 13px;
  color: var(--color-text-secondary);
  line-height: 1.6;
}

/* 窄屏：列表收窄，仍保持双栏（移动端抽屉化留待后续）。 */
@media (max-width: 1024px) {
  .conv {
    grid-template-columns: 200px 1fr;
  }
}
</style>
