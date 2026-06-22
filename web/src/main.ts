import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'

// 命令式调用的组件（ElMessage / ElMessageBox）样式不会被按需解析器自动引入，需手动引入，
// 否则确认框/提示会缺样式塌到左上角。置于本地样式之前，保证 element-overrides 仍可覆盖。
import 'element-plus/theme-chalk/el-overlay.css'
import 'element-plus/theme-chalk/el-message-box.css'
import 'element-plus/theme-chalk/el-message.css'

import './styles/index.css'

const app = createApp(App)

app.use(createPinia())
app.use(router)

app.mount('#app')
