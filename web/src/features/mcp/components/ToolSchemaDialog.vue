<script setup lang="ts">
import { computed } from 'vue'

import type { McpToolDto } from '@/api/mcp'

const visible = defineModel<boolean>('visible', { required: true })
const props = defineProps<{ tool: McpToolDto | null }>()

// 美化 JSON Schema；非法 JSON 原样展示。
const prettySchema = computed(() => {
  const raw = props.tool?.inputSchema ?? '{}'
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
})
</script>

<template>
  <el-dialog
    v-model="visible"
    :title="tool ? `入参 Schema · ${tool.name}` : '入参 Schema'"
    width="560px"
  >
    <p v-if="tool?.description" class="desc">{{ tool.description }}</p>
    <pre class="schema"><code>{{ prettySchema }}</code></pre>
  </el-dialog>
</template>

<style scoped>
.desc {
  margin: 0 0 12px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.schema {
  margin: 0;
  padding: 14px;
  max-height: 50vh;
  overflow: auto;
  background: var(--color-bg-subtle, #f1f5f9);
  border: 1px solid var(--el-border-color);
  border-radius: var(--radius-md, 8px);
  font-family: var(--font-mono, monospace);
  font-size: 12px;
  line-height: 1.6;
  color: var(--el-text-color-primary);
  white-space: pre;
}
</style>
