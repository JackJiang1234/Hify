<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage, type UploadRequestOptions } from 'element-plus'
import { Loading, UploadFilled } from '@element-plus/icons-vue'

import { documentApi } from '@/api/knowledge'
import { MAX_UPLOAD_BYTES } from '../constants'

const props = defineProps<{ kbId: number }>()
const emit = defineEmits<{ uploaded: [] }>()

const uploading = ref(false)

// el-upload 上传前校验：仅 .txt、非空、不超限
function beforeUpload(file: File): boolean {
  if (!file.name.toLowerCase().endsWith('.txt')) {
    ElMessage.error('仅支持 .txt 文件')
    return false
  }
  if (file.size === 0) {
    ElMessage.error('文件为空')
    return false
  }
  if (file.size > MAX_UPLOAD_BYTES) {
    ElMessage.error('文件超过 5MB 上限')
    return false
  }
  return true
}

// 覆盖默认上传：走统一 api（multipart）。同步处理——请求返回即已分块+嵌入入库。
async function customUpload(options: UploadRequestOptions): Promise<void> {
  uploading.value = true
  try {
    await documentApi.upload(props.kbId, options.file)
    ElMessage.success('已上传并完成处理')
    emit('uploaded')
  } finally {
    uploading.value = false
  }
}
</script>

<template>
  <el-upload
    drag
    :show-file-list="false"
    accept=".txt"
    :before-upload="beforeUpload"
    :http-request="customUpload"
    :disabled="uploading"
  >
    <div v-if="uploading" class="up-busy">
      <el-icon class="is-loading"><Loading /></el-icon>
      <span>分块 + 嵌入中，请稍候…</span>
    </div>
    <template v-else>
      <el-icon class="up-icon"><UploadFilled /></el-icon>
      <div class="up-text">点击或拖拽 <strong>TXT</strong> 文件到此上传</div>
      <div class="up-hint">一期仅支持 .txt（UTF-8），单文件 ≤ 5MB；上传后即时分块、嵌入入库</div>
    </template>
  </el-upload>
</template>

<style scoped>
.up-icon {
  font-size: 38px;
  color: var(--el-text-color-placeholder);
  margin-bottom: 8px;
}

.up-text {
  color: var(--el-text-color-regular);
}

.up-hint {
  margin-top: 4px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.up-busy {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  color: var(--el-color-primary);
}
</style>
