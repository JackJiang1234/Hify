<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowDown } from '@element-plus/icons-vue'

import { workflowApi } from '@/api/workflow'
import FlowCanvas from '../components/FlowCanvas.vue'
import NodePanel from '../components/NodePanel.vue'
import RunDialog from '../components/RunDialog.vue'
import RunResultDrawer from '../components/RunResultDrawer.vue'
import { useFlowGraph } from '../composables/useFlowGraph'
import { NODE_TYPE_META, formatEpochMs, runStatusMeta, workflowStatusMeta } from '../constants'
import { useWorkflowEditorStore } from '../store'
import type { WfNodeType, WorkflowRunDto } from '../types'

const route = useRoute()
const router = useRouter()
const store = useWorkflowEditorStore()
const { addNode, loadDefinition, clear, readDefinition, startInputs } = useFlowGraph()

const loading = ref(false)
const saving = ref(false)
const publishing = ref(false)

const runVisible = ref(false)
const running = ref(false)
const resultVisible = ref(false)
const currentRun = ref<WorkflowRunDto | null>(null)
const historyVisible = ref(false)
const historyLoading = ref(false)
const runs = ref<WorkflowRunDto[]>([])

const NODE_TYPES = Object.keys(NODE_TYPE_META) as WfNodeType[]

async function init(): Promise<void> {
  const id = route.params.id
  if (!id) {
    store.reset()
    clear()
    return
  }
  loading.value = true
  try {
    const dto = await workflowApi.get(Number(id))
    store.setMeta(dto)
    loadDefinition(dto.definition)
  } catch {
    void router.replace('/workflows')
  } finally {
    loading.value = false
  }
}

async function save(): Promise<boolean> {
  if (!store.name.trim()) {
    ElMessage.warning('请填写工作流名称')
    return false
  }
  saving.value = true
  try {
    const body = { name: store.name, description: store.description, definition: readDefinition() }
    if (store.isNew) {
      const dto = await workflowApi.create(body)
      store.workflowId = dto.id
      store.status = dto.status
      void router.replace(`/workflows/${dto.id}`)
    } else {
      const dto = await workflowApi.update(store.workflowId as number, body)
      store.status = dto.status
    }
    ElMessage.success('已保存')
    return true
  } catch {
    return false
  } finally {
    saving.value = false
  }
}

async function publish(): Promise<void> {
  // 发布校验的是已存定义，先保存再发布。
  if (!(await save())) {
    return
  }
  publishing.value = true
  try {
    const dto = await workflowApi.publish(store.workflowId as number)
    store.status = dto.status
    ElMessage.success('已发布')
  } catch {
    // 图校验失败（6002）由拦截器提示，状态保持 draft
  } finally {
    publishing.value = false
  }
}

function onAddNode(type: WfNodeType): void {
  addNode(type)
}

// 试运行：先保存（后端跑的是已存定义），再弹输入表单。
async function openRun(): Promise<void> {
  if (!(await save())) {
    return
  }
  runVisible.value = true
}

async function doRun(values: Record<string, string>): Promise<void> {
  running.value = true
  try {
    const run = await workflowApi.run(store.workflowId as number, { inputs: values })
    currentRun.value = run
    runVisible.value = false
    resultVisible.value = true
  } catch {
    // 拦截器已提示（预检失败 6001/6002）；执行失败仍返回 run，不会进此分支
  } finally {
    running.value = false
  }
}

async function openHistory(): Promise<void> {
  historyVisible.value = true
  if (store.workflowId === null) {
    runs.value = []
    return
  }
  historyLoading.value = true
  try {
    const page = await workflowApi.listRuns(store.workflowId, { page: 1, size: 20 })
    runs.value = page.items
  } catch {
    // 拦截器已提示
  } finally {
    historyLoading.value = false
  }
}

async function viewRun(runId: number): Promise<void> {
  try {
    currentRun.value = await workflowApi.getRun(store.workflowId as number, runId)
    historyVisible.value = false
    resultVisible.value = true
  } catch {
    // 拦截器已提示
  }
}

onMounted(init)
</script>

<template>
  <div v-loading="loading" class="editor">
    <div class="editor__toolbar">
      <el-button link @click="router.push('/workflows')">← 返回</el-button>
      <el-input v-model="store.name" placeholder="工作流名称" class="editor__name" />
      <el-input v-model="store.description" placeholder="描述（可选）" class="editor__desc" />
      <el-tag :type="workflowStatusMeta(store.status).type" size="small">
        {{ workflowStatusMeta(store.status).label }}
      </el-tag>
      <span class="editor__spacer" />

      <el-dropdown @command="onAddNode">
        <el-button>+ 节点<el-icon class="el-icon--right"><ArrowDown /></el-icon></el-button>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item v-for="type in NODE_TYPES" :key="type" :command="type">
              {{ NODE_TYPE_META[type].label }}
            </el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>

      <el-button :loading="saving" @click="save">保存</el-button>
      <el-button :loading="running" @click="openRun">试运行</el-button>
      <el-button :disabled="store.isNew" @click="openHistory">运行记录</el-button>
      <el-button type="primary" :loading="publishing" @click="publish">发布</el-button>
    </div>

    <div class="editor__body">
      <div class="editor__canvas">
        <FlowCanvas />
      </div>
      <div class="editor__panel">
        <NodePanel />
      </div>
    </div>

    <RunDialog
      v-model:visible="runVisible"
      :inputs="startInputs"
      :running="running"
      @run="doRun"
    />
    <RunResultDrawer v-model:visible="resultVisible" :run="currentRun" />

    <el-dialog v-model="historyVisible" title="运行记录" width="560px">
      <el-table v-loading="historyLoading" :data="runs" empty-text="暂无运行记录" border>
        <el-table-column label="状态" width="90">
          <template #default="{ row }">
            <el-tag :type="runStatusMeta(row.status).type" size="small">
              {{ runStatusMeta(row.status).label }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="开始时间" min-width="170">
          <template #default="{ row }">
            <span class="muted">{{ formatEpochMs(row.startedAt) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="90">
          <template #default="{ row }">
            <el-button link type="primary" @click="viewRun(row.id)">查看</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-dialog>
  </div>
</template>

<style scoped>
.editor {
  display: flex;
  flex-direction: column;
  height: calc(100vh - var(--layout-header-height) - 2 * var(--space-6));
}

.editor__toolbar {
  display: flex;
  gap: 8px;
  align-items: center;
  padding-bottom: 12px;
}

.editor__name {
  width: 220px;
}

.editor__desc {
  width: 260px;
}

.editor__spacer {
  flex: 1;
}

.editor__body {
  display: flex;
  flex: 1;
  min-height: 0;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  overflow: hidden;
}

.editor__canvas {
  flex: 1;
  min-width: 0;
}

.editor__panel {
  width: 340px;
  border-left: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
}

.muted {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
</style>
