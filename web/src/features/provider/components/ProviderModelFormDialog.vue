<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'

import { modelApi, type ModelDto, type ModelUpsert } from '@/api/provider'
import { MODEL_TYPE_OPTIONS, REQUIRED_EMBEDDING_DIMENSIONS } from '../constants'

const visible = defineModel<boolean>('visible', { required: true })
const props = defineProps<{ providerId: number; model: ModelDto | null }>()
const emit = defineEmits<{ saved: [] }>()

const isEdit = computed(() => props.model !== null)
const submitting = ref(false)
const formRef = ref<FormInstance>()

const form = reactive<ModelUpsert>({
  name: '',
  displayName: '',
  modelType: 'chat',
  contextWindow: 0,
  maxOutputTokens: 0,
  embeddingDimensions: 0,
  supportsStreaming: false,
  supportsTools: false,
  supportsVision: false,
  sortOrder: 0,
  enabled: true,
})

const isEmbedding = computed(() => form.modelType === 'embedding')

const rules: FormRules<ModelUpsert> = {
  name: [{ required: true, message: '请输入模型标识', trigger: 'blur' }],
  modelType: [{ required: true, message: '请选择模型类型', trigger: 'change' }],
  embeddingDimensions: [
    {
      validator: (_rule, value: number, callback) => {
        if (form.modelType === 'embedding' && value !== REQUIRED_EMBEDDING_DIMENSIONS) {
          callback(new Error(`嵌入模型维度须为 ${REQUIRED_EMBEDDING_DIMENSIONS}`))
          return
        }
        callback()
      },
      trigger: 'blur',
    },
  ],
}

watch(visible, (open) => {
  if (!open) {
    return
  }
  formRef.value?.clearValidate()
  if (props.model) {
    Object.assign(form, { ...props.model } as ModelUpsert)
  } else {
    Object.assign(form, {
      name: '',
      displayName: '',
      modelType: 'chat',
      contextWindow: 0,
      maxOutputTokens: 0,
      embeddingDimensions: 0,
      supportsStreaming: false,
      supportsTools: false,
      supportsVision: false,
      sortOrder: 0,
      enabled: true,
    })
  }
})

// 切到嵌入类型时，预填固定维度，省去用户记忆
watch(isEmbedding, (embedding) => {
  if (embedding && form.embeddingDimensions === 0) {
    form.embeddingDimensions = REQUIRED_EMBEDDING_DIMENSIONS
  }
})

async function submit(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) {
    return
  }
  submitting.value = true
  try {
    const body: ModelUpsert = { ...form }
    if (props.model) {
      await modelApi.update(props.model.id, body)
      ElMessage.success('已更新')
    } else {
      await modelApi.create(props.providerId, body)
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
    :title="isEdit ? '编辑模型' : '新增模型'"
    width="520px"
    append-to-body
    :close-on-click-modal="false"
  >
    <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
      <el-form-item label="模型标识" prop="name">
        <el-input v-model="form.name" placeholder="如 gpt-4o / claude-opus-4-8" />
      </el-form-item>
      <el-form-item label="展示名称">
        <el-input v-model="form.displayName" />
      </el-form-item>
      <el-form-item label="类型" prop="modelType">
        <el-select v-model="form.modelType" style="width: 100%">
          <el-option
            v-for="opt in MODEL_TYPE_OPTIONS"
            :key="opt.value"
            :label="opt.label"
            :value="opt.value"
          />
        </el-select>
      </el-form-item>
      <template v-if="!isEmbedding">
        <el-form-item label="上下文窗口">
          <el-input-number v-model="form.contextWindow" :min="0" :step="1000" />
        </el-form-item>
        <el-form-item label="最大输出">
          <el-input-number v-model="form.maxOutputTokens" :min="0" :step="256" />
        </el-form-item>
        <el-form-item label="能力">
          <el-checkbox v-model="form.supportsStreaming">流式</el-checkbox>
          <el-checkbox v-model="form.supportsTools">工具</el-checkbox>
          <el-checkbox v-model="form.supportsVision">视觉</el-checkbox>
        </el-form-item>
      </template>
      <el-form-item v-else label="嵌入维度" prop="embeddingDimensions">
        <el-input-number v-model="form.embeddingDimensions" :min="0" />
        <span class="hint">须为 {{ REQUIRED_EMBEDDING_DIMENSIONS }}（与 pgvector 一致）</span>
      </el-form-item>
      <el-form-item label="排序">
        <el-input-number v-model="form.sortOrder" :min="0" />
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
  margin-left: 8px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
</style>
