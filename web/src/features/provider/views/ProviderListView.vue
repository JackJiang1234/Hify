<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'

import { providerApi, type ProviderDto } from '@/api/provider'
import { formatEpochMs, healthMeta, providerTypeLabel } from '../constants'
import ProviderFormDialog from '../components/ProviderFormDialog.vue'
import ProviderModelsDialog from '../components/ProviderModelsDialog.vue'

const providers = ref<ProviderDto[]>([])
const total = ref(0)
const page = ref(1)
const size = ref(20)
const loading = ref(false)
const testingId = ref<number | null>(null)

const formVisible = ref(false)
const editing = ref<ProviderDto | null>(null)
const modelsVisible = ref(false)
const modelsProvider = ref<ProviderDto | null>(null)

async function load(): Promise<void> {
  loading.value = true
  try {
    const result = await providerApi.list({ page: page.value, size: size.value })
    providers.value = result.items
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

function openEdit(provider: ProviderDto): void {
  editing.value = provider
  formVisible.value = true
}

function openModels(provider: ProviderDto): void {
  modelsProvider.value = provider
  modelsVisible.value = true
}

async function toggleEnabled(provider: ProviderDto): Promise<void> {
  try {
    await (provider.enabled ? providerApi.enable(provider.id) : providerApi.disable(provider.id))
  } catch {
    provider.enabled = !provider.enabled // 回滚乐观切换
  }
}

async function testConnection(provider: ProviderDto): Promise<void> {
  testingId.value = provider.id
  try {
    const health = await providerApi.testConnection(provider.id)
    provider.health = health
    if (health.status === 'healthy') {
      ElMessage.success(`连通正常（${health.latencyMs}ms）`)
    } else {
      ElMessage.warning(health.lastError || '连通异常')
    }
  } catch {
    // 拦截器已统一提示
  } finally {
    testingId.value = null
  }
}

async function remove(provider: ProviderDto): Promise<void> {
  await ElMessageBox.confirm(
    `确认删除供应商「${provider.name}」？其下模型将一并删除。`,
    '提示',
    { type: 'warning' },
  )
  await providerApi.remove(provider.id)
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
  <div class="providers">
    <div class="toolbar">
      <el-button type="primary" :icon="undefined" @click="openCreate">新增供应商</el-button>
      <el-button :loading="loading" @click="load">刷新</el-button>
    </div>

    <el-table v-loading="loading" :data="providers" empty-text="暂无供应商" row-key="id" border>
      <el-table-column prop="name" label="名称" min-width="140" />
      <el-table-column label="类型" width="140">
        <template #default="{ row }">{{ providerTypeLabel(row.providerType) }}</template>
      </el-table-column>
      <el-table-column prop="baseUrl" label="API 基址" min-width="200" show-overflow-tooltip />
      <el-table-column label="密钥" width="110">
        <template #default="{ row }">
          <span class="muted">{{ row.apiKeyHint || '—' }}</span>
        </template>
      </el-table-column>
      <el-table-column label="健康" width="150">
        <template #default="{ row }">
          <el-tooltip
            :disabled="!row.health.lastError && row.health.checkedAt === 0"
            placement="top"
          >
            <template #content>
              <div>最近探测：{{ formatEpochMs(row.health.checkedAt) }}</div>
              <div v-if="row.health.lastError">原因：{{ row.health.lastError }}</div>
            </template>
            <span>
              <el-tag :type="healthMeta(row.health.status).type" size="small">
                {{ healthMeta(row.health.status).label }}
              </el-tag>
              <span v-if="row.health.status === 'healthy'" class="muted"> {{ row.health.latencyMs }}ms</span>
            </span>
          </el-tooltip>
        </template>
      </el-table-column>
      <el-table-column label="启用" width="80">
        <template #default="{ row }">
          <el-switch v-model="row.enabled" @change="toggleEnabled(row)" />
        </template>
      </el-table-column>
      <el-table-column label="操作" width="280" fixed="right">
        <template #default="{ row }">
          <el-button
            link
            type="primary"
            :loading="testingId === row.id"
            @click="testConnection(row)"
          >
            测试连接
          </el-button>
          <el-button link type="primary" @click="openModels(row)">模型</el-button>
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

    <ProviderFormDialog v-model:visible="formVisible" :provider="editing" @saved="load" />
    <ProviderModelsDialog v-model:visible="modelsVisible" :provider="modelsProvider" />
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
