<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'

import { knowledgeApi, type KnowledgeBaseDto } from '@/api/knowledge'
import { useEmbeddingModels } from '../composables/useEmbeddingModels'
import KnowledgeBaseFormDialog from '../components/KnowledgeBaseFormDialog.vue'

const router = useRouter()

const items = ref<KnowledgeBaseDto[]>([])
const total = ref(0)
const page = ref(1)
const size = ref(20)
const loading = ref(false)

const formVisible = ref(false)
const editing = ref<KnowledgeBaseDto | null>(null)

const { models, byId, load: loadModels } = useEmbeddingModels()

async function load(): Promise<void> {
  loading.value = true
  try {
    const result = await knowledgeApi.list({ page: page.value, size: size.value })
    items.value = result.items
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

function formatDate(epochMs: number): string {
  return epochMs > 0 ? new Date(epochMs).toLocaleDateString('zh-CN') : '—'
}

function openCreate(): void {
  editing.value = null
  formVisible.value = true
}

function openEdit(kb: KnowledgeBaseDto): void {
  editing.value = kb
  formVisible.value = true
}

function openDetail(kb: KnowledgeBaseDto, tab: 'docs' | 'search'): void {
  void router.push({ name: 'knowledge-base-detail', params: { id: kb.id }, query: { tab } })
}

async function remove(kb: KnowledgeBaseDto): Promise<void> {
  await ElMessageBox.confirm(`确认删除知识库「${kb.name}」？其下文档与向量将一并删除。`, '提示', {
    type: 'warning',
  })
  await knowledgeApi.remove(kb.id)
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
  <div class="knowledge">
    <div class="toolbar">
      <el-button type="primary" @click="openCreate">新建知识库</el-button>
      <el-button :loading="loading" @click="load">刷新</el-button>
    </div>

    <el-table v-loading="loading" :data="items" empty-text="暂无知识库" row-key="id" border>
      <el-table-column prop="name" label="名称" min-width="140" show-overflow-tooltip>
        <template #default="{ row }">
          <el-link type="primary" :underline="false" @click="openDetail(row, 'docs')">{{
            row.name
          }}</el-link>
          <div v-if="row.description" class="sub">{{ row.description }}</div>
        </template>
      </el-table-column>
      <el-table-column label="嵌入模型" min-width="180" show-overflow-tooltip>
        <template #default="{ row }">{{ modelLabel(row.embeddingModelId) }}</template>
      </el-table-column>
      <el-table-column label="文档" width="80" align="center">
        <template #default="{ row }">{{ row.documentCount }}</template>
      </el-table-column>
      <el-table-column label="分块 / 重叠" width="120" align="center">
        <template #default="{ row }">{{ row.chunkSize }} / {{ row.chunkOverlap }}</template>
      </el-table-column>
      <el-table-column label="创建时间" width="120">
        <template #default="{ row }"
          ><span class="muted">{{ formatDate(row.createdAt) }}</span></template
        >
      </el-table-column>
      <el-table-column label="操作" width="230" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openDetail(row, 'docs')">文档</el-button>
          <el-button link type="primary" @click="openDetail(row, 'search')">检索</el-button>
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

    <KnowledgeBaseFormDialog
      v-model:visible="formVisible"
      :kb="editing"
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

.sub {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.muted {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
</style>
