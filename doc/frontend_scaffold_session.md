# 前端脚手架搭建会话记录

> 日期：2026-06-16
> 主题：规划并落地 Hify 前端（Vue 3 + TS + Vite + Element Plus），配套脚本与文档同步。
> 形式：按用户请求顺序记录「需求 → 处理 / 决策 → 结果」。

---

## 1. 规划前端目录结构（review）

**需求**：项目原结构未含前端，采用前后端分离（Vue 3 + TS + Vite + Element Plus），先出目录规划供 review。

**决策与结果**：
- 前端独立项目放在与 `src/`、`tests/` 平级的 `web/` 目录（不挂进 .NET solution）。
- 采用特性切片，`features/` 下与后端 6 模块一一对应：provider、agent、conversation、knowledge、workflow、mcp。
- 关键约定与后端契约对齐：`api/client.ts` 拦截器拆 `Result<T>`/`PageResult<T>`、错误码四位分段、SSE 用原生 fetch + 120s、axios 同步 60s。
- 待用户拍板项：目录名、是否 monorepo、包管理器。

## 2. 确认三项决策

**需求/回答**：① 目录名 `web/`；② `web` 独立 npm 项目；③ 包管理器 pnpm。

**结果**：落地脚手架，核心配置写实际内容，业务特性目录用 `.gitkeep` 占位。生成内容：
- 配置：`package.json`、`vite.config.ts`（含 `@` 别名、Element Plus 按需自动导入、`/api` dev proxy）、`tsconfig*.json`、`env.d.ts`、`.env*`、`eslint.config.ts`、`.prettierrc.json`、`.gitignore`、`Dockerfile`、`nginx.conf`、`index.html`。
- 源码：`main.ts`、`App.vue`、`router/index.ts`、`stores/app.ts`、`styles/index.css`。
- API 层：`api/client.ts`（拦截器 + `ApiError` + `api.get/post/put/delete/getPage`）、`api/types.ts`、`constants/error-code.ts`（与后端 `ErrorCode` 枚举对齐）、各模块 `api/*.ts`、`composables/useSse.ts`。
- 示例特性：provider 的 view→composable→api 完整接线（后续被简化）。
- dev proxy target 按 `launchSettings.json` 设为 `http://localhost:5155`。

## 3. 更新 CLAUDE.md（补前端规划）

**需求**：更新 CLAUDE.md，补前端规划。

**结果**：
- 技术栈行补全为 `Vue 3 + TypeScript + Vite + Element Plus + Pinia + vue-router`、pnpm、前后端分离。
- 新增「前端代码组织：特性切片」「前端约定（强制）」两节，与脚手架一致。

## 4. 精简 CLAUDE.md

**需求**：在不影响 AI 阅读的前提下精简。

**结果**：329 行 → 258 行（约 -22%），规范条目一条未删。
- 合并重复的「行为指令」+「开发规范」为「开发规范与行为指令」。
- 去跨章节重复（分页/空值/超时表述收敛到权威处）。
- 压缩说明性长句，修掉编辑残留的断头标题。
- 保留所有「禁/必须/强制」条目、目录图、错误码分段、DB 字段约定、C# 编码细则与标题结构。

## 5. 启动前端 & 安装依赖

**需求**：如何启动；随后让我 install 并启动。

**结果**：
- 环境：pnpm 9.11.0、Node v20.15.1。
- `pnpm install` 成功；`pnpm dev` 启动，Vite v6.4.3，http://localhost:5173。
- 提示：后端需在 5155 才能让 proxy 生效；Node 略低于 Vite 6 推荐（20.19+），实测可跑。

## 6. 后端 health 接口联通测试

**需求**：用后端 health 接口做联通测试。

**结果**：
- 健康端点 `/health`（仅 `self` 检查，不连 DB/Redis）。
- 临时措施：user-secrets 设 `Database:Password=devsecret` 绕过 `ValidateOnStart`；Vite proxy 临时加 `/health` 规则。
- 验证：直连 `:5155/health` 与经代理 `:5173/health` 均返回 `HTTP 200` 且为统一 `Result<T>` 包，证明 浏览器→Vite proxy→后端 链路通。

## 7. 生成前端启动脚本

**需求**：生成前端启动 script。

**结果**：新增 `scripts/web.ps1` 与 `scripts/web.cmd`（与后端 `dev.*` 同风格）。任务：`dev`(默认)/`build`/`preview`/`install`/`lint`；自动定位 `web/`、缺依赖先 `pnpm install`、缺 pnpm 明确报错。`web.ps1 install` 验证通过。

## 8. 更新 README（补脚本用法）

**需求**：README 补脚本用法。

**结果**：技术栈/环境要求加前端工具链（Node 20.19+ / pnpm 9+）；新增「前端开发脚本」小节（PowerShell + cmd 双块）。

## 9. 停掉后台进程

**需求**：停掉。

**结果**：后端 `:5155` 已退出；前端 `:5173`（PID 29568）结束进程，两端口空闲。

## 10. 健康端点改路径 → `/api/v1/health`

**需求**：health 接口路径改为 `api/v1/health`。

**结果**（测试先行 Red-Green）：
- 先改集成测试期望路径（`HealthCheckTests.cs`），再改实现（`HealthCheckHostExtensions.cs` 的 `MapHealthChecks` + XML 注释）；测试通过。
- 同步活文档：`vite.config.ts` 删掉临时 `/health` proxy（已被 `/api` 覆盖）、`dev.ps1`/`dev.cmd`/`web.ps1`/`web.cmd`/`README.md` 中的路径与提示。
- `doc/` 历史文档保留原貌。

## 11. 配置 Vue Router + 空壳页 + App.vue 布局

**需求**：配 3 路由（模型管理 / Agent 管理 / 对话）+ 空壳页 + App.vue 左菜单右 router-view。

**决策与结果**：
- 命名沿用 CLAUDE.md 约定的 `*View.vue`（非示例的 `ProviderList.vue`）。
- 布局并入 `App.vue`（左 `el-aside`+`el-menu` 三项，右 `el-main`+`<RouterView/>`），删除原 `ConsoleLayout.vue` 避免竞争布局。
- 扁平三路由 + 根重定向：`/providers`、`/agents`、`/conversations`。
- 三个空壳页：`ProviderListView.vue` / `AgentListView.vue` / `ConversationView.vue`，各一行文字。
- 删除空壳化后变死代码的 `useProviderList.ts`。类型检查通过。

## 12. ProviderList 调 getHealth 显示连接状态

**需求**：页面加载调 `getHealth()`，成功显示绿色「后端已连接：Hify is running」，失败红色「后端未连接」。

**结果**：
- 新增 `api/health.ts`：`getHealth()` 用独立 axios 请求（不走全局拦截器，避免失败弹全局 toast），10s 超时（连通性测试约定）。
- `ProviderListView.vue` `onMounted` 调用，`connected` 三态（null 检测中），绿 `#67c23a` / 红 `#f56c6c`。类型检查通过。
- 说明：成功文案按用户字面量写死「Hify is running」；如需显示后端真实 `checks[0].description` 可再调。

## 13. 一键启动脚本

**需求**：写 start cmd：构建后端→后台启动→轮询健康→启动前端→开浏览器，任一步失败即停并提示。

**结果**：新增 `scripts/start.cmd`。
- 流程：检查 dotnet/pnpm/curl → build 后端 → 新窗口后台起后端(5155) → `curl -sf` 轮询 `/api/v1/health`(≤60s) → 缺依赖则 `pnpm install` 后新窗口起 `pnpm dev`(5173) → 轮询前端就绪后开浏览器。
- 错误处理用 goto 标签；失败时 `taskkill /FI WINDOWTITLE` 清理已起窗口。
- 后端端口取 5155 对齐 proxy；延时用 `ping` 不用 `timeout`（stdin 重定向下更稳）。
- 未自动运行（会弹窗+开浏览器，有外部副作用）。

---

## 当前状态小结

- 前端脚手架可运行（http://localhost:5173），路由 3 页 + 健康状态指示。
- 后端为骨架：6 模块 `Endpoints/` 仍是占位，业务接口未实现；`/api/v1/health` 可用（仅 self 检查）。
- 脚本：后端 `scripts/dev.*`、前端 `scripts/web.*`、一键 `scripts/start.cmd`。
- 待办参考：实现某模块竖切（如 ModelProvider 的 `/providers`）、补 docker-compose（PG18+pgvector+Redis）、health 接入真实 DB/Redis 就绪检查（可能拆 live/ready）。
