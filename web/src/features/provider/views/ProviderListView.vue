<script setup lang="ts">
import { onMounted, ref } from 'vue'

import { getHealth } from '@/api/health'

// null=检测中，true=已连接，false=未连接
const connected = ref<boolean | null>(null)

onMounted(async () => {
  try {
    await getHealth()
    connected.value = true
  } catch {
    connected.value = false
  }
})
</script>

<template>
  <div>模型提供商管理</div>
  <p v-if="connected === true" style="color: #67c23a">后端已连接：Hify is running</p>
  <p v-else-if="connected === false" style="color: #f56c6c">后端未连接</p>
</template>
