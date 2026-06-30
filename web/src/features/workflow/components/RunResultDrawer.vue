<script setup lang="ts">
import { computed } from 'vue'

import { formatEpochMs, nodeTypeMeta, runStatusMeta } from '../constants'
import type { NodeTrace, WorkflowRunDto } from '../types'

const props = defineProps<{ visible: boolean; run: WorkflowRunDto | null }>()
const emit = defineEmits<{ (event: 'update:visible', value: boolean): void }>()

const trace = computed<NodeTrace[]>(() => {
  if (!props.run?.trace) {
    return []
  }
  try {
    return JSON.parse(props.run.trace) as NodeTrace[]
  } catch {
    return []
  }
})

function outputText(output?: Record<string, unknown>): string {
  return output && Object.keys(output).length > 0 ? JSON.stringify(output) : '—'
}

function close(): void {
  emit('update:visible', false)
}
</script>

<template>
  <el-drawer
    :model-value="visible"
    title="运行结果"
    size="480px"
    @update:model-value="close"
  >
    <template v-if="run">
      <div class="result__head">
        <el-tag :type="runStatusMeta(run.status).type">{{ runStatusMeta(run.status).label }}</el-tag>
        <span class="muted">{{ formatEpochMs(run.startedAt) }}</span>
      </div>

      <el-alert v-if="run.errorMessage" :title="run.errorMessage" type="error" :closable="false" show-icon />

      <div class="result__section">
        <div class="result__label">最终输出</div>
        <pre class="result__output">{{ run.output || '—' }}</pre>
      </div>

      <div class="result__section">
        <div class="result__label">执行轨迹</div>
        <el-timeline>
          <el-timeline-item
            v-for="(item, index) in trace"
            :key="index"
            :type="runStatusMeta(item.status).type"
            :timestamp="`${item.ms} ms`"
          >
            <div class="trace__row">
              <el-tag :type="nodeTypeMeta(item.type).type" size="small">{{ nodeTypeMeta(item.type).label }}</el-tag>
              <span class="trace__id">{{ item.nodeId }}</span>
            </div>
            <div v-if="item.error" class="trace__error">{{ item.error }}</div>
            <div v-else class="trace__output">{{ outputText(item.output) }}</div>
          </el-timeline-item>
        </el-timeline>
      </div>
    </template>
  </el-drawer>
</template>

<style scoped>
.result__head {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
}

.result__section {
  margin-top: 16px;
}

.result__label {
  margin-bottom: 6px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}

.result__output {
  margin: 0;
  padding: 8px;
  border-radius: 6px;
  background: var(--el-fill-color-light);
  white-space: pre-wrap;
  word-break: break-all;
  font-size: 12px;
}

.trace__row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.trace__id {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.trace__output,
.trace__error {
  margin-top: 4px;
  font-size: 12px;
  word-break: break-all;
}

.trace__error {
  color: var(--el-color-danger);
}

.muted {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
</style>
