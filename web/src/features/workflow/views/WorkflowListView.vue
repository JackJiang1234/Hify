<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'

import { workflowApi } from '@/api/workflow'
import type { WorkflowDto } from '../types'
import { formatEpochMs, workflowStatusMeta } from '../constants'

const router = useRouter()

const workflows = ref<WorkflowDto[]>([])
const total = ref(0)
const page = ref(1)
const size = ref(20)
const loading = ref(false)
const publishingId = ref<number | null>(null)

async function load(): Promise<void> {
  loading.value = true
  try {
    const result = await workflowApi.list({ page: page.value, size: size.value })
    workflows.value = result.items
    total.value = result.total
  } catch {
    // 拦截器已统一提示
  } finally {
    loading.value = false
  }
}

function openCreate(): void {
  void router.push('/workflows/new')
}

function openEdit(workflow: WorkflowDto): void {
  void router.push(`/workflows/${workflow.id}`)
}

async function publish(workflow: WorkflowDto): Promise<void> {
  publishingId.value = workflow.id
  try {
    const updated = await workflowApi.publish(workflow.id)
    Object.assign(workflow, updated)
    ElMessage.success('已发布')
  } catch {
    // 校验失败（6002）等由拦截器提示；状态保持 draft
  } finally {
    publishingId.value = null
  }
}

async function remove(workflow: WorkflowDto): Promise<void> {
  await ElMessageBox.confirm(`确认删除工作流「${workflow.name}」？`, '提示', { type: 'warning' })
  await workflowApi.remove(workflow.id)
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
  <div class="workflow-list">
    <div class="toolbar">
      <el-button type="primary" @click="openCreate">新建工作流</el-button>
      <el-button :loading="loading" @click="load">刷新</el-button>
      <span class="spacer" />
      <span class="muted">线性 + 单层条件分支，拖拽编排</span>
    </div>

    <el-table v-loading="loading" :data="workflows" empty-text="暂无工作流" row-key="id" border>
      <el-table-column prop="name" label="名称" min-width="160" />
      <el-table-column prop="description" label="描述" min-width="200" show-overflow-tooltip />
      <el-table-column label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="workflowStatusMeta(row.status).type" size="small">
            {{ workflowStatusMeta(row.status).label }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="更新时间" width="180">
        <template #default="{ row }">
          <span class="muted">{{ formatEpochMs(row.updatedAt) }}</span>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="220" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button link type="primary" :loading="publishingId === row.id" @click="publish(row)">
            发布
          </el-button>
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
