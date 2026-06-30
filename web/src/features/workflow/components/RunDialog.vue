<script setup lang="ts">
import { ref, watch } from 'vue'

import type { StartInput } from '../types'

const props = defineProps<{ visible: boolean; inputs: StartInput[]; running: boolean }>()
const emit = defineEmits<{
  (event: 'update:visible', value: boolean): void
  (event: 'run', values: Record<string, string>): void
}>()

const values = ref<Record<string, string>>({})

// 打开时按 start 声明的输入初始化空表单。
watch(
  () => props.visible,
  (open) => {
    if (open) {
      const next: Record<string, string> = {}
      for (const input of props.inputs) {
        next[input.name] = ''
      }
      values.value = next
    }
  },
)

function submit(): void {
  emit('run', { ...values.value })
}

function close(): void {
  emit('update:visible', false)
}
</script>

<template>
  <el-dialog :model-value="visible" title="试运行" width="480px" @update:model-value="close">
    <el-form v-if="inputs.length > 0" label-position="top">
      <el-form-item v-for="input in inputs" :key="input.name" :required="input.required">
        <template #label>
          {{ input.name }}<span v-if="input.required" class="req"> *</span>
        </template>
        <el-input v-model="values[input.name]" :placeholder="`输入 ${input.name}`" />
      </el-form-item>
    </el-form>
    <el-text v-else type="info" size="small">该工作流的 start 节点未声明输入，直接运行即可。</el-text>

    <template #footer>
      <el-button @click="close">取消</el-button>
      <el-button type="primary" :loading="running" @click="submit">运行</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.req {
  color: var(--el-color-danger);
}
</style>
