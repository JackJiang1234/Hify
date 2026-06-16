<script setup lang="ts">
import { useRoute } from 'vue-router'

const route = useRoute()
</script>

<template>
  <el-container class="console">
    <el-aside width="220px" class="console__aside">
      <div class="console__brand">Hify</div>
      <el-menu :default-active="route.path" router>
        <el-menu-item index="/providers">模型管理</el-menu-item>
        <!-- Agent 配置、对话、知识库、工作流、MCP 菜单后续追加 -->
      </el-menu>
    </el-aside>
    <el-container>
      <el-header class="console__header">
        {{ route.meta.title ?? '' }}
      </el-header>
      <el-main>
        <RouterView />
      </el-main>
    </el-container>
  </el-container>
</template>

<style scoped>
.console {
  height: 100vh;
}
.console__aside {
  border-right: 1px solid var(--el-border-color);
}
.console__brand {
  height: 56px;
  display: flex;
  align-items: center;
  padding-left: 20px;
  font-size: 18px;
  font-weight: 600;
}
.console__header {
  display: flex;
  align-items: center;
  border-bottom: 1px solid var(--el-border-color);
  font-weight: 500;
}
</style>
