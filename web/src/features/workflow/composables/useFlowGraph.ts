import { computed, markRaw } from 'vue'
import type { Component } from 'vue'
import { useVueFlow, type Connection, type Edge } from '@vue-flow/core'
import { ElMessage } from 'element-plus'

import WorkflowNode from '../components/nodes/WorkflowNode.vue'
import {
  ancestorsOf,
  createFlowNode,
  fromDefinition,
  isValidConnection as checkConnection,
  outputFieldsOf,
  toDefinition,
  type FlowNode,
} from '../graph'
import { emptyDefinition, parseDefinition, stringifyDefinition } from '../constants'
import { useWorkflowEditorStore } from '../store'
import type { StartInput, WfNodeType } from '../types'

/** 编辑器共用的 Vue Flow 实例 id：编辑器视图与画布、面板都用它取到同一张图。 */
export const FLOW_ID = 'workflow-editor'

/**
 * 画布编辑交互：以 Vue Flow 实例（按 FLOW_ID 共享）为图的单一数据源。
 * 提供节点类型注册、连线约束（线性 + 单层分支）、连线/加点/删点、图装载与读出。
 */
export function useFlowGraph() {
  const store = useWorkflowEditorStore()
  const vf = useVueFlow(FLOW_ID)

  const nodeTypes: Record<WfNodeType, Component> = {
    start: markRaw(WorkflowNode),
    llm: markRaw(WorkflowNode),
    tool: markRaw(WorkflowNode),
    condition: markRaw(WorkflowNode),
    end: markRaw(WorkflowNode),
  }

  function currentNodes(): FlowNode[] {
    return vf.getNodes.value as unknown as FlowNode[]
  }

  function currentEdges(): Edge[] {
    return vf.getEdges.value as unknown as Edge[]
  }

  // 传给 <VueFlow :is-valid-connection>，拖拽时实时校验，挡掉非法连线。
  function isValidConnection(connection: Connection): boolean {
    return checkConnection(connection, currentNodes(), currentEdges())
  }

  // 仅在合法 drop 时触发；用 Vue Flow 的 addEdges 入图（可靠触发渲染）。
  function handleConnect(connection: Connection): void {
    if (!checkConnection(connection, currentNodes(), currentEdges())) {
      ElMessage.warning('连线不合法：仅支持线性 + 单层分支（单入、单出/分支出、无环）')
      return
    }
    vf.addEdges([
      {
        id: `e_${Math.random().toString(36).slice(2, 8)}`,
        source: connection.source,
        target: connection.target,
        sourceHandle: connection.sourceHandle ?? undefined,
      },
    ])
  }

  function addNode(type: WfNodeType): void {
    const count = currentNodes().length
    const node = createFlowNode(type, { x: 120 + (count % 4) * 40, y: 40 + count * 80 })
    vf.addNodes([node])
    store.selectedNodeId = node.id
  }

  function removeNode(nodeId: string): void {
    vf.removeNodes([nodeId], true)
    if (store.selectedNodeId === nodeId) {
      store.selectedNodeId = null
    }
  }

  // 删除 condition 某 case 后，清掉该 handle 的悬空出边。
  function removeBranchEdges(nodeId: string, handle: string): void {
    const dangling = currentEdges().filter(
      (edge) => edge.source === nodeId && edge.sourceHandle === handle,
    )
    if (dangling.length > 0) {
      vf.removeEdges(dangling.map((edge) => edge.id))
    }
  }

  /** 把后端 definition 装入画布（替换现有图）。 */
  function loadDefinition(json: string): void {
    const graph = fromDefinition(parseDefinition(json))
    vf.setNodes(graph.nodes)
    vf.setEdges(graph.edges)
    store.selectedNodeId = null
  }

  /** 清空画布（新建）。 */
  function clear(): void {
    vf.setNodes([])
    vf.setEdges([])
    store.selectedNodeId = null
  }

  /** 读出当前画布为 definition JSON 文本。 */
  function readDefinition(): string {
    return stringifyDefinition(toDefinition(currentNodes(), currentEdges()))
  }

  /** 某节点可引用的上游变量 token（仅前驱链上节点的输出字段）。 */
  function upstreamVariables(nodeId: string): { token: string; label: string }[] {
    const edges = currentEdges()
    const ancestorIds = new Set(ancestorsOf(nodeId, edges))
    const result: { token: string; label: string }[] = []
    for (const node of currentNodes()) {
      if (!ancestorIds.has(node.id)) {
        continue
      }
      for (const field of outputFieldsOf(node)) {
        result.push({
          token: `{{${node.id}.${field}}}`,
          label: `${node.data?.title || node.id} · ${field}`,
        })
      }
    }
    return result
  }

  const selectedNode = computed<FlowNode | null>(() =>
    store.selectedNodeId ? ((vf.findNode(store.selectedNodeId) as FlowNode | undefined) ?? null) : null,
  )

  // start 节点声明的输入（供试运行表单）。
  const startInputs = computed<StartInput[]>(() => {
    const start = currentNodes().find((node) => node.type === 'start')
    const config = start?.data?.config as { inputs?: StartInput[] } | undefined
    return config?.inputs ?? []
  })

  return {
    vf,
    nodeTypes,
    isValidConnection,
    handleConnect,
    addNode,
    removeNode,
    removeBranchEdges,
    loadDefinition,
    clear,
    readDefinition,
    selectedNode,
    startInputs,
    upstreamVariables,
    emptyDefinition,
  }
}
