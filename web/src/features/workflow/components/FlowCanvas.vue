<script setup lang="ts">
import { MarkerType, VueFlow, type DefaultEdgeOptions, type NodeMouseEvent } from '@vue-flow/core'

import '@vue-flow/core/dist/style.css'
import '@vue-flow/core/dist/theme-default.css'

import { FLOW_ID, useFlowGraph } from '../composables/useFlowGraph'
import { useWorkflowEditorStore } from '../store'

const store = useWorkflowEditorStore()
const { nodeTypes, isValidConnection, handleConnect } = useFlowGraph()

// 所有边默认带箭头（指向 target），表达执行方向。
const defaultEdgeOptions: DefaultEdgeOptions = { markerEnd: MarkerType.ArrowClosed }

function onNodeClick(payload: NodeMouseEvent): void {
  store.selectedNodeId = payload.node.id
}

function onPaneClick(): void {
  store.selectedNodeId = null
}
</script>

<template>
  <div class="flow-canvas">
    <VueFlow
      :id="FLOW_ID"
      :node-types="nodeTypes"
      :is-valid-connection="isValidConnection"
      :default-edge-options="defaultEdgeOptions"
      fit-view-on-init
      @connect="handleConnect"
      @node-click="onNodeClick"
      @pane-click="onPaneClick"
    />
  </div>
</template>

<style scoped>
.flow-canvas {
  width: 100%;
  height: 100%;
  background: var(--el-fill-color-lighter);
}
</style>
