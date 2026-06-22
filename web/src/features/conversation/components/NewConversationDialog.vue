<script setup lang="ts">
import { ref, watch } from 'vue'

import { agentApi, type AgentDto } from '@/api/agent'

const props = defineProps<{ visible: boolean }>()
const emit = defineEmits<{
  'update:visible': [value: boolean]
  confirm: [agentId: number]
}>()

const agents = ref<AgentDto[]>([])
const loading = ref(false)
const selectedId = ref<number | null>(null)

async function loadAgents(): Promise<void> {
  loading.value = true
  try {
    const result = await agentApi.list({ page: 1, size: 100 })
    agents.value = result.items.filter((agent) => agent.enabled)
  } catch {
    // 拦截器已统一提示
  } finally {
    loading.value = false
  }
}

// 打开时加载启用的 Agent，并重置选择。
watch(
  () => props.visible,
  (open) => {
    if (open) {
      selectedId.value = null
      void loadAgents()
    }
  },
)

function close(): void {
  emit('update:visible', false)
}

function confirm(): void {
  if (selectedId.value === null) {
    return
  }
  emit('confirm', selectedId.value)
  close()
}
</script>

<template>
  <el-dialog :model-value="visible" title="新建会话" width="420px" @update:model-value="close">
    <div class="field-label">选择 Agent</div>
    <el-select
      v-model="selectedId"
      placeholder="选择一个 Agent"
      :loading="loading"
      filterable
      style="width: 100%"
    >
      <el-option v-for="agent in agents" :key="agent.id" :label="agent.name" :value="agent.id" />
    </el-select>
    <p class="field-hint">对话将使用该 Agent 绑定的模型、系统提示词与知识库。</p>

    <template #footer>
      <el-button @click="close">取消</el-button>
      <el-button type="primary" :disabled="selectedId === null" @click="confirm">创建</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.field-label {
  font-size: 13px;
  font-weight: var(--font-weight-medium, 500);
  color: var(--color-text-regular);
  margin-bottom: 6px;
}

.field-hint {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--color-text-placeholder);
  line-height: 1.5;
}
</style>
