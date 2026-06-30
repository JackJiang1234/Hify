# 工作流模块设计（简单拖拽版）— 草案待确认

> 模块：`Hify.Modules.Workflow`（L2 编排层，依赖 Agent / ModelProvider / Mcp，仅经 `Hify.Contracts`）。
> 状态：**设计已定稿**（决策 A–G 见 §12 全部拍板）。当前模块为空 stub（仅 `WorkflowModule.cs`），消费契约已全部核实存在（§3）。下一步：Test-First 实现。

## ⚠️ 范围变更声明

CLAUDE.md 原文：**「不做可视化工作流拖拽编排」**。本设计**有意突破该边界**，做一个**简单拖拽**版本——但通过下列约束把成本压到「比纯表单略增、远小于完整编排器」：

- 图结构限制为 **线性 + 单层条件分支**（前端连线交互强制约束，不允许任意 DAG / 环 / 多入度汇合）；
- 砍掉完整编排器最贵的连带项：**不做**变量自动联想、不做连线类型实时校验、不做撤销重做、不做运行态画布回填；
- 底层定义仍存成标准的 `{ nodes, edges }` 图 JSON，**前端拖拽只是它的编辑器外壳**，后端执行引擎不关心 JSON 来源。

> 此声明等待你确认。若否决，回退到 CLAUDE.md 原边界（纯表单 / JSON 编辑）。

---

## 1. 目标与范围

### 一期做

- 工作流 **创建 / 编辑 / 列表 / 删除（软删）/ 发布**（定义存 JSON）
- **简单拖拽画布**（Vue Flow），约束为线性 + 单层条件分支
- 执行引擎：解析 JSON → 维护变量上下文 → 按边遍历 → 每种节点一个 handler
- 节点类型五种：`start` / `llm` / `tool`（MCP）/ `condition` / `end`
- **同步执行**：一次调用跑完，落一条运行记录（run），返回最终输出
- 变量引用：`{{nodeId.field}}` 插值

### 一期暂不做 / 二期

- 任意 DAG（分叉汇合、并行、循环节点）、拓扑排序 / 环检测
- 知识检索（RAG）节点 —— 待 Knowledge 模块落地后作为新节点类型接入
- **流式执行**（SSE 推进度 / 中间结果）—— 一期同步执行 + run 记录，前端跑完看结果
- 运行态画布回填、变量自动联想、连线类型校验、撤销重做
- 代码（Code）节点（安全考量大，明确不做）
- 工作流定时触发 / Webhook 触发（一期仅手动「试运行」）

---

## 2. 模块结构（垂直切片）

对齐 `Hify.Modules.Conversation` / `Hify.Modules.ModelProvider` 的切片风格：

```
Hify.Modules.Workflow/
├── WorkflowModule.cs                 # 注册入口（已存在，当前为 stub）
├── Endpoints/
│   ├── WorkflowsController.cs        # /api/v1/workflows（CRUD + 发布）
│   └── WorkflowRunsController.cs     # /api/v1/workflows/{id}/runs（试运行 + 查运行记录）
├── Features/
│   ├── Definitions/                  # 定义 CRUD + 校验
│   │   ├── WorkflowService.cs
│   │   ├── WorkflowMapping.cs
│   │   ├── WorkflowRequests.cs       # 入参 record + 校验
│   │   └── DefinitionValidator.cs    # 校验图合法性（线性+单层分支、变量引用可解析、无环）
│   ├── Execution/                    # 核心：执行引擎
│   │   ├── WorkflowEngine.cs         # 主流程：遍历图、维护上下文
│   │   ├── VariableResolver.cs       # {{nodeId.field}} 解析
│   │   ├── ExecutionContext.cs       # 变量池 + run 状态
│   │   └── Nodes/                    # 每种节点一个 handler
│   │       ├── INodeHandler.cs
│   │       ├── StartNodeHandler.cs
│   │       ├── LlmNodeHandler.cs
│   │       ├── ToolNodeHandler.cs
│   │       ├── ConditionNodeHandler.cs
│   │       └── EndNodeHandler.cs
│   └── Runs/                         # 运行记录查询
│       └── WorkflowRunService.cs
├── Domain/                           # internal 实体
│   ├── Workflow.cs
│   └── WorkflowRun.cs
└── Persistence/
    └── WorkflowDbContext.cs          # 独立 DbContext / 独立 schema=workflow
```

---

## 3. 对外契约（Hify.Contracts）

**结论：Workflow 无需对外暴露契约**（与 Conversation 同理——它是依赖链顶端的 L2，没有其它模块依赖它）。API 的 request/response DTO 留在模块内部按 `internal` 处理，`Hify.Contracts/Workflow/` 保持空。

它**消费**的契约（已逐一核实，全部存在，无需新增 Contracts）：

| 来自 | 接口（实际签名） | 用途 | 状态 |
|---|---|---|---|
| ModelProvider | `IModelInvoker.ChatAsync(long modelId, ChatRequest, ct) → Result<ChatResponse>` | `llm` 节点调模型（同步非流式，正合一期同步执行） | ✅ 已存在 |
| ModelProvider | `IModelProviderQuery.GetModelAsync` | 校验模型存在 / 启用 | ✅ 已存在 |
| Mcp | `IMcpToolInvoker.InvokeAsync(McpToolCall, ct) → Result<McpToolResult>` | `tool` 节点调 MCP 工具 | ✅ 已存在 |
| Mcp | `IMcpToolQuery.GetInvocableToolsAsync` | 校验工具存在 / 可调用 | ✅ 已存在 |
| Agent | `IAgentQuery.GetAgentAsync` | 决策 E=内联，一期**不用** | ✅ 存在（备用） |

实现要点（据已核实的 DTO 形状）：

- `llm` 节点：装配 `ChatRequest{ Messages=[system?, user], MaxTokens, Temperature }` → `ChatAsync` → 取 `ChatResponse.Content` 作为输出字段 `text`；`Tools` 留空（工作流 llm 节点一期不在节点内开工具循环，要调工具就单独连一个 `tool` 节点）。
- `tool` 节点：`McpToolCall{ ToolId=config.mcpToolId, CallId=节点id, ArgumentsJson=序列化(解析后的 config.args) }`——注意 `ArgumentsJson` 是 **JSON 字符串**不是字典，变量解析后需 `JsonSerializer.Serialize`；取 `McpToolResult.Content` 作输出字段 `result`，`IsError=true` 视为节点失败（6004）。

---

## 4. REST API

遵循接口规范：`/api/v1/{资源复数}`，非 CRUD 用动词；统一 `Result<T>` / `PageResult<T>`；错误码 **6xxx**（Workflow 段）。

| 方法 | 路径 | 说明 | 响应 |
|---|---|---|---|
| POST | `/api/v1/workflows` | 新建工作流 | `Result<WorkflowDto>` |
| GET | `/api/v1/workflows` | 工作流列表（游标分页） | `PageResult<WorkflowDto>` |
| GET | `/api/v1/workflows/{id}` | 取工作流详情（含 definition JSON） | `Result<WorkflowDto>` |
| PUT | `/api/v1/workflows/{id}` | 更新（含画布定义） | `Result<WorkflowDto>` |
| DELETE | `/api/v1/workflows/{id}` | 软删 | `Result<bool>` |
| POST | `/api/v1/workflows/{id}/publish` | 发布（draft → published，发布前跑校验） | `Result<WorkflowDto>` |
| **POST** | **`/api/v1/workflows/{id}/runs`** | **试运行（同步执行，body 带 inputs）** | `Result<WorkflowRunDto>` |
| GET | `/api/v1/workflows/{id}/runs` | 运行记录列表（游标分页） | `PageResult<WorkflowRunDto>` |
| GET | `/api/v1/workflows/{id}/runs/{runId}` | 运行详情（含逐节点 trace） | `Result<WorkflowRunDto>` |

> 一期 run 为**同步执行**：`POST /runs` 阻塞到跑完返回最终结果（受同步超时约束，见 §8）。二期若改流式，此接口升级为 SSE。

---

## 5. 数据模型（遵守 DB 规范）

schema `workflow`。严格遵守 CLAUDE.md DB 规范：`bigint GENERATED ALWAYS AS IDENTITY` 主键、所有列 `NOT NULL + DEFAULT`、软删 `deleted_at bigint DEFAULT 0`、枚举 `varchar(32)`、定义用 `jsonb`。

> 已落地于 `ddl.sql`（schema `workflow`），与下表一致。

### 5.1 `workflow.workflow` — 工作流定义

| 列 | 类型 | 说明 |
|---|---|---|
| `id` | bigint PK identity | |
| `name` | varchar(128) NOT NULL DEFAULT '' | 名称（唯一） |
| `description` | varchar(512) NOT NULL DEFAULT '' | 描述 |
| `definition` | jsonb NOT NULL DEFAULT '{}' | 画布定义 `{ nodes, edges }`（§6） |
| `status` | varchar(32) NOT NULL DEFAULT 'draft' | `draft` / `published`（替换原 enabled；不设 version——简单性优先） |
| `created_at` | bigint NOT NULL DEFAULT 0 | epoch ms |
| `updated_at` | bigint NOT NULL DEFAULT 0 | epoch ms |
| `deleted_at` | bigint NOT NULL DEFAULT 0 | 0=未删 |

索引：`idx_workflow_name`（UNIQUE，`WHERE deleted_at = 0`）、`idx_workflow_status`（`WHERE deleted_at = 0`，列表按 status 过滤 + id 游标）。

### 5.2 `workflow.workflow_run` — 运行记录

| 列 | 类型 | 说明 |
|---|---|---|
| `id` | bigint PK identity | |
| `workflow_id` | bigint NOT NULL DEFAULT 0 | 应用层维护关系，无 DB 外键 |
| `status` | varchar(32) NOT NULL DEFAULT '' | `running` / `succeeded` / `failed` |
| `inputs` | jsonb NOT NULL DEFAULT '{}' | 触发输入 |
| `output` | text NOT NULL DEFAULT '' | 最终输出文本（end 节点产出，纯文本非 JSON——曾误设 jsonb，存纯文本会 22P02） |
| `trace` | jsonb NOT NULL DEFAULT '[]' | 逐节点执行轨迹 `[{nodeId,status,ms,input,output}]`，供调试 |
| `error_message` | varchar(512) NOT NULL DEFAULT '' | 失败原因（截断，不含敏感数据 / 完整提示词） |
| `started_at` | bigint NOT NULL DEFAULT 0 | |
| `finished_at` | bigint NOT NULL DEFAULT 0 | |
| `created_at` / `updated_at` / `deleted_at` | bigint NOT NULL DEFAULT 0 | |

索引：`idx_workflow_run_workflow_id_created_at`（`WHERE deleted_at = 0`，按 workflow_id 查 + 翻页）。

> 一期把逐节点轨迹塞进 run 的 `trace` jsonb，**不单独建 node_run 表**（简单性优先；将来需要按节点维度查询统计时再拆表）。

---

## 6. 工作流定义 JSON Schema

`{ nodes, edges }` 图结构，字段兼容 Vue Flow（`nodes` 带 `id/type/position`，`edges` 带 `source/target/sourceHandle`）。外层 `position` 仅前端渲染用，引擎忽略。

```jsonc
{
  "version": "1",
  "nodes": [
    {
      "id": "start_1",
      "type": "start",
      "title": "开始",
      "position": { "x": 0, "y": 0 },
      "config": {
        "inputs": [
          { "name": "user_input", "type": "string", "required": true }
        ]
      }
    },
    {
      "id": "llm_1",
      "type": "llm",
      "title": "意图分类",
      "position": { "x": 280, "y": 0 },
      "config": {
        "modelId": 12,
        "systemPrompt": "你是分类助手。",
        "prompt": "判断下面问题属于[技术]还是[销售]：{{start_1.user_input}}",
        "params": { "temperature": 0 }
      }
      // 输出固定字段名 text（见 §7 各节点输出约定）
    },
    {
      "id": "cond_1",
      "type": "condition",
      "title": "分支",
      "position": { "x": 560, "y": 0 },
      "config": {
        "cases": [
          { "handle": "c1", "left": "{{llm_1.text}}", "op": "contains", "right": "技术" }
        ]
        // 不匹配任何 case 走 else handle
      }
    },
    {
      "id": "tool_1",
      "type": "tool",
      "title": "知识检索工具",
      "position": { "x": 840, "y": -80 },
      "config": {
        "mcpToolId": 5,
        "args": { "query": "{{start_1.user_input}}" }
      }
      // 输出固定字段名 result
    },
    {
      "id": "end_1",
      "type": "end",
      "title": "结束",
      "position": { "x": 840, "y": 80 },
      "config": { "output": "{{llm_1.text}}" }
    }
  ],
  "edges": [
    { "id": "e1", "source": "start_1", "target": "llm_1" },
    { "id": "e2", "source": "llm_1",   "target": "cond_1" },
    { "id": "e3", "source": "cond_1",  "target": "tool_1", "sourceHandle": "c1" },
    { "id": "e4", "source": "cond_1",  "target": "end_1",  "sourceHandle": "else" }
  ]
}
```

### 图约束（DefinitionValidator 强制，发布前校验）

1. **有且仅有一个 `start`、至少一个 `end`**；
2. 除 `condition` 外，每个节点**至多一条出边**（线性）；`condition` 节点出边 = 各 case 的 handle + 一条 `else`；
3. 除 `start` 外，每个节点**至多一条入边**（不允许多入度汇合）；
4. **无环**（沿出边可达性检查，不依赖完整拓扑排序——线性结构足够简单）；
5. 每个 `{{nodeId.field}}` 引用的 `nodeId` 必须在**当前节点的前驱链**上存在；
6. `start` 无入边、`end` 无出边。

> 校验在两处跑：保存草稿时给**警告**不拦截；**发布时拦截**（不通过则 422 + 6xxx 错误码）。

---

## 7. 执行引擎（WorkflowEngine）

### 节点 handler 抽象

```csharp
internal interface INodeHandler
{
    // 节点类型标识，与 JSON 的 node.type 对应。
    string NodeType { get; }

    // 执行单个节点：从上下文解析输入 → 执行 → 返回输出 + 下一跳决策。
    Task<NodeResult> ExecuteAsync(NodeRunContext context, CancellationToken cancellationToken);
}

internal sealed record NodeResult(
    IReadOnlyDictionary<string, object> Output,  // 写回上下文，键即该节点输出字段
    string? NextHandle);                          // condition 用：选中的出边 handle；其余节点为 null（走唯一出边）
```

### 各节点输出字段约定（变量引用据此）

| 节点 | 输出字段 | 引用示例 |
|---|---|---|
| `start` | 各 input 的 name | `{{start_1.user_input}}` |
| `llm` | `text` | `{{llm_1.text}}` |
| `tool` | `result`（MCP 返回原样） | `{{tool_1.result}}` |
| `condition` | 无输出（只决定走向） | — |
| `end` | 无（汇总为 run.output） | — |

### 主流程（伪代码）

```
1. 取 published 定义（试运行也可跑 draft），解析 nodes/edges
2. 建 run 记录(status=running, inputs)
3. 校验 inputs 满足 start.inputs 的 required
4. ctx = ExecutionContext(); ctx[start.id] = inputs
5. cur = start 节点; steps = 0
6. while cur.type != 'end':
     - 防失控：steps++ 超过 MAX_STEPS(如 64) → 失败(6xxx)
     - resolver 把 cur.config 里的 {{...}} 用 ctx 解析成实参
     - handler = handlers[cur.type]
     - result = await handler.ExecuteAsync(...)   // 各节点超时 + 异常 → 失败
     - ctx[cur.id] = result.Output
     - 记一条 trace
     - 选下一跳：
         condition → 按 result.NextHandle 找出边
         其余      → 唯一出边
       找不到出边 → 失败(6xxx 图断裂)
     - cur = 出边.target
7. end 节点：output = resolver(end.config.output)
8. run 更新(status=succeeded, output, trace, finished_at)
9. 返回 WorkflowRunDto
```

### condition 求值（一期最简）

- 每个 case = 单个比较 `left op right`，`op ∈ { eq, ne, contains, gt, lt }`；
- 按 cases 顺序求值，**首个为真**的 case → 其 handle；全假 → `else`；
- **不支持** AND/OR 组合、嵌套表达式（二期再说）。比较前两侧都先做变量解析，数值比较失败则按字符串比较。

### 容错（对齐 CLAUDE.md「外部调用必须设超时」）

- `llm` / `tool` 节点：复用 ModelProvider / Mcp 既有的熔断 + 超时；节点级再包一层超时；
- 任一节点异常 → run 置 `failed` + `error_message`（脱敏），**不抛到 API 边界**（返回 `Result` 失败，6xxx）；
- 整体执行受同步超时约束（见下）。

---

## 8. 执行模式：一期同步

- `POST /runs` **同步阻塞**到工作流跑完返回，适配「简单 + 小规模（3-5 QPS）」定位；
- **同步总超时**：建议 60s（与 CLAUDE.md 同步调用 60s 对齐）；含多个 LLM 节点易超，发布时给提示；
- 前端「试运行」转圈等结果，跑完渲染 run.output + trace；
- **二期**：长流程改 SSE 流式逐节点推进度（`node_start` / `node_done` / `done` 帧），届时 run 改异步 + 状态轮询或流式订阅。

---

## 9. 前端拖拽设计（Vue Flow）

特性切片 `web/src/features/workflow/`（已存在 `views/ components/ composables/` 空目录）。

```
features/workflow/
├── views/
│   ├── WorkflowListView.vue       # 列表（useTable 分页）
│   └── WorkflowEditorView.vue     # 画布编辑器（Vue Flow 容器）
├── components/
│   ├── FlowCanvas.vue             # @vue-flow/core 画布
│   ├── nodes/                     # 自定义节点组件（5 种），与 node.type 注册映射
│   │   ├── StartNode.vue / LlmNode.vue / ToolNode.vue / ConditionNode.vue / EndNode.vue
│   ├── NodePanel.vue              # 选中节点的配置表单（Element Plus）
│   └── RunResultDrawer.vue        # 试运行结果 + trace 展示
├── composables/
│   └── useFlowGraph.ts            # 画布 ↔ {nodes,edges} JSON 互转 + 连线约束
├── store.ts                       # 当前编辑的工作流状态
└── types.ts                       # 与后端 JSON schema 对齐的 TS 类型
```

### 选库

- **`@vue-flow/core`**（Vue 3 原生，Dify 用的 React Flow 的 Vue 同源实现）——最贴近目标体验，节点用自定义 Vue 组件，TS 友好。

### 如何「强制约束成线性 + 单层分支」（省工关键，见 §1 声明）

在 `useFlowGraph.ts` 的连线钩子里拦截（Vue Flow 的 `isValidConnection` / `onConnect`）：

- **出边限制**：非 condition 节点已有出边时，拒绝再连；
- **入边限制**：非 start 节点已有入边时，拒绝再连；
- **condition 出边**：仅允许从其 case handle / else handle 连出；
- **禁环**：连线前做一次可达性检查，目标可回到源则拒绝；
- 拒绝时给 Element Plus 轻提示，不做复杂高亮。

> 这些约束让用户**画不出**后端简单引擎跑不了的图，从而省掉「任意 DAG 引擎」与「复杂连线校验」两块大成本。

### 明确不做（省工）

变量自动联想（手填 `{{nodeId.field}}`）、连线类型实时校验、撤销重做、运行态画布实时回填。

---

## 10. 错误码（6xxx Workflow 段，建议）

| 码 | 含义 |
|---|---|
| 6001 | 工作流不存在 |
| 6002 | 定义非法（图校验未过：缺 start/end、多入多出、有环、变量引用不可解析） |
| 6003 | 试运行输入缺失 / 不满足 start required |
| 6004 | 节点执行失败（LLM / 工具上游错误） |
| 6005 | 执行超出最大步数（疑似环 / 失控） |
| 6006 | 执行超时（同步总超时） |
| 6007 | 引用的模型 / Agent / MCP 工具不存在或已停用 |

> 具体数字待与其它模块对齐后定稿。

---

## 11. 测试策略（Test-First）

- **集成测试优先、真实依赖**：Testcontainers 起真实 PG；LLM / MCP 用**可控假适配器**（按脚本返回），不真打外部。
- 表驱动单测：
  - `VariableResolver`：各种 `{{nodeId.field}}` 解析、缺失引用、嵌套对象取值；
  - `DefinitionValidator`：合法 / 多出边 / 多入边 / 有环 / 引用前驱不存在 / 缺 start-end 各一例；
  - `ConditionNodeHandler`：eq/ne/contains/gt/lt × 命中/未命中/走 else；
- 端到端：一条「start → llm → condition → (tool|end)」的工作流试运行，断言 run.output 与 trace 顺序。

---

## 12. 决策记录（✅ 已全部拍板 2026-06-30）

| 决策 | 结论 | 影响 |
|---|---|---|
| **A** 是否突破「不做拖拽」边界 | ✅ **做简单拖拽** | 已同步更新 CLAUDE.md（做什么/不做什么/模块树）+ WorkflowModule.cs 注释 + memory |
| **B** 图表达能力 | ✅ **线性 + 单层分支** | 前端连线约束（§9），引擎免做拓扑排序/环检测 |
| **C** 节点类型范围 | ✅ **5 种**（start/llm/tool/condition/end） | RAG 待 Knowledge 就绪再加为新节点；Code 节点不做 |
| **D** 执行模式 | ✅ **同步**（60s 超时） | `POST /runs` 阻塞返回；流式二期 |
| **E** `llm` 节点配置 | ✅ **内联**（modelId + prompt + params） | 不绑 Agent，一期不用 `IAgentQuery`；绑 Agent 二期可选 |
| **F** condition 表达力 | ✅ **单比较**（eq/ne/contains/gt/lt） | 不支持 AND/OR / 嵌套，组合二期 |
| **G** 定义存储 | ✅ **单 jsonb**（`definition` 列） | 读写整存整取，不拆 node/edge 表 |

### 消费契约核实结果

§3 三项已全部核实**存在**，无需新增 Contracts。决策 E=内联，故 `IAgentQuery` 一期不用。可直接进入实现。

> 下一步：Test-First 实现，从失败测试开始（建议顺序：DefinitionValidator → VariableResolver → 各 NodeHandler → WorkflowEngine 端到端 → API/前端）。

---

## 13. 开发任务规划

原则：**Test-First**（标 🧪 的任务从失败测试开始，Red-Green-Refactor）；**自底向上**，依赖项先行。后端 §13.1 与前端 §13.3 在「API 契约冻结」后可**并行推进**（前端先用 §6 JSON schema + §4 API 表对齐类型，必要时 mock）。

DDL 约定：对齐 ModelProvider——**手写 DDL 文件**（非 EF Migration），实体仅做 EF 映射。

### 13.1 后端（`Hify.Modules.Workflow`）

**阶段 BE-A：数据层与脚手架**（无外部依赖，最先做）✅ 完成

- [x] **BE-A1** 手写 DDL：schema `workflow` + 两表（`workflow`、`workflow_run`）+ 索引（§5）——已对齐 status/trace 决议
- [x] **BE-A2** 实体 `Workflow` / `WorkflowRun`（class，internal，`Domain/`）+ 状态常量 `WorkflowStatus` / `WorkflowRunStatus`
- [x] **BE-A3** `WorkflowDbContext`（独立 schema=workflow，`Persistence/`）+ EF 映射（jsonb 列、软删全局过滤）
- [x] **BE-A4** `WorkflowModule.RegisterServices`：注册 DbContext（csproj 补 EF/Npgsql/AspNetCore 引用）；Host 已装配。后续服务/handler 待 BE-B~F 补

**阶段 BE-B：定义模型与校验**（依赖 A）✅ 完成

- [x] **BE-B1** 定义反序列化模型：`WorkflowDefinition` / `WorkflowNode` / `WorkflowEdge`（record，对齐 §6 JSON）+ `WorkflowNodeType` 常量 + `WorkflowErrorCode`（6xxx）+ `VariableRef` 引用提取助手
- [x] 🧪 **BE-B2** `DefinitionValidator`（§6 六条约束）+ 表驱动单测 **18 项全通过**：合法线性 / 合法分支 / 解析失败 / 缺 start / 双 start / 缺 end / Id 重复 / 类型非法 / 连线悬空 / 多出边 / 多入边汇合 / start 有入边 / end 有出边 / 有环 / condition 缺 handle / 引用不存在 / 引用非前驱

**阶段 BE-C：变量解析**（依赖 A）✅ 完成

- [x] 🧪 **BE-C1** `VariableResolver`（`{{nodeId.field}}` 解析，支持嵌套字典/JsonElement 下钻、类型字符串化、缺失兜底空串、`TryResolveValue` 保留原始类型供数值比较）+ 表驱动单测 **11 项全通过**

**阶段 BE-D：节点 handlers**（依赖 B、C）✅ 完成

- [x] **BE-D1** `INodeHandler` 抽象 + `NodeRunContext` / `NodeResult`（§7）+ `NodeConfigJson` / `NodeOutputField` 助手
- [x] 🧪 **BE-D2** `StartNodeHandler`（校验 inputs required → 透出输入）+ 单测 5 项
- [x] **BE-D3** `LlmNodeHandler`（装配 `ChatRequest` → `IModelInvoker.ChatAsync` → 输出 `text`；无 modelId→6007，上游失败→6004）
- [x] **BE-D4** `ToolNodeHandler`（递归解析 args 内 `{{}}` 重建 JSON → `McpToolCall` → `IMcpToolInvoker.InvokeAsync`；`IsError`/调用失败→6004 → 输出 `result`）
- [x] 🧪 **BE-D5** `ConditionNodeHandler`（eq/ne/contains/gt/lt × 命中/未命中/走 else，数值优先回退字符串）+ 表驱动单测 13 项
- [x] **BE-D6** `EndNodeHandler`（解析 `config.output` → 字段 output）

> D3/D4/D6 的运行路径由 BE-E2 端到端用假 LLM/MCP 适配器覆盖。

**阶段 BE-E：执行引擎**（依赖 D）✅ 完成 → **M1 引擎可跑达成**

- [x] **BE-E1** `ExecutionState`（变量池 + 触发输入 + trace 累积；命名避开 BCL `System.Threading.ExecutionContext`）+ `WorkflowExecution` / `NodeTrace` 结果类型
- [x] 🧪 **BE-E2** `WorkflowEngine`（按图遍历、`MaxSteps=64` 防失控、`OperationCanceledException`→6006、节点失败→failed、产出 WorkflowExecution + trace；不落库，由 BE-F 持久化）+ **端到端测试 6 项**：tech 分支跑 tool / else 分支跳过 tool / LLM 失败→6004 / 工具错误→6004 / 必填缺失→6003 / 环→6005

**阶段 BE-F：Service 与 API**（依赖 B、E）✅ 完成 → **M2 API 通达成**

- [x] **BE-F1** `WorkflowService`（CRUD + `publish`：发布前跑 `DefinitionValidator` → 6002；名称重复 → 6008；定义改动退回 draft）
- [x] **BE-F2** `WorkflowRunService`（试运行调 `WorkflowEngine` + 单次落 run + 运行记录 OFFSET 分页查询）
- [x] **BE-F3** `WorkflowsController` + `WorkflowRunsController`（§4 路由，统一 `Result<T>`/`PageResult<T>`）+ `WorkflowDto`/`WorkflowRunDto`/mapping/请求校验（FluentValidation）
- [x] **BE-F4** 错误码 6xxx 映射（§10）+ 新增 6008 NameConflict / 6009 RunNotFound
- [x] 🧪 **BE-F5** 服务层集成测试 **7 项（真实 PG，事务回滚零残留）**：建→查 / 重名 6008 / 发布非法图 6002 留 draft / 发布合法 / 试运行落 run+trace+output / 必填缺失落 failed run / 软删后查 6001。**实测真实 DB 执行（用时 3s）**

> **本阶段 API 取舍（已落地，记入决策）**：
> - **`definition` 用 JSON 字符串**跨 API（请求+响应）——Host 用 Newtonsoft、模块内用 STJ，字符串避开二者耦合；前端 api 层 `JSON.parse`/`stringify` 各一行。保存仅校验 JSON 合法，发布才校验图。
> - **run `inputs` 用 `string→string` map**——start 声明的输入皆字符串值，避开 Newtonsoft/STJ 边界类型不匹配。
> - **执行失败仍以 `Ok` 返回 run**（status=failed + trace + errorMessage），前端总能拿到 trace 展示；仅预检失败（6001/6002）返回失败 Result 不建 run。
> - **run 单次落库**（同步执行，不写 running 占位再改）——对齐 Conversation「一次性落库」简化。

### 13.2 API 契约冻结点 🚩

BE-F3 路由 + §6 JSON schema 定稿后即冻结契约，前端可全速并行（此前可按本文档 mock）。

### 13.3 前端（`web/src/features/workflow/`）

**阶段 FE-A：基础设施**（可与后端并行，依赖 §6/§4 文档）✅ 完成

- [x] **FE-A1** `pnpm add @vue-flow/core`（1.48.2）
- [x] **FE-A2** `types.ts`（DTO + 画布图结构 + 各节点 config 形状 + NodeTrace）+ `constants.ts`（节点/状态/op 元数据 + definition parse/stringify）
- [x] **FE-A3** `api/workflow.ts`：CRUD + publish + run/listRuns/getRun，走 `client.ts` 拆 `Result<T>`
- [x] **FE-A4** `store.ts`（编辑器 working 状态：元信息 + 画布图 + 选中节点）+ 路由挂载（/workflows、/workflows/new、/workflows/:id）+ App 菜单项；type-check 通过

**阶段 FE-B：列表页**（依赖 A）✅ 完成

- [x] **FE-B1** `WorkflowListView.vue`（OFFSET 分页 + 新建跳编辑器 + 编辑 + 发布 + 删除 + 状态标签；与 mcp/provider 列表同款，未引 useTable——代码库实际内联 page/total/load）+ `nodeTypeMeta`/`workflowStatusMeta`/`runStatusMeta` 安全取值助手；type-check 通过

**阶段 FE-C：画布编辑器**（依赖 A，核心）✅ 完成

- [x] **FE-C1** `WorkflowEditorView.vue`（加载/工具栏/+节点下拉/保存/发布/布局）+ `FlowCanvas.vue`（Vue Flow 容器 + 样式导入 + 节点点击选中）
- [x] **FE-C2** 单一 `nodes/WorkflowNode.vue` 服务 5 种类型（按 type 渲染句柄；condition 动态多 source handle + else）
- [x] **FE-C3** `graph.ts`（纯）：画布 ↔ `{nodes,edges}` 互转 + 默认配置 + 建点；`useFlowGraph.ts` 包装 Vue Flow 接线
- [x] **FE-C4** **连线约束**（`graph.ts isValidConnection` + `useFlowGraph` 接 `:is-valid-connection`/`@connect`）：单入、非 condition 单出、condition 每 handle 一出、禁连 start 入/end 出、**禁环**；非法时轻提示
- [x] **FE-C5** `NodePanel.vue`：按 type 切换配置表单（start inputs 增删；llm modelId+prompt+温度；tool mcpToolId+args JSON；condition cases 行编辑；end output）+ 删节点
- [x] **FE-C6** 保存（create/update）/ 发布（先存后发，6002 经拦截器提示，状态保持 draft）；type-check + 生产构建均通过

> 取舍：5 种类型共用一个节点组件（按 type 渲染）；llm.modelId / tool.mcpToolId 一期用数字输入（下拉接 model/tool 列表为后续优化）；store 用 `ref([]) as Ref<FlowNode[]>` 规避 Vue Flow Node 泛型触发的 TS2589。

**阶段 FE-D：试运行**（依赖 C、后端 F）✅ 完成

- [x] **FE-D1** `RunDialog.vue`：按 start.inputs 动态生成输入表单；编辑器 `试运行` 先保存再弹窗 → 调 `POST /runs`（转圈等同步结果）
- [x] **FE-D2** `RunResultDrawer.vue`：状态标签 + `run.output` + 逐节点 `trace`（el-timeline：节点类型/id/状态/耗时/输出或错误）+ 失败 alert
- [x] **FE-D3** 运行记录 dialog（`运行记录` 按钮查 `listRuns`，点查看调 `getRun` → 复用结果抽屉）；type-check + 生产构建通过

### 13.4 里程碑

| 里程碑 | 含 | 可演示 |
|---|---|---|
| **M1 引擎可跑** | BE-A~E | 集成测试中一条工作流端到端执行通过 |
| **M2 API 通** | BE-F | Postman/HTTP 测试：建→发布→试运行 |
| **M3 画布可编辑** | FE-A~C | ✅ 拖出线性+分支流程并保存、发布（type-check + 构建通过） |
| **M4 闭环** | FE-D | ✅ 前端试运行看到结果 + trace，端到端打通（构建通过） |

> 关键路径：BE-A → B/C → D → E → F（M1/M2）；前端 FE-A 起可并行，FE-D 需等 BE-F。建议先打通后端到 M2，再集中做前端 M3/M4。

## 14. 实现状态总览（2026-06-30）

**全部阶段完成**。后端 60 单测全绿（含 7 项真实 PG 集成）；前端 type-check + 生产构建均通过。

| 层 | 阶段 | 状态 |
|---|---|---|
| 后端 | BE-A 数据层 / B 校验 / C 变量解析 / D 节点 / E 引擎 / F Service+API | ✅ |
| 前端 | FE-A 基础设施 / B 列表 / C 画布编辑器 / D 试运行 | ✅ |
| 里程碑 | M1 引擎可跑 / M2 API 通 / M3 画布可编辑 / M4 闭环 | ✅ |

> 待真实浏览器联调验证（`pnpm -C web dev` + 后端 `dotnet run`）；已知后续优化项：运行记录分页、流式执行（二期）。

### 联调修复（2026-06-30，浏览器反馈）

1. **节点无法连线** —— 原实现把 store 数组用 `v-model:nodes/edges` 绑给 Vue Flow，连线时 `push` 到 store 数组，Vue Flow 不可靠地响应原地数组变更（双源不同步）。改为 **Vue Flow 实例（按固定 `FLOW_ID` 跨组件共享）作为图的单一数据源**：`useFlowGraph` 用 `useVueFlow(FLOW_ID)`，加点/连线/删点走 `addNodes/addEdges/removeNodes`，装载/读出走 `setNodes/setEdges` ↔ `getNodes/getEdges`；store 只留元信息 + selectedNodeId。`<VueFlow :id="FLOW_ID">` 不再绑 `:nodes/:edges`。
2. **llm.modelId / tool.mcpToolId 改下拉** —— `NodePanel` 复用 `useChatModels` / `useMcpTools`，渲染模型下拉与按 Server 分组的工具下拉（替换数字输入）。
