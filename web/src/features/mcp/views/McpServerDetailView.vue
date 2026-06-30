<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'

import { mcpApi, type McpServerDto, type McpToolDto } from '@/api/mcp'
import { authTypeLabel, formatEpochMs, statusMeta, transportLabel } from '../constants'
import ToolSchemaDialog from '../components/ToolSchemaDialog.vue'

const route = useRoute()
const router = useRouter()
const serverId = computed(() => Number(route.params.id))

const server = ref<McpServerDto | null>(null)
const tools = ref<McpToolDto[]>([])
const loading = ref(false)
const syncing = ref(false)
const pruning = ref(false)

const schemaVisible = ref(false)
const schemaTool = ref<McpToolDto | null>(null)

const removedCount = computed(() => tools.value.filter((tool) => !tool.available).length)

async function loadServer(): Promise<void> {
  try {
    server.value = await mcpApi.get(serverId.value)
  } catch {
    // 拦截器已统一提示
  }
}

async function loadTools(): Promise<void> {
  loading.value = true
  try {
    tools.value = await mcpApi.listTools(serverId.value)
  } catch {
    // 拦截器已统一提示
  } finally {
    loading.value = false
  }
}

async function syncTools(): Promise<void> {
  syncing.value = true
  try {
    const updated = await mcpApi.syncTools(serverId.value)
    server.value = updated
    await loadTools()
    if (updated.status === 'connected') {
      ElMessage.success(`已同步，发现 ${updated.toolCount} 个工具`)
    } else {
      ElMessage.warning(updated.lastError || '同步失败')
    }
  } catch {
    // 拦截器已统一提示
  } finally {
    syncing.value = false
  }
}

async function pruneRemoved(): Promise<void> {
  await ElMessageBox.confirm(
    `确认清理 ${removedCount.value} 个「已移除」工具？清理后若有 Agent 绑定将失效。`,
    '清理已移除工具',
    { type: 'warning' },
  )
  pruning.value = true
  try {
    const count = await mcpApi.pruneTools(serverId.value)
    ElMessage.success(`已清理 ${count} 个工具`)
    await Promise.all([loadServer(), loadTools()])
  } catch {
    // 拦截器已统一提示
  } finally {
    pruning.value = false
  }
}

async function toggleEnabled(tool: McpToolDto): Promise<void> {
  try {
    await (tool.enabled ? mcpApi.enableTool(tool.id) : mcpApi.disableTool(tool.id))
  } catch {
    tool.enabled = !tool.enabled // 回滚乐观切换
  }
}

function viewSchema(tool: McpToolDto): void {
  schemaTool.value = tool
  schemaVisible.value = true
}

onMounted(() => {
  void loadServer()
  void loadTools()
})
</script>

<template>
  <div class="mcp-detail">
    <div class="header">
      <el-button :icon="ArrowLeft" link @click="router.push('/mcp-servers')">返回</el-button>
      <h2 v-if="server">{{ server.name }}</h2>
      <el-tag v-if="server" :type="statusMeta(server.status).type" size="small">
        {{ statusMeta(server.status).label }}
      </el-tag>
    </div>

    <el-descriptions v-if="server" class="summary" :column="3" border size="small">
      <el-descriptions-item label="端点">{{ server.endpoint }}</el-descriptions-item>
      <el-descriptions-item label="传输">{{
        transportLabel(server.transport)
      }}</el-descriptions-item>
      <el-descriptions-item label="鉴权">{{ authTypeLabel(server.authType) }}</el-descriptions-item>
      <el-descriptions-item label="工具数">{{ server.toolCount }}</el-descriptions-item>
      <el-descriptions-item label="最近同步">{{
        formatEpochMs(server.lastSyncedAt)
      }}</el-descriptions-item>
      <el-descriptions-item label="最近错误">
        <span :class="{ muted: !server.lastError }">{{ server.lastError || '—' }}</span>
      </el-descriptions-item>
    </el-descriptions>

    <div class="toolbar">
      <el-button type="primary" :loading="syncing" @click="syncTools">同步工具</el-button>
      <el-button :loading="pruning" :disabled="removedCount === 0" @click="pruneRemoved">
        清理已移除工具<span v-if="removedCount > 0">（{{ removedCount }}）</span>
      </el-button>
      <span class="spacer" />
      <span class="muted">「可用」由同步发现；「启用」为管理员开关，仅可用工具生效</span>
    </div>

    <el-table v-loading="loading" :data="tools" empty-text="暂无工具，点「同步工具」发现" border>
      <el-table-column label="工具名" min-width="180">
        <template #default="{ row }">
          <span class="tool-name" :class="{ removed: !row.available }">{{ row.name }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="description" label="描述" min-width="240" show-overflow-tooltip />
      <el-table-column label="可用" width="100">
        <template #default="{ row }">
          <el-tag :type="row.available ? 'success' : 'info'" size="small">
            {{ row.available ? '可用' : '已移除' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="启用" width="80">
        <template #default="{ row }">
          <el-switch
            v-model="row.enabled"
            :disabled="!row.available"
            @change="toggleEnabled(row)"
          />
        </template>
      </el-table-column>
      <el-table-column label="入参" width="90">
        <template #default="{ row }">
          <el-button link type="primary" @click="viewSchema(row)">Schema</el-button>
        </template>
      </el-table-column>
    </el-table>

    <ToolSchemaDialog v-model:visible="schemaVisible" :tool="schemaTool" />
  </div>
</template>

<style scoped>
.header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}

.header h2 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
}

.summary {
  margin-bottom: 16px;
}

.toolbar {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 16px;
}

.toolbar .spacer {
  flex: 1;
}

.tool-name {
  font-family: var(--font-mono, monospace);
  font-size: 13px;
}

.tool-name.removed {
  color: var(--el-text-color-secondary);
  text-decoration: line-through;
}

.muted {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
</style>
