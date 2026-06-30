<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'

import { mcpApi, type McpServerDto } from '@/api/mcp'
import { authTypeLabel, statusMeta } from '../constants'
import McpServerFormDialog from '../components/McpServerFormDialog.vue'

const router = useRouter()

const servers = ref<McpServerDto[]>([])
const total = ref(0)
const page = ref(1)
const size = ref(20)
const loading = ref(false)
const testingId = ref<number | null>(null)
const syncingId = ref<number | null>(null)

const formVisible = ref(false)
const editing = ref<McpServerDto | null>(null)

async function load(): Promise<void> {
  loading.value = true
  try {
    const result = await mcpApi.list({ page: page.value, size: size.value })
    servers.value = result.items
    total.value = result.total
  } catch {
    // 拦截器已统一提示
  } finally {
    loading.value = false
  }
}

function openCreate(): void {
  editing.value = null
  formVisible.value = true
}

function openEdit(server: McpServerDto): void {
  editing.value = server
  formVisible.value = true
}

function openTools(server: McpServerDto): void {
  void router.push(`/mcp-servers/${server.id}`)
}

async function toggleEnabled(server: McpServerDto): Promise<void> {
  try {
    await (server.enabled ? mcpApi.enable(server.id) : mcpApi.disable(server.id))
  } catch {
    server.enabled = !server.enabled // 回滚乐观切换
  }
}

async function testConnection(server: McpServerDto): Promise<void> {
  testingId.value = server.id
  try {
    const updated = await mcpApi.testConnection(server.id)
    Object.assign(server, updated)
    if (updated.status === 'connected') {
      ElMessage.success('连接正常')
    } else {
      ElMessage.warning(updated.lastError || '连接异常')
    }
  } catch {
    // 拦截器已统一提示
  } finally {
    testingId.value = null
  }
}

async function syncTools(server: McpServerDto): Promise<void> {
  syncingId.value = server.id
  try {
    const updated = await mcpApi.syncTools(server.id)
    Object.assign(server, updated)
    if (updated.status === 'connected') {
      ElMessage.success(`已同步，发现 ${updated.toolCount} 个工具`)
    } else {
      ElMessage.warning(updated.lastError || '同步失败')
    }
  } catch {
    // 拦截器已统一提示
  } finally {
    syncingId.value = null
  }
}

async function remove(server: McpServerDto): Promise<void> {
  await ElMessageBox.confirm(`确认删除「${server.name}」？其下工具将一并删除。`, '提示', {
    type: 'warning',
  })
  await mcpApi.remove(server.id)
  ElMessage.success('已删除')
  await load()
}

function changePage(next: number): void {
  page.value = next
  void load()
}

onMounted(load)
</script>

<template>
  <div class="mcp-servers">
    <div class="toolbar">
      <el-button type="primary" @click="openCreate">新增 Server</el-button>
      <el-button :loading="loading" @click="load">刷新</el-button>
      <span class="spacer" />
      <span class="muted">一期仅支持 Streamable HTTP 传输</span>
    </div>

    <el-table v-loading="loading" :data="servers" empty-text="暂无 MCP Server" row-key="id" border>
      <el-table-column prop="name" label="名称" min-width="140" />
      <el-table-column prop="endpoint" label="端点" min-width="220" show-overflow-tooltip />
      <el-table-column label="鉴权" width="130">
        <template #default="{ row }">
          <span>{{ authTypeLabel(row.authType) }}</span>
          <span v-if="row.authType === 'header' && row.authHeaderName" class="muted">
            · {{ row.authHeaderName }}</span
          >
        </template>
      </el-table-column>
      <el-table-column label="状态" width="130">
        <template #default="{ row }">
          <el-tooltip :disabled="!row.lastError" :content="row.lastError" placement="top">
            <el-tag :type="statusMeta(row.status).type" size="small">
              {{ statusMeta(row.status).label }}
            </el-tag>
          </el-tooltip>
        </template>
      </el-table-column>
      <el-table-column label="工具数" width="80" align="center">
        <template #default="{ row }">
          <span v-if="row.lastSyncedAt > 0">{{ row.toolCount }}</span>
          <span v-else class="muted">—</span>
        </template>
      </el-table-column>
      <el-table-column label="启用" width="70">
        <template #default="{ row }">
          <el-switch v-model="row.enabled" @change="toggleEnabled(row)" />
        </template>
      </el-table-column>
      <el-table-column label="操作" width="320" fixed="right">
        <template #default="{ row }">
          <el-button
            link
            type="primary"
            :loading="testingId === row.id"
            @click="testConnection(row)"
          >
            测试连接
          </el-button>
          <el-button link type="primary" :loading="syncingId === row.id" @click="syncTools(row)">
            同步工具
          </el-button>
          <el-button link type="primary" @click="openTools(row)">工具</el-button>
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

    <McpServerFormDialog v-model:visible="formVisible" :server="editing" @saved="load" />
  </div>
</template>

<style scoped>
.toolbar {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 16px;
}

.toolbar .spacer {
  flex: 1;
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
