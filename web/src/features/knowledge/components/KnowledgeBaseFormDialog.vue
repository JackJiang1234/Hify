<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'

import { knowledgeApi, type KnowledgeBaseDto, type KnowledgeBaseUpsert } from '@/api/knowledge'
import type { EmbeddingModelOption } from '../composables/useEmbeddingModels'
import { CHUNK_SIZE_RANGE, defaultKnowledgeBaseForm } from '../constants'

const visible = defineModel<boolean>('visible', { required: true })
const props = defineProps<{ kb: KnowledgeBaseDto | null; models: EmbeddingModelOption[] }>()
const emit = defineEmits<{ saved: [] }>()

const isEdit = computed(() => props.kb !== null)
// 已有分块（documentCount>0）则冻结嵌入模型与分块参数（对应后端 7004）
const frozen = computed(() => props.kb !== null && props.kb.documentCount > 0)
const submitting = ref(false)
const formRef = ref<FormInstance>()

const form = reactive<KnowledgeBaseUpsert>(defaultKnowledgeBaseForm())

// 按供应商分组的嵌入模型下拉
const modelGroups = computed(() => {
  const grouped = new Map<string, EmbeddingModelOption[]>()
  for (const model of props.models) {
    const items = grouped.get(model.providerName) ?? []
    items.push(model)
    grouped.set(model.providerName, items)
  }
  return [...grouped.entries()].map(([providerName, items]) => ({ providerName, items }))
})

const rules: FormRules<KnowledgeBaseUpsert> = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  embeddingModelId: [
    {
      validator: (_rule, value: number, callback) =>
        callback(value > 0 ? undefined : new Error('请选择嵌入模型')),
      trigger: 'change',
    },
  ],
  chunkOverlap: [
    {
      validator: (_rule, value: number, callback) =>
        callback(value < form.chunkSize ? undefined : new Error('重叠长度须小于分块长度')),
      trigger: 'change',
    },
  ],
}

watch(visible, (open) => {
  if (!open) {
    return
  }
  formRef.value?.clearValidate()
  const initial = props.kb
  if (initial) {
    Object.assign(form, {
      name: initial.name,
      description: initial.description,
      embeddingModelId: initial.embeddingModelId,
      chunkSize: initial.chunkSize,
      chunkOverlap: initial.chunkOverlap,
    })
  } else {
    Object.assign(form, defaultKnowledgeBaseForm())
  }
})

async function submit(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) {
    return
  }

  submitting.value = true
  try {
    if (props.kb) {
      await knowledgeApi.update(props.kb.id, { ...form })
      ElMessage.success('已更新')
    } else {
      await knowledgeApi.create({ ...form })
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
    :title="isEdit ? '编辑知识库' : '新建知识库'"
    width="560px"
    :close-on-click-modal="false"
  >
    <el-alert
      v-if="frozen"
      type="warning"
      :closable="false"
      show-icon
      title="该知识库已有分块"
      description="嵌入模型与分块参数不可更改（更改会使存量向量与新向量语义空间不一致）。如需调整请新建知识库。"
      style="margin-bottom: 16px"
    />

    <el-form ref="formRef" :model="form" :rules="rules" label-width="92px">
      <el-form-item label="名称" prop="name">
        <el-input v-model="form.name" maxlength="128" placeholder="如 售后政策库" />
      </el-form-item>
      <el-form-item label="描述">
        <el-input v-model="form.description" maxlength="512" placeholder="一句话说明用途（可选）" />
      </el-form-item>
      <el-form-item label="嵌入模型" prop="embeddingModelId">
        <el-select
          v-model="form.embeddingModelId"
          :disabled="frozen"
          filterable
          placeholder="选择 1536 维嵌入模型"
          style="width: 100%"
        >
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
        <span class="hint">仅支持 1536 维嵌入模型（与向量库固定维度一致），列表已自动过滤。</span>
      </el-form-item>
      <el-form-item label="分块长度">
        <el-input-number
          v-model="form.chunkSize"
          :disabled="frozen"
          :min="CHUNK_SIZE_RANGE.min"
          :max="CHUNK_SIZE_RANGE.max"
          controls-position="right"
        />
        <span class="hint">{{ CHUNK_SIZE_RANGE.min }}–{{ CHUNK_SIZE_RANGE.max }} 字符</span>
      </el-form-item>
      <el-form-item label="重叠长度" prop="chunkOverlap">
        <el-input-number
          v-model="form.chunkOverlap"
          :disabled="frozen"
          :min="0"
          :max="CHUNK_SIZE_RANGE.max"
          controls-position="right"
        />
        <span class="hint">须小于分块长度</span>
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
</style>
