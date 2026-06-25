<script setup lang="ts">
import { reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'

import { knowledgeApi, type KnowledgeChunkDto } from '@/api/knowledge'
import { SEARCH_RANGE } from '../constants'

const props = defineProps<{ kbId: number }>()

const form = reactive({ query: '', topK: 3, scoreThreshold: 0 })
const results = ref<KnowledgeChunkDto[]>([])
const searched = ref(false)
const loading = ref(false)

async function search(): Promise<void> {
  if (form.query.trim().length === 0) {
    ElMessage.warning('请输入查询文本')
    return
  }
  loading.value = true
  try {
    results.value = await knowledgeApi.search(props.kbId, { ...form })
    searched.value = true
  } catch {
    // 拦截器已统一提示
  } finally {
    loading.value = false
  }
}

// 相似度配色：高(主色) / 中(辅色) / 低(中性)
function scoreClass(score: number): string {
  if (score >= 0.8) {
    return 'score--high'
  }
  return score >= 0.6 ? 'score--mid' : 'score--low'
}
</script>

<template>
  <div class="search-grid">
    <div class="panel">
      <el-form label-position="top">
        <el-form-item label="查询文本">
          <el-input
            v-model="form.query"
            type="textarea"
            :rows="3"
            maxlength="2000"
            placeholder="如：退货多少天内可以办理？"
            @keyup.ctrl.enter="search"
          />
        </el-form-item>
        <div class="row2">
          <el-form-item label="TopK">
            <el-input-number
              v-model="form.topK"
              :min="SEARCH_RANGE.topK.min"
              :max="SEARCH_RANGE.topK.max"
              controls-position="right"
            />
          </el-form-item>
          <el-form-item label="相似度阈值">
            <el-input-number
              v-model="form.scoreThreshold"
              :min="SEARCH_RANGE.scoreThreshold.min"
              :max="SEARCH_RANGE.scoreThreshold.max"
              :step="SEARCH_RANGE.scoreThreshold.step"
              controls-position="right"
            />
          </el-form-item>
        </div>
        <el-button type="primary" :loading="loading" style="width: 100%" @click="search">
          检索
        </el-button>
        <div class="tip">阈值 0 表示不过滤；相似度 = 1 − 余弦距离，越大越相关。</div>
      </el-form>
    </div>

    <div class="panel" v-loading="loading">
      <el-empty v-if="searched && results.length === 0" description="无命中片段" />
      <p v-else-if="!searched" class="placeholder">输入查询并点击「检索」查看命中片段。</p>
      <template v-else>
        <div class="results-head">命中片段 · {{ results.length }} 条</div>
        <div v-for="(chunk, index) in results" :key="index" class="result">
          <div class="result__top">
            <span class="result__src">{{ chunk.documentName }} · 第 {{ chunk.chunkIndex }} 块</span>
            <span class="score" :class="scoreClass(chunk.score)">{{ chunk.score.toFixed(2) }}</span>
          </div>
          <div class="result__body">{{ chunk.content }}</div>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.search-grid {
  display: grid;
  grid-template-columns: 320px 1fr;
  gap: 20px;
  align-items: start;
}

@media (max-width: 860px) {
  .search-grid {
    grid-template-columns: 1fr;
  }
}

.panel {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 12px;
  padding: 18px;
}

.row2 {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.tip {
  margin-top: 10px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  line-height: 1.5;
}

.placeholder {
  color: var(--el-text-color-secondary);
  font-size: 13px;
  text-align: center;
  padding: 32px 0;
}

.results-head {
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 12px;
}

.result {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  padding: 14px 16px;
  margin-bottom: 12px;
}

.result:last-child {
  margin-bottom: 0;
}

.result__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.result__src {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.score {
  font-family: var(--font-mono, monospace);
  font-size: 12px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 6px;
}

.score--high {
  background: var(--violet-50, #eef0ff);
  color: var(--violet-700, #4338ca);
}

.score--mid {
  background: var(--cyan-50, #ecfeff);
  color: var(--cyan-700, #0e7490);
}

.score--low {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
}

.result__body {
  font-size: 13px;
  line-height: 1.65;
  color: var(--el-text-color-regular);
  white-space: pre-wrap;
}
</style>
