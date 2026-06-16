import { ref } from 'vue'
import { defineStore } from 'pinia'

/** 全局应用状态：主题、侧边栏折叠等跨页面共享的偏好 */
export const useAppStore = defineStore('app', () => {
  const sidebarCollapsed = ref(false)

  function toggleSidebar() {
    sidebarCollapsed.value = !sidebarCollapsed.value
  }

  return { sidebarCollapsed, toggleSidebar }
})
