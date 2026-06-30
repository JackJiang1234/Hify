import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import type { WorkflowDto, WorkflowStatus } from './types'

/**
 * 工作流编辑器元信息状态：名称/描述/状态/当前工作流 Id/选中节点。
 * 画布图（节点/连线）的单一数据源是 Vue Flow 实例本身（见 useFlowGraph），不再放 store，
 * 以避免「store 数组」与「Vue Flow 内部图」双源不同步导致连线/加点不生效。
 */
export const useWorkflowEditorStore = defineStore('workflow-editor', () => {
  const workflowId = ref<number | null>(null)
  const name = ref('')
  const description = ref('')
  const status = ref<WorkflowStatus>('draft')
  const selectedNodeId = ref<string | null>(null)

  const isNew = computed(() => workflowId.value === null)

  function reset(): void {
    workflowId.value = null
    name.value = ''
    description.value = ''
    status.value = 'draft'
    selectedNodeId.value = null
  }

  /** 设置元信息（不含画布图——图由 useFlowGraph 装载到 Vue Flow）。 */
  function setMeta(dto: WorkflowDto): void {
    workflowId.value = dto.id
    name.value = dto.name
    description.value = dto.description
    status.value = dto.status
    selectedNodeId.value = null
  }

  return { workflowId, name, description, status, selectedNodeId, isNew, reset, setMeta }
})
