<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'

import { modelApi, type ModelDto, type ProviderDto } from '@/api/provider'
import { modelTypeLabel } from '../constants'
import ProviderModelFormDialog from './ProviderModelFormDialog.vue'

const visible = defineModel<boolean>('visible', { required: true })
const props = defineProps<{ provider: ProviderDto | null }>()

const models = ref<ModelDto[]>([])
const loading = ref(false)
const formVisible = ref(false)
const editing = ref<ModelDto | null>(null)

async function load(): Promise<void> {
  if (!props.provider) {
    return
  }
  loading.value = true
  try {
    models.value = await modelApi.listByProvider(props.provider.id)
  } catch {
    // 错误提示已由拦截器统一弹出
  } finally {
    loading.value = false
  }
}

watch(visible, (open) => {
  if (open) {
    void load()
  }
})

function openCreate(): void {
  editing.value = null
  formVisible.value = true
}

function openEdit(model: ModelDto): void {
  editing.value = model
  formVisible.value = true
}

async function toggleEnabled(model: ModelDto): Promise<void> {
  try {
    await (model.enabled ? modelApi.enable(model.id) : modelApi.disable(model.id))
  } catch {
    model.enabled = !model.enabled // 回滚乐观切换
  }
}

async function setDefault(model: ModelDto): Promise<void> {
  await modelApi.setDefault(model.id)
  ElMessage.success('已设为默认')
  await load()
}

async function remove(model: ModelDto): Promise<void> {
  await ElMessageBox.confirm(`确认删除模型「${model.name}」？`, '提示', { type: 'warning' })
  await modelApi.remove(model.id)
  ElMessage.success('已删除')
  await load()
}
</script>

<template>
  <el-dialog v-model="visible" title="模型管理" width="820px" :close-on-click-modal="false">
    <div class="toolbar">
      <span class="subtitle">{{ props.provider?.name }}</span>
      <el-button type="primary" @click="openCreate">新增模型</el-button>
    </div>

    <el-table v-loading="loading" :data="models" empty-text="暂无模型" row-key="id" border>
      <el-table-column prop="name" label="模型标识" min-width="160" />
      <el-table-column label="类型" width="110">
        <template #default="{ row }">{{ modelTypeLabel(row.modelType) }}</template>
      </el-table-column>
      <el-table-column label="能力" width="150">
        <template #default="{ row }">
          <el-tag v-if="row.supportsStreaming" size="small" class="cap">流式</el-tag>
          <el-tag v-if="row.supportsTools" size="small" class="cap">工具</el-tag>
          <el-tag v-if="row.supportsVision" size="small" class="cap">视觉</el-tag>
          <span v-if="row.modelType === 'embedding'">{{ row.embeddingDimensions }} 维</span>
        </template>
      </el-table-column>
      <el-table-column label="默认" width="80">
        <template #default="{ row }">
          <el-tag v-if="row.isDefault" type="success" size="small">默认</el-tag>
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column label="启用" width="80">
        <template #default="{ row }">
          <el-switch v-model="row.enabled" @change="toggleEnabled(row)" />
        </template>
      </el-table-column>
      <el-table-column label="操作" width="220" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" :disabled="row.isDefault" @click="setDefault(row)">
            设默认
          </el-button>
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <ProviderModelFormDialog
      v-if="props.provider"
      v-model:visible="formVisible"
      :provider-id="props.provider.id"
      :model="editing"
      @saved="load"
    />
  </el-dialog>
</template>

<style scoped>
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.subtitle {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.cap {
  margin-right: 4px;
}
</style>
