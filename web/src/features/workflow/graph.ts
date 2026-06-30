/**
 * 画布图的纯工具：默认节点配置、节点创建、Vue Flow ↔ 后端 definition 互转、连线约束。
 * 连线约束（isValidConnection）是「简单拖拽省工」的关键：在前端就把图限制为线性 + 单层分支，
 * 让用户画不出后端简单引擎跑不了的图（单入、单出/分支出、无环）。
 */
import { MarkerType, type Connection, type Edge, type Node } from '@vue-flow/core'

import { NODE_TYPE_META } from './constants'
import type { FlowNodeData, WfNodeType, WorkflowDefinition, XYPosition } from './types'

export type FlowNode = Node<FlowNodeData>

/** 各节点类型的默认 config。 */
export function defaultConfig(type: WfNodeType): Record<string, unknown> {
  switch (type) {
    case 'start':
      return { inputs: [{ name: 'user_input', type: 'string', required: true }] }
    case 'llm':
      return { modelId: 0, systemPrompt: '', prompt: '', params: {} }
    case 'tool':
      return { mcpToolId: 0, args: {} }
    case 'condition':
      return { cases: [{ handle: 'case_1', left: '', op: 'eq', right: '' }] }
    case 'end':
      return { output: '' }
    default:
      return {}
  }
}

let idSeq = 0

/** 生成节点 Id（类型前缀 + 递增 + 短随机，避免同毫秒碰撞；用于变量引用 {{nodeId.field}}）。 */
export function newNodeId(type: WfNodeType): string {
  idSeq += 1
  return `${type}_${idSeq}_${Math.random().toString(36).slice(2, 6)}`
}

/** 新建一个 Vue Flow 节点（带默认配置）。 */
export function createFlowNode(type: WfNodeType, position: XYPosition): FlowNode {
  return {
    id: newNodeId(type),
    type,
    position,
    data: { title: NODE_TYPE_META[type].label, config: defaultConfig(type) },
  }
}

/** Vue Flow 节点/连线 → 后端 definition。 */
export function toDefinition(nodes: FlowNode[], edges: Edge[]): WorkflowDefinition {
  return {
    version: '1',
    nodes: nodes.map((node) => ({
      id: node.id,
      type: (node.type ?? 'start') as WfNodeType,
      title: node.data?.title ?? '',
      position: { x: node.position.x, y: node.position.y },
      config: node.data?.config ?? {},
    })),
    edges: edges.map((edge) => ({
      id: edge.id,
      source: edge.source,
      target: edge.target,
      sourceHandle: edge.sourceHandle ?? '',
    })),
  }
}

/** 后端 definition → Vue Flow 节点/连线。 */
export function fromDefinition(definition: WorkflowDefinition): { nodes: FlowNode[]; edges: Edge[] } {
  return {
    nodes: definition.nodes.map((node) => ({
      id: node.id,
      type: node.type,
      position: node.position ?? { x: 0, y: 0 },
      data: { title: node.title, config: node.config },
    })),
    edges: definition.edges.map((edge) => ({
      id: edge.id,
      source: edge.source,
      target: edge.target,
      sourceHandle: edge.sourceHandle || undefined,
      markerEnd: MarkerType.ArrowClosed,
    })),
  }
}

/** 沿出边能否从 from 到达 goal（用于禁环判断）。 */
function canReach(from: string, goal: string, edges: Edge[]): boolean {
  const visited = new Set<string>()
  const stack = [from]
  while (stack.length > 0) {
    const current = stack.pop() as string
    if (current === goal) {
      return true
    }
    if (visited.has(current)) {
      continue
    }
    visited.add(current)
    for (const edge of edges) {
      if (edge.source === current) {
        stack.push(edge.target)
      }
    }
  }
  return false
}

/** 某节点的全部祖先（沿入边可回溯到的节点 id）。用于「可引用变量」只列前驱链上的节点。 */
export function ancestorsOf(nodeId: string, edges: Edge[]): string[] {
  const result = new Set<string>()
  const stack: string[] = edges.filter((edge) => edge.target === nodeId).map((edge) => edge.source)
  while (stack.length > 0) {
    const current = stack.pop() as string
    if (result.has(current)) {
      continue
    }
    result.add(current)
    for (const edge of edges) {
      if (edge.target === current) {
        stack.push(edge.source)
      }
    }
  }
  return [...result]
}

/** 节点对外可被引用的输出字段名（与后端 NodeOutputField 对齐；condition/end 无输出）。 */
export function outputFieldsOf(node: FlowNode): string[] {
  switch (node.type) {
    case 'start': {
      const inputs = (node.data?.config?.inputs as { name?: string }[] | undefined) ?? []
      return inputs.map((input) => input.name ?? '').filter((name) => name.length > 0)
    }
    case 'llm':
      return ['text']
    case 'tool':
      return ['result']
    default:
      return []
  }
}

/**
 * 连线合法性：强制线性 + 单层分支。拒绝——自环 / 连到 start / 从 end 连出 /
 * 目标已有入边（不支持汇合）/ 非 condition 已有出边 / condition 同一 handle 已用 / 形成环。
 */
export function isValidConnection(connection: Connection | Edge, nodes: FlowNode[], edges: Edge[]): boolean {
  const { source, target } = connection
  if (!source || !target || source === target) {
    return false
  }

  const sourceNode = nodes.find((node) => node.id === source)
  const targetNode = nodes.find((node) => node.id === target)
  if (!sourceNode || !targetNode) {
    return false
  }

  if (targetNode.type === 'start' || sourceNode.type === 'end') {
    return false
  }

  // 目标至多一条入边（不支持汇合）。
  if (edges.some((edge) => edge.target === target)) {
    return false
  }

  const sourceHandle = connection.sourceHandle ?? ''
  if (sourceNode.type === 'condition') {
    // condition 每个 handle 至多一条出边。
    if (edges.some((edge) => edge.source === source && (edge.sourceHandle ?? '') === sourceHandle)) {
      return false
    }
  } else if (edges.some((edge) => edge.source === source)) {
    // 非 condition 至多一条出边。
    return false
  }

  // 加这条边不得形成环（目标已能到达源）。
  return !canReach(target, source, edges)
}
