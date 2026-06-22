<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'

import { agentApi, type AgentDto } from '@/api/agent'
import { useChatModels } from '../composables/useChatModels'
import AgentFormDialog from '../components/AgentFormDialog.vue'

const agents = ref<AgentDto[]>([])
const total = ref(0)
const page = ref(1)
const size = ref(20)
const loading = ref(false)

const formVisible = ref(false)
const editing = ref<AgentDto | null>(null)

const { models, byId, load: loadModels } = useChatModels()

async function load(): Promise<void> {
  loading.value = true
  try {
    const result = await agentApi.list({ page: page.value, size: size.value })
    agents.value = result.items
    total.value = result.total
  } catch {
    // 拦截器已统一提示
  } finally {
    loading.value = false
  }
}

function modelLabel(modelId: number): string {
  return byId.value.get(modelId)?.name ?? `#${modelId}（不可用）`
}

function openCreate(): void {
  editing.value = null
  formVisible.value = true
}

function openEdit(agent: AgentDto): void {
  editing.value = agent
  formVisible.value = true
}

async function toggleEnabled(agent: AgentDto): Promise<void> {
  try {
    await (agent.enabled ? agentApi.enable(agent.id) : agentApi.disable(agent.id))
  } catch {
    agent.enabled = !agent.enabled // 回滚乐观切换
  }
}

async function remove(agent: AgentDto): Promise<void> {
  await ElMessageBox.confirm(`确认删除 Agent「${agent.name}」？`, '提示', { type: 'warning' })
  await agentApi.remove(agent.id)
  ElMessage.success('已删除')
  await load()
}

function changePage(next: number): void {
  page.value = next
  void load()
}

onMounted(() => {
  void loadModels()
  void load()
})
</script>

<template>
  <div class="agents">
    <div class="toolbar">
      <el-button type="primary" @click="openCreate">新增 Agent</el-button>
      <el-button :loading="loading" @click="load">刷新</el-button>
    </div>

    <el-table v-loading="loading" :data="agents" empty-text="暂无 Agent" row-key="id" border>
      <el-table-column prop="name" label="名称" min-width="140" />
      <el-table-column prop="description" label="描述" min-width="180" show-overflow-tooltip>
        <template #default="{ row }">
          <span :class="{ muted: !row.description }">{{ row.description || '—' }}</span>
        </template>
      </el-table-column>
      <el-table-column label="模型" min-width="160" show-overflow-tooltip>
        <template #default="{ row }">{{ modelLabel(row.modelId) }}</template>
      </el-table-column>
      <el-table-column label="工具" width="80" align="center">
        <template #default="{ row }">{{ row.toolIds.length }}</template>
      </el-table-column>
      <el-table-column label="知识库" width="90" align="center">
        <template #default="{ row }">{{ row.knowledgeBaseIds.length }}</template>
      </el-table-column>
      <el-table-column prop="maxIterations" label="迭代上限" width="90" align="center" />
      <el-table-column label="启用" width="80">
        <template #default="{ row }">
          <el-switch v-model="row.enabled" @change="toggleEnabled(row)" />
        </template>
      </el-table-column>
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      class="pager"
      layout="total, prev, pager, next"
      :total="total"
      :page-size="size"
      :current-page="page"
      @current-change="changePage"
    />

    <AgentFormDialog
      v-model:visible="formVisible"
      :agent="editing"
      :models="models"
      @saved="load"
    />
  </div>
</template>

<style scoped>
.toolbar {
  display: flex;
  gap: 8px;
  margin-bottom: 16px;
}

.pager {
  margin-top: 16px;
  justify-content: flex-end;
}

.muted {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
</style>
