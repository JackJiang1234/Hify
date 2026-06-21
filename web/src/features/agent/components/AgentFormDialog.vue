<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'

import { agentApi, type AgentDto, type AgentUpsert } from '@/api/agent'
import type { ChatModelOption } from '../composables/useChatModels'
import { MAX_BINDINGS, MAX_SYSTEM_PROMPT, PARAM_RANGE, defaultAgentForm } from '../constants'

const visible = defineModel<boolean>('visible', { required: true })
const props = defineProps<{ agent: AgentDto | null; models: ChatModelOption[] }>()
const emit = defineEmits<{ saved: [] }>()

const isEdit = computed(() => props.agent !== null)
const submitting = ref(false)
const formRef = ref<FormInstance>()

const form = reactive<AgentUpsert>(defaultAgentForm())
// 绑定 ID 以标签输入，提交时校验并转为 number[]（MCP/知识库列表接口上线后可替换为选择器）
const toolIdsInput = ref<string[]>([])
const knowledgeIdsInput = ref<string[]>([])

// 按供应商分组的模型下拉
const modelGroups = computed(() => {
  const grouped = new Map<string, ChatModelOption[]>()
  for (const model of props.models) {
    const items = grouped.get(model.providerName) ?? []
    items.push(model)
    grouped.set(model.providerName, items)
  }
  return [...grouped.entries()].map(([providerName, items]) => ({ providerName, items }))
})

const selectedModel = computed(() => props.models.find((model) => model.id === form.modelId) ?? null)
const maxTokensCap = computed(() => selectedModel.value?.maxOutputTokens || undefined)
// 选了不支持工具的模型却绑了工具：前端先行提示，最终以后端校验为准
const toolUnsupported = computed(
  () => selectedModel.value !== null && !selectedModel.value.supportsTools && toolIdsInput.value.length > 0,
)

const rules: FormRules<AgentUpsert> = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  modelId: [
    {
      validator: (_rule, value: number, callback) =>
        callback(value > 0 ? undefined : new Error('请选择模型')),
      trigger: 'change',
    },
  ],
}

// 解析标签为正整数数组；含非法项返回 null
function parseIdList(input: string[]): number[] | null {
  const ids: number[] = []
  for (const raw of input) {
    const value = Number(raw)
    if (!Number.isInteger(value) || value <= 0) {
      return null
    }
    ids.push(value)
  }
  return ids
}

watch(visible, (open) => {
  if (!open) {
    return
  }
  formRef.value?.clearValidate()
  const initial = props.agent
  if (initial) {
    Object.assign(form, {
      name: initial.name,
      description: initial.description,
      modelId: initial.modelId,
      systemPrompt: initial.systemPrompt,
      modelParams: {
        temperature: initial.modelParams.temperature ?? null,
        topP: initial.modelParams.topP ?? null,
        maxTokens: initial.modelParams.maxTokens ?? null,
      },
      retrievalParams: { ...initial.retrievalParams },
      maxIterations: initial.maxIterations,
      toolIds: [...initial.toolIds],
      knowledgeBaseIds: [...initial.knowledgeBaseIds],
      enabled: initial.enabled,
    })
    toolIdsInput.value = initial.toolIds.map(String)
    knowledgeIdsInput.value = initial.knowledgeBaseIds.map(String)
  } else {
    Object.assign(form, defaultAgentForm())
    toolIdsInput.value = []
    knowledgeIdsInput.value = []
  }
})

async function submit(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) {
    return
  }

  const toolIds = parseIdList(toolIdsInput.value)
  const knowledgeBaseIds = parseIdList(knowledgeIdsInput.value)
  if (toolIds === null || knowledgeBaseIds === null) {
    ElMessage.error('工具 / 知识库 ID 必须为正整数')
    return
  }

  submitting.value = true
  try {
    const body: AgentUpsert = { ...form, toolIds, knowledgeBaseIds }
    if (props.agent) {
      await agentApi.update(props.agent.id, body)
      ElMessage.success('已更新')
    } else {
      await agentApi.create(body)
      ElMessage.success('已创建')
    }
    visible.value = false
    emit('saved')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <el-dialog
    v-model="visible"
    :title="isEdit ? '编辑 Agent' : '新增 Agent'"
    width="640px"
    :close-on-click-modal="false"
  >
    <el-form ref="formRef" :model="form" :rules="rules" label-width="96px">
      <el-form-item label="名称" prop="name">
        <el-input v-model="form.name" maxlength="128" placeholder="如 客服助手" />
      </el-form-item>
      <el-form-item label="描述">
        <el-input v-model="form.description" maxlength="512" placeholder="一句话说明用途（可选）" />
      </el-form-item>
      <el-form-item label="模型" prop="modelId">
        <el-select v-model="form.modelId" filterable placeholder="选择对话模型" style="width: 100%">
          <el-option-group
            v-for="group in modelGroups"
            :key="group.providerName"
            :label="group.providerName"
          >
            <el-option
              v-for="model in group.items"
              :key="model.id"
              :label="model.name"
              :value="model.id"
            />
          </el-option-group>
        </el-select>
      </el-form-item>
      <el-form-item label="系统提示词">
        <el-input
          v-model="form.systemPrompt"
          type="textarea"
          :rows="4"
          :maxlength="MAX_SYSTEM_PROMPT"
          show-word-limit
          placeholder="设定角色、任务与约束"
        />
      </el-form-item>

      <el-collapse>
        <el-collapse-item title="生成参数 / 检索参数（留空用模型默认）" name="advanced">
          <el-form-item label="温度">
            <el-input-number
              v-model="form.modelParams.temperature"
              :min="PARAM_RANGE.temperature.min"
              :max="PARAM_RANGE.temperature.max"
              :step="PARAM_RANGE.temperature.step"
              :value-on-clear="null"
              controls-position="right"
            />
            <span class="hint">0–2，越高越发散</span>
          </el-form-item>
          <el-form-item label="Top P">
            <el-input-number
              v-model="form.modelParams.topP"
              :min="PARAM_RANGE.topP.min"
              :max="PARAM_RANGE.topP.max"
              :step="PARAM_RANGE.topP.step"
              :value-on-clear="null"
              controls-position="right"
            />
            <span class="hint">0–1，核采样</span>
          </el-form-item>
          <el-form-item label="最大 Token">
            <el-input-number
              v-model="form.modelParams.maxTokens"
              :min="1"
              :max="maxTokensCap"
              :value-on-clear="null"
              controls-position="right"
            />
            <span v-if="maxTokensCap" class="hint">模型上限 {{ maxTokensCap }}</span>
          </el-form-item>
          <el-form-item label="迭代上限">
            <el-input-number
              v-model="form.maxIterations"
              :min="PARAM_RANGE.maxIterations.min"
              :max="PARAM_RANGE.maxIterations.max"
              controls-position="right"
            />
            <span class="hint">工具调用循环次数上限</span>
          </el-form-item>
          <el-form-item label="检索 TopK">
            <el-input-number
              v-model="form.retrievalParams.topK"
              :min="PARAM_RANGE.topK.min"
              :max="PARAM_RANGE.topK.max"
              controls-position="right"
            />
            <span class="hint">RAG 返回分块数</span>
          </el-form-item>
          <el-form-item label="相似度阈值">
            <el-input-number
              v-model="form.retrievalParams.scoreThreshold"
              :min="PARAM_RANGE.scoreThreshold.min"
              :max="PARAM_RANGE.scoreThreshold.max"
              :step="PARAM_RANGE.scoreThreshold.step"
              controls-position="right"
            />
            <span class="hint">0–1，低于此分丢弃</span>
          </el-form-item>
        </el-collapse-item>
      </el-collapse>

      <el-form-item label="工具" class="bindings">
        <el-select
          v-model="toolIdsInput"
          multiple
          filterable
          allow-create
          default-first-option
          :reserve-keyword="false"
          :multiple-limit="MAX_BINDINGS"
          placeholder="输入 MCP 工具 ID 回车添加"
          style="width: 100%"
        />
        <span v-if="toolUnsupported" class="hint hint--warn">所选模型不支持工具调用，保存会被拒绝</span>
      </el-form-item>
      <el-form-item label="知识库" class="bindings">
        <el-select
          v-model="knowledgeIdsInput"
          multiple
          filterable
          allow-create
          default-first-option
          :reserve-keyword="false"
          :multiple-limit="MAX_BINDINGS"
          placeholder="输入知识库 ID 回车添加"
          style="width: 100%"
        />
      </el-form-item>
      <el-form-item label="启用">
        <el-switch v-model="form.enabled" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" :loading="submitting" @click="submit">保存</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.hint {
  margin-left: 12px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.hint--warn {
  color: var(--el-color-warning);
}

.bindings :deep(.el-form-item__content) {
  flex-direction: column;
  align-items: flex-start;
}
</style>
