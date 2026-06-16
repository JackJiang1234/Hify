import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

import ConsoleLayout from '@/components/layout/ConsoleLayout.vue'

// 各 feature 的路由集中在此聚合；feature 增多后可拆为 routes/*.ts 再合并
const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: ConsoleLayout,
    redirect: '/providers',
    children: [
      {
        path: 'providers',
        name: 'providers',
        component: () => import('@/features/provider/views/ProviderListView.vue'),
        meta: { title: '模型管理' },
      },
      // agent / conversation / knowledge / workflow / mcp 路由后续在此追加
    ],
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

export default router
