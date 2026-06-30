# MCP 模块开发会话记录

> 日期：2026-06-26 ~ 06-30
> 主题：MCP 工具接入模块从概念、数据模型、后端实现、接入对话、到前端的端到端落地。
> 形式：按用户请求顺序记录「需求 → 决策 / 处理 → 结果」。全程 test-first，DB 集成测试走真实 PostgreSQL。

---

## 1. 概念答疑（MCP / 传输）

**需求**：解释 MCP 是什么、解决什么问题、与直接调 REST 的区别（智能客服场景）；随后问 SSE 与 Streamable HTTP 的区别。

**结果**：
- MCP = LLM/Agent 与外部工具/数据源之间的标准化协议（USB-C 类比）；核心解决 N×M 集成爆炸，把乘法变加法（每应用一个 Client、每工具一个 Server）。
- 传输两代：旧 HTTP+SSE（两端点、长连接、难水平扩展，已废弃）vs 新 Streamable HTTP（单端点、按需流式、`Mcp-Session-Id` 会话、可断点续传、近无状态）。
- 落到本项目：`Hify.Modules.Mcp` 是 Client 侧，建议 Streamable HTTP。

## 2. 数据模型设计（review）

**需求**：先设计 MCP 模块数据模型供 review。

**决策（用户拍板）**：① 仅支持 `streamable_http`（砍 stdio/sse，删 command/args/env）；② 工具重新发现走**原地 upsert**、id 永不变（消失只置 `available=false`，保护 agent 绑定）；③ 不建工具调用日志表（复用 `conversation.message`）；④ 连接状态内联 `mcp_server`，不拆健康表。

**结果**：重写 `ddl.sql` 的 mcp 段（`mcp_server` 补鉴权三件套 + `timeout_ms` + 状态/同步元数据；`mcp_tool` 用 `text` 描述 + `available`）；新建 `McpServer`/`McpTool` 实体 + `McpDbContext`。

## 3. API + 实现设计（review，含并发）

**需求**：设计模块 API 与实现供 review，并考虑并发调用支持。

**决策（用户拍板）**：① 协议客户端用**官方 SDK**（`ModelContextProtocol`）；② 凭证加密上提 `Hify.Shared`；③ 不做后台周期同步/探活；④ 不做本地 JSON Schema 校验（透传给 Server）。

**关键设计**：
- 跨模块契约 `IMcpToolQuery` / `IMcpToolInvoker`；`InvokeManyAsync` 三层并发控制（每-Server 舱壁+熔断、单批并行度信号量、每调用超时），逐项隔离失败、结果顺序一致。
- 协议层抽象 `IMcpProtocolClient`，SDK 版 `StreamableHttpMcpClient` 每次短会话（建连→握手→操作→释放）。

## 4. 实现 MCP 模块（任务 1–12）

**需求**：先规划任务，再从 1 开始实现。

**结果**（test-first，逐任务绿）：
1. 引入 `ModelContextProtocol.Core` 1.4.0（选 .Core，纯 Client）。
2. 凭证加密上提 `Hify.Shared.Security`（`AddHifyCredentialProtection` 幂等注册）；配置节 app 级化 `ModelProvider:CredentialProtection` → `CredentialProtection`（同步改 docker-compose/README/skill/测试工厂）；ModelProvider 回归 76 绿。
3. `Hify.Contracts/Mcp` 契约 + `McpErrorCode`（5xxx）。
4. 协议客户端 + 真 HTTP 集成测试（AspNetCore + TestHost 起 in-process MCP server）。
5. 每-Server 弹性管道（Polly `ResiliencePipelineRegistry<long>`）+ `McpOptions`。
6. `McpServerService`（CRUD，凭证留空保留、重名冲突、级联软删）。
7. `McpConnectivityService`（test-connection 握手刷新状态）。
8. `McpToolSyncService`（tools/list + 原地 upsert，**核心验证 id 稳定**）。
9. `McpToolService` + `McpToolQuery`（列表/启停 + 只读查询）。
10. `McpToolInvoker`（`IMcpToolInvoker`，并发：部分失败隔离/顺序/真并行/超时全验证）。
11. 端点 `McpServersController` + `McpToolsController`。
12. `McpModule` 注册 + 全量构建与测试。

**过程发现（已记入记忆）**：DB 集成测试默认连 `hify/hify`，本机不存在 → **静默跳过仍记 passed**。建 `hify_test`（本机 PG `postgres:123456`）+ 设 `HIFY_TEST_DB` 后测试才真跑；`McpSchemaFixture` 用原生 Npgsql（非 EF）按 `ddl.sql` 重建 mcp 两表（EF 会把 `'{}'` 当占位符报错）。

## 5. 接入 Conversation（任务 13–18）

**需求**：把 MCP 工具接入对话循环。

**关键发现**：大头不是接线，而是 LLM 层无 function-calling。

**决策（用户拍板）**：① 用**原生 function-calling**（扩 ModelProvider 契约+适配器）；② **迭代非流式 + 只流最终答**；③ 先只做 **OpenAI 兼容**适配器（Claude/Ollama 忽略 Tools 优雅降级）。

**结果**：
- ModelProvider 契约加 `ToolDefinition`/`ToolCall` + `ChatRequest.Tools`/`ChatResponse.ToolCalls`/`ChatMessage.ToolCalls`/`ToolCallId`；`OpenAiCompatibleAdapter` 实现工具序列化与解析。
- `ContextBuilder` 按 `model.SupportsTools && agent.ToolIds` 装配工具 + 工具名→id 映射；历史回放过滤工具中间消息（`ToolCalls=="[]"`）。
- `ConversationOrchestrator` 工具循环：`ChatAsync(tools)` 探测 → `InvokeManyAsync` 执行 → assistant(tool_calls)+tool 落库回喂 → 收尾 `ChatStreamAsync` 流式最终答；`ChatEvent` 加 `tool_call`/`tool_result` SSE 事件。
- 顺手修了 Conversation 既有 bug（`Message_JsonbToolCalls_RoundTrips` 在未提交事务上另开连接读，改为同连接重查），全量套件回到全绿。

## 6. MCP 前端原型（review）

**需求**：开始前端开发前先出原型 review。

**结果**：生成可视化原型（沿用现有设计令牌），覆盖 Server 列表 / 表单 / 工具管理 / 对话工具调用四界面。

**决策（用户拍板）**：① 工具列表用**独立详情页**；② 加**清理已移除工具**；③ Schema 用**弹窗**；④ 对话工具调用**可展开看入参/返回**；⑤ 菜单名「MCP 工具」。

## 7. 前端实现（任务 19–26）+ 后端补充

**结果**：
- 后端补：工具 SSE 事件带 `arguments`/`result`（截断）；`PruneRemovedToolsAsync` + `POST /mcp-servers/{id}/tools/prune`（软删 available=false）。
- `features/mcp` 切片：`api/mcp.ts` 全端点 + 类型；`constants.ts`（状态/鉴权/传输 meta）；`McpServerListView` + `McpServerFormDialog`；`McpServerDetailView`（`/mcp-servers/:id`）+ `ToolSchemaDialog`；清理已移除工具。
- 对话改造：`useChat`/`useSse`/`MessageBubble` 处理 tool_call/tool_result，渲染「调用工具X ✓/✕」可展开看入参/返回。
- router + App.vue 侧边栏「MCP 工具」；前端四门（type-check/lint/test/build）全绿；补 `jiti`（应用户要求，使 ESLint TS 配置可加载）。

## 8. 修复 Agent 编辑页工具下拉

**需求**：新增的工具未出现在 Agent 编辑页工具下拉，请修复。

**根因**：该字段是手动输入工具 ID 的 `allow-create` 占位（注释「MCP 列表接口上线后替换为选择器」），从不拉取真实工具。

**结果**：新增 `useMcpTools` composable（跨 Server 聚合 available 工具、按 Server 分组），把字段换成真正的分组多选下拉（直接绑 `form.toolIds`，停用工具标注「（已停用）」，空态提示），移除手动 ID 解析。四门复跑全绿。

---

## 当前状态小结

- **后端**：MCP 模块完整（数据模型 / 协议客户端 / 弹性 / CRUD / 连通性 / 工具发现 upsert / 工具管理 / 并发调用 / 清理）；已接入 Conversation 工具循环；ModelProvider 具备 OpenAI 兼容 function-calling。
- **前端**：`features/mcp` 管理界面（列表 + 详情工具页）+ 对话工具调用展示 + Agent 绑定工具选择器。
- **测试**（真实 PG）：后端 483 通过 / 0 失败（Shared 91 · Agent 62 · Knowledge 90 · Mcp 48 · ModelProvider 79 · Conversation 63 · Integration 50）；前端 vitest 7、type-check / lint / build 全绿。
- **运行约定**：DB 集成测试须带 `HIFY_TEST_DB`（指向已应用 `ddl.sql` 的库），否则静默跳过。
- **已知限制 / 待办**：function-calling 仅 OpenAI 兼容适配器（Claude/Ollama 原生工具格式待补）；工具名在单个 Agent 工具集内须唯一；有工具路径首个 LLM 调用失败作为 SSE error 事件；「只流最终答」使工具路径最终轮多一次 LLM 调用。
