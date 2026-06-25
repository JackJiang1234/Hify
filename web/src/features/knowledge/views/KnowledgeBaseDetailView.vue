<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'

import { documentApi, knowledgeApi, type DocumentDto, type KnowledgeBaseDto } from '@/api/knowledge'
import { useEmbeddingModels } from '../composables/useEmbeddingModels'
import DocumentUpload from '../components/DocumentUpload.vue'
import SearchPreviewPanel from '../components/SearchPreviewPanel.vue'

const route = useRoute()
const router = useRouter()
const kbId = computed(() => Number(route.params.id))

const activeTab = ref(route.query.tab === 'search' ? 'search' : 'docs')

const kb = ref<KnowledgeBaseDto | null>(null)
const documents = ref<DocumentDto[]>([])
const total = ref(0)
const page = ref(1)
const size = ref(20)
const docsLoading = ref(false)

const { byId, load: loadModels } = useEmbeddingModels()

const modelName = computed(() =>
  kb.value
    ? (byId.value.get(kb.value.embeddingModelId)?.name ?? `#${kb.value.embeddingModelId}`)
    : '',
)

async function loadKb(): Promise<void> {
  try {
    kb.value = await knowledgeApi.get(kbId.value)
  } catch {
    // 拦截器已统一提示
  }
}

async function loadDocuments(): Promise<void> {
  docsLoading.value = true
  try {
    const result = await documentApi.list(kbId.value, { page: page.value, size: size.value })
    documents.value = result.items
    total.value = result.total
  } catch {
    // 拦截器已统一提示
  } finally {
    docsLoading.value = false
  }
}

// 上传成功：文档数变了，列表与库头一起刷新
async function onUploaded(): Promise<void> {
  page.value = 1
  await Promise.all([loadDocuments(), loadKb()])
}

async function removeDocument(doc: DocumentDto): Promise<void> {
  await ElMessageBox.confirm(`确认删除文档「${doc.name}」？其分块将一并删除。`, '提示', {
    type: 'warning',
  })
  await documentApi.remove(kbId.value, doc.id)
  ElMessage.success('已删除')
  await onUploaded()
}

function changePage(next: number): void {
  page.value = next
  void loadDocuments()
}

function statusType(status: string): 'success' | 'danger' | 'info' | 'warning' {
  if (status === 'completed') {
    return 'success'
  }
  if (status === 'failed') {
    return 'danger'
  }
  return status === 'pending' ? 'warning' : 'info'
}

function statusLabel(status: string): string {
  const map: Record<string, string> = {
    completed: '已完成',
    processing: '处理中',
    pending: '待处理',
    failed: '失败',
  }
  return map[status] ?? status
}

function formatDateTime(epochMs: number): string {
  return epochMs > 0 ? new Date(epochMs).toLocaleString('zh-CN') : '—'
}

function backToList(): void {
  void router.push({ name: 'knowledge-bases' })
}

onMounted(() => {
  void loadModels()
  void loadKb()
  void loadDocuments()
})
</script>

<template>
  <div class="detail">
    <div class="head">
      <div class="head__info">
        <div class="head__title">{{ kb?.name ?? '知识库' }}</div>
        <div v-if="kb" class="head__meta">
          <el-tag type="info" effect="plain" round>{{ modelName }} · 1536 维</el-tag>
          <el-tag type="info" effect="plain" round
            >分块 {{ kb.chunkSize }} / 重叠 {{ kb.chunkOverlap }}</el-tag
          >
          <span class="muted">{{ kb.documentCount }} 个文档</span>
        </div>
      </div>
      <el-button :icon="ArrowLeft" @click="backToList">返回列表</el-button>
    </div>

    <el-tabs v-model="activeTab" class="tabs">
      <el-tab-pane label="文档" name="docs">
        <el-alert
          type="info"
          :closable="false"
          show-icon
          title="同步处理"
          description="上传后即时完成「分块 → 嵌入 → 入库」，可能耗时数秒；成功后文档为「已完成」。若嵌入失败则整体回滚、不留残档，仅提示错误。"
          style="margin-bottom: 16px"
        />

        <DocumentUpload :kb-id="kbId" class="uploader" @uploaded="onUploaded" />

        <el-table
          v-loading="docsLoading"
          :data="documents"
          empty-text="暂无文档"
          row-key="id"
          border
        >
          <el-table-column prop="name" label="文件名" min-width="200" show-overflow-tooltip />
          <el-table-column label="状态" width="100">
            <template #default="{ row }">
              <el-tag :type="statusType(row.status)" effect="light" round>{{
                statusLabel(row.status)
              }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="charCount" label="字符数" width="110" align="right" />
          <el-table-column prop="chunkCount" label="分块" width="80" align="center" />
          <el-table-column label="上传时间" width="180">
            <template #default="{ row }"
              ><span class="muted">{{ formatDateTime(row.createdAt) }}</span></template
            >
          </el-table-column>
          <el-table-column label="操作" width="90" fixed="right">
            <template #default="{ row }">
              <el-button link type="danger" @click="removeDocument(row)">删除</el-button>
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
      </el-tab-pane>

      <el-tab-pane label="检索预览" name="search">
        <el-alert
          type="info"
          :closable="false"
          show-icon
          title="检索预览"
          description="用于管理员调参（TopK / 相似度阈值），验证召回质量；不影响线上对话。"
          style="margin-bottom: 16px"
        />
        <SearchPreviewPanel :kb-id="kbId" />
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<style scoped>
.head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 12px;
  gap: 16px;
}

.head__title {
  font-size: 18px;
  font-weight: 600;
  margin-bottom: 8px;
}

.head__meta {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.uploader {
  margin-bottom: 16px;
}

.pager {
  margin-top: 16px;
  justify-content: flex-end;
}

.muted {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}
</style>
