<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'

import { mcpApi, type McpServerDto, type McpServerUpsert } from '@/api/mcp'
import { AUTH_TYPE_OPTIONS } from '../constants'

const visible = defineModel<boolean>('visible', { required: true })
const props = defineProps<{ server: McpServerDto | null }>()
const emit = defineEmits<{ saved: [] }>()

const isEdit = computed(() => props.server !== null)
const submitting = ref(false)
const formRef = ref<FormInstance>()

function emptyForm(): McpServerUpsert {
  return {
    name: '',
    endpoint: '',
    authType: 'none',
    authHeaderName: '',
    apiKey: '',
    timeoutMs: 0,
    enabled: true,
  }
}

const form = reactive<McpServerUpsert>(emptyForm())

function validHttpUrl(_rule: unknown, value: string, callback: (error?: Error) => void): void {
  if (!value) {
    callback(new Error('请输入端点 URL'))
    return
  }
  try {
    const url = new URL(value)
    if (url.protocol === 'http:' || url.protocol === 'https:') {
      callback()
    } else {
      callback(new Error('端点须为 http / https 地址'))
    }
  } catch {
    callback(new Error('端点 URL 格式不合法'))
  }
}

const rules: FormRules<McpServerUpsert> = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  endpoint: [{ validator: validHttpUrl, trigger: 'blur' }],
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
}

watch(visible, (open) => {
  if (!open) {
    return
  }
  formRef.value?.clearValidate()
  if (props.server) {
    const s = props.server
    Object.assign(form, {
      name: s.name,
      endpoint: s.endpoint,
      authType: s.authType,
      authHeaderName: s.authHeaderName,
      apiKey: '',
      timeoutMs: s.timeoutMs,
      enabled: s.enabled,
    })
  } else {
    Object.assign(form, emptyForm())
  }
})

async function submit(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) {
    return
  }
  submitting.value = true
  try {
    const body: McpServerUpsert = { ...form }
    if (props.server) {
      await mcpApi.update(props.server.id, body)
      ElMessage.success('已更新')
    } else {
      await mcpApi.create(body)
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
    :title="isEdit ? '编辑 MCP Server' : '新增 MCP Server'"
    width="560px"
    :close-on-click-modal="false"
  >
    <el-form ref="formRef" :model="form" :rules="rules" label-width="96px">
      <el-form-item label="名称" prop="name">
        <el-input v-model="form.name" maxlength="128" placeholder="如 订单系统 MCP" />
      </el-form-item>
      <el-form-item label="端点 URL" prop="endpoint">
        <el-input v-model="form.endpoint" placeholder="如 https://mcp.internal/orders" />
      </el-form-item>
      <el-form-item label="传输">
        <el-input model-value="Streamable HTTP" disabled />
        <div class="field-hint">一期仅支持 Streamable HTTP，固定不可改。</div>
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
      <el-form-item v-if="form.authType !== 'none'" label="凭证" prop="apiKey">
        <el-input
          v-model="form.apiKey"
          type="password"
          show-password
          :placeholder="isEdit ? '留空保留原凭证' : '明文凭证，加密后保存'"
        />
      </el-form-item>
      <el-form-item label="超时 (ms)">
        <el-input-number v-model="form.timeoutMs" :min="0" :step="1000" controls-position="right" />
        <div class="field-hint">0 = 用全局默认（60s）；按 Server 覆盖时填 &gt; 0。</div>
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
.field-hint {
  margin-top: 4px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  line-height: 1.5;
}
</style>
