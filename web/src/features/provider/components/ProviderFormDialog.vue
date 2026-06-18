<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'

import { providerApi, type ProviderDto, type ProviderUpsert } from '@/api/provider'
import { AUTH_TYPE_OPTIONS, PROVIDER_TYPE_OPTIONS } from '../constants'

const visible = defineModel<boolean>('visible', { required: true })
const props = defineProps<{ provider: ProviderDto | null }>()
const emit = defineEmits<{ saved: [] }>()

const isEdit = computed(() => props.provider !== null)
const submitting = ref(false)
const formRef = ref<FormInstance>()

const form = reactive<ProviderUpsert>({
  name: '',
  providerType: 'openai',
  baseUrl: '',
  authType: 'bearer',
  authHeaderName: '',
  apiKey: '',
  settings: '{}',
  enabled: true,
})

function validJsonObject(_rule: unknown, value: string, callback: (error?: Error) => void): void {
  if (!value || value.trim() === '') {
    callback()
    return
  }
  try {
    const parsed = JSON.parse(value)
    if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
      callback(new Error('settings 须为 JSON 对象'))
      return
    }
    callback()
  } catch {
    callback(new Error('settings 不是合法 JSON'))
  }
}

const rules: FormRules<ProviderUpsert> = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  providerType: [{ required: true, message: '请选择类型', trigger: 'change' }],
  baseUrl: [{ required: true, message: '请输入 API 基址', trigger: 'blur' }],
  authType: [{ required: true, message: '请选择鉴权方式', trigger: 'change' }],
  authHeaderName: [
    {
      validator: (_rule, value: string, callback) => {
        if (form.authType === 'header' && !value) {
          callback(new Error('请输入请求头名'))
          return
        }
        callback()
      },
      trigger: 'blur',
    },
  ],
  settings: [{ validator: validJsonObject, trigger: 'blur' }],
}

// 打开时按编辑/新增初始化表单
watch(visible, (open) => {
  if (!open) {
    return
  }
  formRef.value?.clearValidate()
  if (props.provider) {
    const p = props.provider
    Object.assign(form, {
      name: p.name,
      providerType: p.providerType,
      baseUrl: p.baseUrl,
      authType: p.authType,
      authHeaderName: p.authHeaderName,
      apiKey: '',
      settings: p.settings || '{}',
      enabled: p.enabled,
    })
  } else {
    Object.assign(form, {
      name: '',
      providerType: 'openai',
      baseUrl: '',
      authType: 'bearer',
      authHeaderName: '',
      apiKey: '',
      settings: '{}',
      enabled: true,
    })
  }
})

async function submit(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) {
    return
  }
  submitting.value = true
  try {
    const body: ProviderUpsert = { ...form }
    if (props.provider) {
      await providerApi.update(props.provider.id, body)
      ElMessage.success('已更新')
    } else {
      await providerApi.create(body)
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
    :title="isEdit ? '编辑供应商' : '新增供应商'"
    width="560px"
    :close-on-click-modal="false"
  >
    <el-form ref="formRef" :model="form" :rules="rules" label-width="96px">
      <el-form-item label="名称" prop="name">
        <el-input v-model="form.name" maxlength="128" placeholder="如 我的 OpenAI" />
      </el-form-item>
      <el-form-item label="类型" prop="providerType">
        <el-select v-model="form.providerType" style="width: 100%">
          <el-option
            v-for="opt in PROVIDER_TYPE_OPTIONS"
            :key="opt.value"
            :label="opt.label"
            :value="opt.value"
          />
        </el-select>
      </el-form-item>
      <el-form-item label="API 基址" prop="baseUrl">
        <el-input v-model="form.baseUrl" placeholder="如 https://api.openai.com/v1" />
      </el-form-item>
      <el-form-item label="鉴权方式" prop="authType">
        <el-select v-model="form.authType" style="width: 100%">
          <el-option
            v-for="opt in AUTH_TYPE_OPTIONS"
            :key="opt.value"
            :label="opt.label"
            :value="opt.value"
          />
        </el-select>
      </el-form-item>
      <el-form-item v-if="form.authType === 'header'" label="请求头名" prop="authHeaderName">
        <el-input v-model="form.authHeaderName" placeholder="如 x-api-key" />
      </el-form-item>
      <el-form-item v-if="form.authType !== 'none'" label="密钥" prop="apiKey">
        <el-input
          v-model="form.apiKey"
          type="password"
          show-password
          :placeholder="isEdit ? '留空保留原密钥' : '明文密钥，加密后保存'"
        />
      </el-form-item>
      <el-form-item label="附加配置" prop="settings">
        <el-input
          v-model="form.settings"
          type="textarea"
          :rows="2"
          placeholder='JSON 头映射，如 {"anthropic-version":"2023-06-01"}'
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
