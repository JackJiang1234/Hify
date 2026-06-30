import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

// 布局由 App.vue 承担（左侧菜单 + 右侧 router-view），路由为扁平结构
const routes: RouteRecordRaw[] = [
  { path: '/', redirect: '/providers' },
  {
    path: '/providers',
    name: 'providers',
    component: () => import('@/features/provider/views/ProviderListView.vue'),
    meta: { title: '模型管理' },
  },
  {
    path: '/agents',
    name: 'agents',
    component: () => import('@/features/agent/views/AgentListView.vue'),
    meta: { title: 'Agent 管理' },
  },
  {
    path: '/knowledge-bases',
    name: 'knowledge-bases',
    component: () => import('@/features/knowledge/views/KnowledgeBaseListView.vue'),
    meta: { title: '知识库' },
  },
  {
    path: '/knowledge-bases/:id',
    name: 'knowledge-base-detail',
    component: () => import('@/features/knowledge/views/KnowledgeBaseDetailView.vue'),
    meta: { title: '知识库' },
  },
  {
    path: '/conversations',
    name: 'conversations',
    component: () => import('@/features/conversation/views/ConversationView.vue'),
    meta: { title: '对话' },
  },
  {
    path: '/mcp-servers',
    name: 'mcp-servers',
    component: () => import('@/features/mcp/views/McpServerListView.vue'),
    meta: { title: 'MCP 工具' },
  },
  {
    path: '/mcp-servers/:id',
    name: 'mcp-server-detail',
    component: () => import('@/features/mcp/views/McpServerDetailView.vue'),
    meta: { title: 'MCP 工具' },
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

export default router
