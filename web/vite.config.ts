import { fileURLToPath, URL } from 'node:url'

import vue from '@vitejs/plugin-vue'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    // 按需自动导入 Element Plus 组件与 API，避免全量打包
    AutoImport({ resolvers: [ElementPlusResolver()] }),
    Components({ resolvers: [ElementPlusResolver()] }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // 开发期反代后端，避免跨域；生产由 Nginx 承担
      '/api': {
        target: 'http://localhost:5155',
        changeOrigin: true,
      },
      // 健康检查端点（联通测试用）
      '/health': {
        target: 'http://localhost:5155',
        changeOrigin: true,
      },
    },
  },
})
