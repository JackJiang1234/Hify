import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vitest/config'

// 仅测逻辑层（composable / api），无需 DOM，environment 用 node。
export default defineConfig({
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    environment: 'node',
    include: ['src/**/*.spec.ts'],
  },
})
