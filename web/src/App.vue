<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import {
  ChatDotRound,
  Collection,
  Connection,
  Expand,
  Fold,
  Setting,
  User,
} from '@element-plus/icons-vue'

const route = useRoute()

// 页头标题取自路由 meta.title，缺省回退到品牌名
const pageTitle = computed(() => (route.meta.title as string | undefined) ?? 'Hify')

// 菜单高亮取顶级区段：详情页（如 /knowledge-bases/:id）仍高亮其列表入口
const activeMenu = computed(() => '/' + (route.path.split('/')[1] ?? ''))

// 侧边栏折叠状态
const collapsed = ref(false)
</script>

<template>
  <el-container class="app">
    <el-aside :width="collapsed ? '64px' : '240px'" class="app__aside">
      <!-- Logo 区：渐变品牌字 + 副标题；折叠时只留渐变 H -->
      <div class="app__brand">
        <template v-if="!collapsed">
          <div class="app__brand-name">Hify</div>
          <div class="app__brand-subtitle">AI Agent Platform</div>
        </template>
        <div v-else class="app__brand-mark">H</div>
      </div>

      <el-menu :default-active="activeMenu" :collapse="collapsed" router class="app__menu">
        <el-menu-item index="/providers">
          <el-icon><Setting /></el-icon>
          <span>模型管理</span>
        </el-menu-item>
        <el-menu-item index="/agents">
          <el-icon><User /></el-icon>
          <span>Agent 管理</span>
        </el-menu-item>
        <el-menu-item index="/knowledge-bases">
          <el-icon><Collection /></el-icon>
          <span>知识库</span>
        </el-menu-item>
        <el-menu-item index="/conversations">
          <el-icon><ChatDotRound /></el-icon>
          <span>对话</span>
        </el-menu-item>
        <el-menu-item index="/mcp-servers">
          <el-icon><Connection /></el-icon>
          <span>MCP 工具</span>
        </el-menu-item>
      </el-menu>

      <!-- 底部：折叠/展开按钮 + 版本号 -->
      <div class="app__aside-footer" :class="{ 'app__aside-footer--collapsed': collapsed }">
        <button
          type="button"
          class="app__collapse-btn"
          :title="collapsed ? '展开' : '收起'"
          @click="collapsed = !collapsed"
        >
          <el-icon><component :is="collapsed ? Expand : Fold" /></el-icon>
          <span v-show="!collapsed">收起</span>
        </button>
        <span v-show="!collapsed" class="app__version">v0.1</span>
      </div>
    </el-aside>

    <el-container>
      <el-header class="app__header">
        <h1 class="app__title">{{ pageTitle }}</h1>
      </el-header>
      <el-main class="app__main">
        <RouterView />
      </el-main>
    </el-container>
  </el-container>
</template>

<style scoped>
.app {
  height: 100vh;
}

/* ---- 侧边栏：深色基底（--color-bg-dark），宽度折叠平滑过渡 ---- */
.app__aside {
  display: flex;
  flex-direction: column;
  background: var(--color-bg-dark);
  border-right: 1px solid var(--color-sidebar-border);
  transition: width var(--duration-base) var(--ease-standard);
  overflow: hidden;
}

/* ---- Logo 区 ---- */
.app__brand {
  display: flex;
  flex-direction: column;
  justify-content: center;
  min-height: var(--layout-header-height);
  padding: var(--space-3) var(--space-5);
  border-bottom: 1px solid var(--color-sidebar-border);
}

/* "Hify" 主色 → 辅色渐变文字 */
.app__brand-name,
.app__brand-mark {
  width: fit-content;
  background: linear-gradient(105deg, var(--violet-400) 0%, var(--cyan-400) 100%);
  background-clip: text;
  -webkit-background-clip: text;
  color: transparent;
  -webkit-text-fill-color: transparent;
  font-weight: var(--font-weight-semibold);
  letter-spacing: 0.5px;
  line-height: 1.2;
}

.app__brand-name {
  font-size: var(--font-size-2xl);
}

.app__brand-mark {
  margin: 0 auto;
  font-size: var(--font-size-2xl);
}

.app__brand-subtitle {
  margin-top: 2px;
  color: var(--color-sidebar-text-muted);
  font-size: var(--font-size-xs);
  letter-spacing: 1px;
}

/* ---- 菜单 ---- */
.app__menu {
  flex: 1;
  padding: var(--space-3);
  border-right: none;
  /* 默认白色文字 + 透明背景；hover 背景微亮（白色 10%） */
  --el-menu-bg-color: transparent;
  --el-menu-text-color: rgb(255 255 255 / 85%);
  --el-menu-hover-bg-color: rgb(255 255 255 / 10%);
  --el-menu-hover-text-color: #fff;
  --el-menu-active-color: #fff;
}

/* 折叠态下 padding 收窄，避免图标偏移 */
.app__menu.el-menu--collapse {
  padding: var(--space-3) var(--space-2);
}

.app__menu :deep(.el-menu-item) {
  position: relative;
  height: 44px;
  margin-bottom: var(--space-1);
  border-radius: var(--radius-md);
  color: rgb(255 255 255 / 85%);
  transition: var(--transition-colors);
}

.app__menu :deep(.el-menu-item .el-icon) {
  color: inherit;
}

/* 选中态：背景微亮 + 左侧 3px 主色竖线 */
.app__menu :deep(.el-menu-item.is-active) {
  background: rgb(255 255 255 / 10%);
  color: #fff;
  font-weight: var(--font-weight-medium);
}

.app__menu :deep(.el-menu-item.is-active)::before {
  content: '';
  position: absolute;
  left: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 3px;
  height: 60%;
  border-radius: 0 var(--radius-xs) var(--radius-xs) 0;
  background: var(--color-primary);
}

/* ---- 底部：折叠按钮 + 版本号 ---- */
.app__aside-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-4);
  border-top: 1px solid var(--color-sidebar-border);
}

.app__aside-footer--collapsed {
  justify-content: center;
  padding: var(--space-3) var(--space-2);
}

.app__collapse-btn {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-2) var(--space-3);
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-sidebar-text);
  font-size: var(--font-size-sm);
  cursor: pointer;
  transition: var(--transition-colors);
}

.app__collapse-btn:hover {
  background: rgb(255 255 255 / 10%);
  color: #fff;
}

.app__version {
  color: var(--color-sidebar-text-muted);
  font-size: var(--font-size-xs);
}

/* ---- 页头 ---- */
.app__header {
  display: flex;
  align-items: center;
  height: var(--layout-header-height);
  padding: 0 var(--space-6);
  background: var(--color-bg-surface);
  border-bottom: 1px solid var(--color-border);
  box-shadow: var(--shadow-xs);
}

.app__title {
  margin: 0;
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-semibold);
  color: var(--color-text-primary);
}

/* ---- 内容画布 ---- */
.app__main {
  padding: var(--space-6);
  background: var(--color-bg-canvas);
}
</style>
