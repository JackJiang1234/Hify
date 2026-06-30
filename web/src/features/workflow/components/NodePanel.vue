<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'

import { useChatModels } from '@/features/agent/composables/useChatModels'
import { useMcpTools } from '@/features/agent/composables/useMcpTools'
import { CONDITION_OP_OPTIONS, nodeTypeMeta } from '../constants'
import { useFlowGraph } from '../composables/useFlowGraph'
import { useWorkflowEditorStore } from '../store'
import type { ConditionConfig, EndConfig, LlmConfig, StartConfig, ToolConfig } from '../types'

const store = useWorkflowEditorStore()
const { selectedNode, removeNode, removeBranchEdges, upstreamVariables } = useFlowGraph()

const { models, load: loadModels } = useChatModels()
const { groups: toolGroups, load: loadTools } = useMcpTools()

onMounted(() => {
  void loadModels()
  void loadTools()
})

const node = computed(() => selectedNode.value)
const nodeType = computed(() => node.value?.type)
const config = computed(() => node.value?.data?.config as Record<string, unknown> | undefined)

const startConfig = computed(() => config.value as unknown as StartConfig | undefined)
const llmConfig = computed(() => config.value as unknown as LlmConfig | undefined)
const toolConfig = computed(() => config.value as unknown as ToolConfig | undefined)
const conditionConfig = computed(() => config.value as unknown as ConditionConfig | undefined)
const endConfig = computed(() => config.value as unknown as EndConfig | undefined)

const title = computed({
  get: () => node.value?.data?.title ?? '',
  set: (value: string) => {
    if (node.value?.data) {
      node.value.data.title = value
    }
  },
})

// 当前节点可引用的上游变量（前驱链上各节点的输出字段）。
const variables = computed(() => (node.value ? upstreamVariables(node.value.id) : []))

async function copyToken(token: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(token)
    ElMessage.success(`已复制 ${token}`)
  } catch {
    ElMessage.warning(`请手动复制：${token}`)
  }
}

// tool 的 args 用 JSON 文本编辑（值可含 {{nodeId.field}}），失焦时解析回对象。
const argsText = ref('{}')
watch(
  () => store.selectedNodeId,
  () => {
    if (toolConfig.value) {
      argsText.value = JSON.stringify(toolConfig.value.args ?? {}, null, 2)
    }
  },
  { immediate: true },
)

function applyArgs(): void {
  if (!toolConfig.value) {
    return
  }
  try {
    toolConfig.value.args = JSON.parse(argsText.value) as Record<string, string>
  } catch {
    ElMessage.warning('args 不是合法 JSON')
  }
}

function addInput(): void {
  startConfig.value?.inputs.push({ name: '', type: 'string', required: false })
}

function removeInput(index: number): void {
  startConfig.value?.inputs.splice(index, 1)
}

function addCase(): void {
  conditionConfig.value?.cases.push({
    handle: `case_${Math.random().toString(36).slice(2, 5)}`,
    left: '',
    op: 'eq',
    right: '',
  })
}

function removeCase(index: number): void {
  const cases = conditionConfig.value?.cases
  if (!cases) {
    return
  }
  const [removed] = cases.splice(index, 1)
  if (removed && node.value) {
    removeBranchEdges(node.value.id, removed.handle)
  }
}

function setTemperature(value: number | undefined): void {
  if (!llmConfig.value) {
    return
  }
  if (!llmConfig.value.params) {
    llmConfig.value.params = {}
  }
  llmConfig.value.params.temperature = value ?? undefined
}

function remove(): void {
  if (node.value) {
    removeNode(node.value.id)
  }
}
</script>

<template>
  <div class="node-panel">
    <div v-if="!node" class="node-panel__empty">
      <el-text type="info" size="small">选中一个节点以编辑配置</el-text>
    </div>

    <template v-else>
      <div class="node-panel__head">
        <el-tag :type="nodeTypeMeta(node.type ?? '').type" size="small">
          {{ nodeTypeMeta(node.type ?? '').label }}
        </el-tag>
        <el-button link type="danger" size="small" @click="remove">删除节点</el-button>
      </div>

      <div class="node-panel__id">
        <span class="muted">节点 id</span>
        <el-tooltip content="点击复制" placement="top">
          <code class="node-panel__id-code" @click="copyToken(node.id)">{{ node.id }}</code>
        </el-tooltip>
      </div>

      <div v-if="variables.length > 0" class="node-panel__vars">
        <div class="muted node-panel__vars-label">可引用变量（点击复制，粘贴到提示词/条件/输出中）</div>
        <div class="node-panel__vars-list">
          <el-tag
            v-for="item in variables"
            :key="item.token"
            size="small"
            class="var-tag"
            @click="copyToken(item.token)"
          >
            {{ item.token }}
          </el-tag>
        </div>
      </div>

      <el-form label-position="top" size="small">
        <el-form-item label="节点标题">
          <el-input v-model="title" placeholder="节点标题" />
        </el-form-item>

        <!-- start -->
        <template v-if="nodeType === 'start' && startConfig">
          <el-form-item label="输入参数">
            <div class="rows">
              <div v-for="(input, index) in startConfig.inputs" :key="index" class="row">
                <el-input v-model="input.name" placeholder="参数名" />
                <el-checkbox v-model="input.required">必填</el-checkbox>
                <el-button link type="danger" @click="removeInput(index)">删除</el-button>
              </div>
              <el-button link type="primary" @click="addInput">+ 添加输入</el-button>
            </div>
          </el-form-item>
        </template>

        <!-- llm -->
        <template v-else-if="nodeType === 'llm' && llmConfig">
          <el-form-item label="模型">
            <el-select v-model="llmConfig.modelId" placeholder="选择模型" filterable style="width: 100%">
              <el-option
                v-for="model in models"
                :key="model.id"
                :label="`${model.name} · ${model.providerName}`"
                :value="model.id"
              />
            </el-select>
          </el-form-item>
          <el-form-item label="系统提示词">
            <el-input v-model="llmConfig.systemPrompt" type="textarea" :rows="2" />
          </el-form-item>
          <el-form-item label="提示词（可引用 {{节点.字段}}）">
            <el-input v-model="llmConfig.prompt" type="textarea" :rows="4" />
          </el-form-item>
          <el-form-item label="温度">
            <el-input-number
              :model-value="llmConfig.params?.temperature"
              :min="0"
              :max="2"
              :step="0.1"
              controls-position="right"
              @update:model-value="setTemperature($event as number | undefined)"
            />
          </el-form-item>
        </template>

        <!-- tool -->
        <template v-else-if="nodeType === 'tool' && toolConfig">
          <el-form-item label="MCP 工具">
            <el-select v-model="toolConfig.mcpToolId" placeholder="选择工具" filterable style="width: 100%">
              <el-option-group v-for="group in toolGroups" :key="group.serverName" :label="group.serverName">
                <el-option
                  v-for="tool in group.items"
                  :key="tool.id"
                  :label="tool.name + (tool.enabled ? '' : '（已停用）')"
                  :value="tool.id"
                />
              </el-option-group>
            </el-select>
          </el-form-item>
          <el-form-item label="参数 args（JSON，值可引用 {{节点.字段}}）">
            <el-input v-model="argsText" type="textarea" :rows="5" @change="applyArgs" />
          </el-form-item>
        </template>

        <!-- condition -->
        <template v-else-if="nodeType === 'condition' && conditionConfig">
          <el-form-item label="分支条件（首个为真走其分支，否则走 else）">
            <div class="rows">
              <div v-for="(item, index) in conditionConfig.cases" :key="index" class="case">
                <div class="case__handle">{{ item.handle }}</div>
                <el-input v-model="item.left" placeholder="左值 如 {{llm.text}}" />
                <el-select v-model="item.op" style="width: 96px">
                  <el-option
                    v-for="opt in CONDITION_OP_OPTIONS"
                    :key="opt.value"
                    :label="opt.label"
                    :value="opt.value"
                  />
                </el-select>
                <el-input v-model="item.right" placeholder="右值" />
                <el-button link type="danger" @click="removeCase(index)">删除</el-button>
              </div>
              <el-button link type="primary" @click="addCase">+ 添加分支</el-button>
            </div>
          </el-form-item>
        </template>

        <!-- end -->
        <template v-else-if="nodeType === 'end' && endConfig">
          <el-form-item label="输出（可引用 {{节点.字段}}）">
            <el-input v-model="endConfig.output" type="textarea" :rows="3" />
          </el-form-item>
        </template>
      </el-form>
    </template>
  </div>
</template>

<style scoped>
.node-panel {
  height: 100%;
  padding: 12px;
  overflow-y: auto;
}

.node-panel__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.node-panel__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.node-panel__id {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.node-panel__id-code {
  padding: 1px 6px;
  border-radius: 4px;
  background: var(--el-fill-color-light);
  font-size: 12px;
  cursor: pointer;
}

.node-panel__vars {
  margin-bottom: 12px;
  padding: 8px;
  border-radius: 6px;
  background: var(--el-fill-color-lighter);
}

.node-panel__vars-label {
  margin-bottom: 6px;
}

.node-panel__vars-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.var-tag {
  cursor: pointer;
  font-family: var(--el-font-family-mono, monospace);
}

.muted {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.rows {
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: 100%;
}

.row,
.case {
  display: flex;
  gap: 6px;
  align-items: center;
}

.case__handle {
  min-width: 64px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
</style>
