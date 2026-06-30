<script setup lang="ts">
import { computed } from 'vue'
import { Handle, Position, type NodeProps } from '@vue-flow/core'

import { ELSE_HANDLE, nodeTypeMeta } from '../../constants'
import type { ConditionCase, FlowNodeData, WfNodeType } from '../../types'

const props = defineProps<NodeProps<FlowNodeData>>()

const meta = computed(() => nodeTypeMeta(props.type))
const title = computed(() => props.data?.title || meta.value.label)

const isStart = computed(() => props.type === 'start')
const isEnd = computed(() => props.type === 'end')
const isCondition = computed(() => props.type === 'condition')

// condition：每个 case 一个 source handle + 一个 else handle，沿底部均匀排布。
const branchHandles = computed<string[]>(() => {
  if (!isCondition.value) {
    return []
  }
  const cases = (props.data?.config?.cases as ConditionCase[] | undefined) ?? []
  return [...cases.map((item) => item.handle), ELSE_HANDLE]
})

function handleLeft(index: number, count: number): string {
  return `${((index + 1) / (count + 1)) * 100}%`
}

const typeLabel = computed(() => meta.value.label)
const tagClass = computed(() => `wf-node__tag wf-node__tag--${props.type as WfNodeType}`)
</script>

<template>
  <div class="wf-node" :class="{ 'wf-node--selected': props.selected }">
    <!-- 入边句柄：start 无 -->
    <Handle v-if="!isStart" type="target" :position="Position.Top" />

    <div class="wf-node__head">
      <span :class="tagClass">{{ typeLabel }}</span>
    </div>
    <div class="wf-node__title">{{ title }}</div>
    <div class="wf-node__id">{{ props.id }}</div>

    <!-- 出边句柄：end 无；condition 多句柄；其余单句柄 -->
    <template v-if="!isEnd">
      <template v-if="isCondition">
        <Handle
          v-for="(handle, index) in branchHandles"
          :key="handle"
          :id="handle"
          type="source"
          :position="Position.Bottom"
          :style="{ left: handleLeft(index, branchHandles.length) }"
        />
        <div class="wf-node__branches">
          <span v-for="handle in branchHandles" :key="handle" class="wf-node__branch">{{ handle }}</span>
        </div>
      </template>
      <Handle v-else type="source" :position="Position.Bottom" />
    </template>
  </div>
</template>

<style scoped>
.wf-node {
  min-width: 150px;
  padding: 8px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  background: var(--el-bg-color);
  box-shadow: 0 1px 3px rgb(0 0 0 / 8%);
  font-size: 12px;
}

.wf-node--selected {
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 2px var(--el-color-primary-light-7);
}

.wf-node__head {
  margin-bottom: 4px;
}

.wf-node__tag {
  display: inline-block;
  padding: 0 6px;
  border-radius: 4px;
  font-size: 11px;
  line-height: 18px;
  color: #fff;
}

.wf-node__tag--start,
.wf-node__tag--end {
  background: var(--el-color-primary);
}

.wf-node__tag--llm {
  background: var(--el-color-success);
}

.wf-node__tag--tool {
  background: var(--el-color-warning);
}

.wf-node__tag--condition {
  background: var(--el-color-info);
}

.wf-node__title {
  color: var(--el-text-color-primary);
  font-weight: 500;
  word-break: break-all;
}

.wf-node__id {
  margin-top: 2px;
  color: var(--el-text-color-secondary);
  font-size: 10px;
  font-family: var(--el-font-family-mono, monospace);
}

.wf-node__branches {
  display: flex;
  justify-content: space-around;
  margin-top: 6px;
  color: var(--el-text-color-secondary);
}

.wf-node__branch {
  font-size: 10px;
}
</style>
