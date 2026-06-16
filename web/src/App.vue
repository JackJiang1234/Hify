<script setup lang="ts">
import { useRoute } from 'vue-router'

const route = useRoute()
</script>

<template>
  <el-container class="app">
    <el-aside width="220px" class="app__aside">
      <div class="app__brand">Hify</div>
      <el-menu :default-active="route.path" router>
        <el-menu-item index="/providers">模型管理</el-menu-item>
        <el-menu-item index="/agents">Agent 管理</el-menu-item>
        <el-menu-item index="/conversations">对话</el-menu-item>
      </el-menu>
    </el-aside>
    <el-main class="app__main">
      <RouterView />
    </el-main>
  </el-container>
</template>

<style scoped>
.app {
  height: 100vh;
}
.app__aside {
  border-right: 1px solid var(--el-border-color);
}
.app__brand {
  height: 56px;
  display: flex;
  align-items: center;
  padding-left: 20px;
  font-size: 18px;
  font-weight: 600;
}
</style>
