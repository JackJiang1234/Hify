# Agent 会话（对话引擎）设计 — 已实现

> 模块：`Hify.Modules.Conversation`（L2 编排层，依赖 Agent / ModelProvider / Knowledge / Mcp，仅经 `Hify.Contracts`）。
> 配套文档：[端到端时序](conversation_streaming_sequence.md) · [上下文策略](conversation_context_strategies.md) · [存储分工](conversation_storage_roles.md)。
> 状态：**一期已实现并测试通过**（决策 A 不做工具 / B 粗估 / C1 实现 IAgentQuery / C2 选项1 RAG 空 seam / D 首条截断 / E 不绑用户）。

## 实现说明（与草案的取舍）

落地时为简单性做了几处取舍，均记录于此：

- **assistant 消息一次性落库**：流结束后按最终状态（completed/failed/cancelled）插入一条，未做「先写 streaming 占位行再改」——少一次写、逻辑更简单；UI 的「生成中」由 SSE 流本身体现。
- **历史回源上限 50 条**：`ContextBuilder` 回源只取近期 50 条（裁剪本就丢更早的），界定 DB 查询与缓存体积；超长历史靠滑动窗口进一步裁剪。
- **历史分页用 OFFSET（PageResult，id 倒序）**：与现有 `AgentService.ListAsync` 一致；游标分页是大表二期优化。
- **`IAgentQuery` 仅在缺失时返回 NotFound**：停用与否由 `AgentDto.Enabled` 透出，调用方（ContextBuilder/会话创建）判定并返回 4002，职责更单一。
- **SSE 帧体用 System.Text.Json（camelCase）**：自包含，不耦合 Host 的 Newtonsoft 管线（Result 信封仍走 Host 全局 Newtonsoft）。

测试：模块单测/集成 56 项 + 端到端 HTTP（含真实 SSE 消费）3 项，全部通过（真实 PG；Redis 缺省时缓存优雅降级）。

---

## 1. 目标与范围

### 一期做

- 创建会话、按会话发消息、**SSE 流式**回复
- 多轮对话（拼接历史）+ **按 token 预算的滑动窗口**上下文管理
- RAG 注入（调 Knowledge）
- 会话/消息持久化（PG）+ 热上下文缓存（Redis，Cache-Aside）
- 列出会话、查会话历史（游标分页）、删会话（软删）

### 一期暂不做 / 二期

- **工具调用循环（MCP）**【决策 A=不做】：assistant 不发起 tool_call；`message` 的 `tool_calls`/`tool_call_id` 字段先空着备用；`ChatController` 不输出 `tool` 类 SSE 帧
- **RAG 注入**【决策 C2 待确认，倾向延后】：Knowledge 模块当前是完全空壳，RAG 在一期做成**可选 seam（无知识库绑定即跳过）**，实际检索待 Knowledge 模块落地
- 历史摘要 / 历史向量检索（上下文策略 4/5）

### 一期已定调（review 回填）

- **会话标题**【D】：用首条用户消息截断生成（如前 50 字）
- **会话归属**【E】：一期不绑用户，不记 `created_by`（CLAUDE.md 明确不做权限体系）；将来要加再演进
- **token 估算**【B】：字符数 / 系数 粗估，不引入 tokenizer 依赖

---

## 2. 模块结构（垂直切片）

```
Hify.Modules.Conversation/
├── ConversationModule.cs            # 注册入口（已存在，当前为 stub）
├── Endpoints/
│   ├── ConversationsController.cs   # /api/v1/conversations（CRUD + 历史）
│   └── ChatController.cs            # /api/v1/conversations/{id}/messages（SSE 发消息）
├── Features/
│   ├── Chat/                        # 核心：发消息 + 流式编排
│   │   ├── ConversationOrchestrator.cs   # 对话引擎主流程
│   │   ├── ContextBuilder.cs             # 装配 + 裁剪上下文
│   │   ├── SseWriter.cs                  # 把内部事件写成 SSE 帧
│   │   └── ChatRequests.cs               # 入参 record + 校验
│   ├── Conversations/               # 会话 CRUD + 历史查询
│   │   ├── ConversationService.cs
│   │   ├── ConversationMapping.cs
│   │   └── ConversationRequests.cs
│   └── Context/
│       └── ConversationContextCache.cs   # Redis Cache-Aside
├── Domain/                          # internal 实体
│   ├── Conversation.cs
│   └── Message.cs
└── Persistence/
    └── ConversationDbContext.cs     # 独立 DbContext / 独立 schema=conversation
```

> 切片风格对齐 `Hify.Modules.ModelProvider`（`Endpoints/*Controller.cs` + `Features/{切片}/*Service.cs` + `Domain` + `Persistence`）。

---

## 3. 对外契约（Hify.Contracts）

**结论：Conversation 几乎不需要对外暴露契约。** 它是依赖链顶端的 L2 编排层，没有其它模块依赖它。所以：

- API 的 request/response DTO **留在模块内部**（`Features/*Requests.cs` + mapping），按 `internal` 处理，不进 `Hify.Contracts`。
- `Hify.Contracts/Conversation/` 保持空（`.gitkeep`），除非将来 Workflow 要复用对话能力——届时再上提接口。

它**消费**的契约（决策 C 核实结果）：

| 来自 | 接口 | 状态 | 用途 |
|---|---|---|---|
| ModelProvider | `IModelInvoker.ChatStreamAsync` | ✅ 已存在 | 流式调 LLM |
| ModelProvider | `IModelProviderQuery.GetModelAsync` | ✅ 已存在 | 取模型窗口/能力位，算 token 预算 |
| Agent | `IAgentQuery.GetAgentAsync` | ❌ **不存在 → 本期实现** | 取 Agent 配置（提示词/模型/知识库引用） |
| Knowledge | RAG 检索接口 | ❌ **整个模块为空壳** | 检索相关 chunk（见 C2） |
| Mcp | 工具执行接口 | — | 决策 A=不做，本期不用 |

**决策 C 结论：**
- **C1（实现）**：`IAgentQuery` 需新增到 `Hify.Contracts/Agent/`，并在 Agent 模块实现。签名建议：
  ```csharp
  public interface IAgentQuery
  {
      // 按 Id 取 Agent 配置（已含 ModelId/SystemPrompt/ModelParams/KnowledgeBaseIds）。不存在/停用返回 NotFound。
      Task<Result<AgentDto>> GetAgentAsync(long agentId, CancellationToken cancellationToken);
  }
  ```
  `AgentDto` 已具备所需全部字段，无需改 DTO。
- **C2（待你确认的 scope 分叉）**：Knowledge 模块当前仅 `KnowledgeModule.cs` 一个 stub，没有检索接口、没有摄取/分块/向量化管线。RAG 不能靠"补接口"实现，需整个 Knowledge 模块落地——这是独立的大工作量。见 §12-C2。

---

## 4. REST API

遵循接口规范：`/api/v1/{资源复数}`，非 CRUD 用动词；统一 `Result<T>` / `PageResult<T>`；错误码 4xxx。

| 方法 | 路径 | 说明 | 响应 |
|---|---|---|---|
| POST | `/api/v1/conversations` | 新建会话（带 agentId） | `Result<ConversationDto>` |
| GET | `/api/v1/conversations` | 会话列表（游标分页） | `PageResult<ConversationDto>` |
| GET | `/api/v1/conversations/{id}/messages` | 会话历史（游标分页，按 id） | `PageResult<MessageDto>` |
| DELETE | `/api/v1/conversations/{id}` | 软删会话 | `Result<bool>` |
| **POST** | **`/api/v1/conversations/{id}/messages`** | **发消息 + SSE 流式回复** | **`text/event-stream`** |

**发消息接口是唯一的流式接口**，不走 `Result<T>` 信封，返回 SSE 帧（§6）。其余皆标准信封。

---

## 5. 数据模型

已落在 `ddl.sql`（schema `conversation`），本设计沿用，不新增表：

- `conversation.conversation` — 会话（agent_id, title, 时间, 软删）
- `conversation.message` — 消息，含上轮 patch 进的 `tool_calls / tool_call_id / finish_reason / status / error_message`
- 索引 `idx_message_conversation_id_created_at`；**读历史按 `id` 排序**（created_at 同毫秒会撞，仅作分页范围）

要点：
- system 消息**不落库**（来自 Agent 配置，运行时装配，避免冗余与历史不一致）
- assistant 占位行先写 `status='streaming'`，流结束改 `completed`，失败改 `failed`/`cancelled`
- token 用量从 `ChatStreamChunk` 末片的 `PromptTokens/CompletionTokens` 落库

---

## 6. SSE 协议（前后端约定）

`Content-Type: text/event-stream`；前端用原生 `fetch + ReadableStream`（不走 axios），120s 超时；Nginx 关 buffering。

每帧 `data: <json>\n\n`，json 形如 `{ "type": "...", ... }`：

| type | 载荷 | 时机 |
|---|---|---|
| `delta` | `{ "text": "片段" }` | 每个 token 增量 |
| `tool` | `{ "name": "...", "status": "calling\|done" }` | 工具循环中（决策 A 选做时） |
| `done` | `{ "messageId": 123, "finishReason": "stop", "promptTokens": n, "completionTokens": m }` | 正常结束 |
| `error` | `{ "code": 4xxx, "message": "..." }` | 流中途失败（头已发出，无法再用 Result） |

首字之前（响应头未发出）的失败，仍返回标准 `Result<T>` 错误（4xxx）。

---

## 7. 核心编排流程（ConversationOrchestrator）

对应[时序图](conversation_streaming_sequence.md)。内部接口草图（C# 规范：主构造函数注入、async + CancellationToken、ConfigureAwait、IAsyncEnumerable 流式）：

```csharp
internal interface IConversationOrchestrator
{
    // 产出内部事件流，由 Controller 经 SseWriter 写成 SSE 帧。
    IAsyncEnumerable<ChatEvent> StreamReplyAsync(
        long conversationId,
        string userInput,
        CancellationToken cancellationToken);
}

internal interface IContextBuilder
{
    // 取 Agent 配置 + 历史(裁剪) + RAG，装配供应商无关的 ChatRequest。
    Task<Result<ChatRequest>> BuildAsync(
        long conversationId,
        long agentId,
        string userInput,
        CancellationToken cancellationToken);
}
```

主流程（伪代码）：

```
1. 校验会话存在、取 agentId            (Conversation 表 / Redis)
2. ContextBuilder.BuildAsync:
     - 取 Agent 配置                    (IAgentQuery, Redis 缓存)
     - 取模型元数据算 token 预算         (IModelProviderQuery)
     - 取历史 + 滑动窗口裁剪             (Redis 命中 / PG 回填)
     - [C2 待定] RAG 检索注入             (Knowledge；无知识库绑定或模块未就绪则跳过)
     - 落库 user 消息                    (PG)
3. IModelInvoker.ChatStreamAsync(modelId, request, ct)
     - 失败 → yield error 事件并结束（此时可能头未发出，交 Controller 决定走 Result 还是 SSE error）
4. foreach chunk in stream:
     - yield delta 事件
     - 累积全文 + 末片 token
   （决策 A=不做工具：本期无 tool_calls 分支；二期再在此插入 Mcp 循环 ≤ agent.MaxIterations）
5. 落库 assistant 消息(status=completed, tokens) → 更新 Redis 上下文
6. yield done 事件
```

取消：Controller 把 `HttpContext.RequestAborted` 透传进 `ct`，一路到 `IModelInvoker`，用户断开即取消 LLM 调用。

---

## 8. 上下文管理

一期 = **按 token 预算的滑动窗口**（见[策略文档](conversation_context_strategies.md) §2）：

```
预算 = model.context_window - model.max_output_tokens - tokens(system + RAG) - 安全余量
从最新消息往回累加，塞满预算为止；更早的丢弃。
```

- **token 估算**【B=粗估】：用「字符数 / 系数」估算，不引入 tokenizer 依赖；偏保守留余量；二期再精确化。
- **工具组不可拆**（二期才相关）：决策 A=不做工具，本期历史只有 user/assistant，无需处理工具组边界；二期接入工具循环时再按 `tool_calls`/`tool_call_id` 判定整组保留/丢弃。

---

## 9. 存储与缓存

见[存储分工](conversation_storage_roles.md)。Redis Cache-Aside：

- **Key**：`conv:ctx:{conversationId}` → 裁剪好的消息序列（JSON）；`agent:cfg:{agentId}` → Agent 配置
- **TTL**：会话上下文如 30min 滑动过期（冷会话自动释放）；配置 TTL 更长 + 改配置时失效
- **写**：新消息先写 PG（事实来源），再更新/失效 Redis
- Redis 挂 → 退化为每次查 PG，**不丢历史**

---

## 10. 错误码（4xxx Chat 段，建议）

| 码 | 含义 |
|---|---|
| 4001 | 会话不存在 |
| 4002 | Agent 不存在 / 已停用 |
| 4003 | 绑定模型不存在 / 已停用 |
| 4004 | 输入为空 / 超长 |
| 4005 | 上游 LLM 调用失败（流中途，经 SSE error 帧） |
| 4006 | 工具调用超出最大轮次（决策 A 选做时） |
| 4007 | 上下文超窗且无法裁剪 |

> 具体数字待与其它模块对齐后定稿。

---

## 11. 测试策略（Test-First）

- **集成测试优先、真实依赖**：用 Testcontainers 起真实 PG + Redis；LLM 用一个**可控的假适配器**（按脚本吐 chunk），避免真打外部 API。
- 表驱动单测：上下文裁剪（含工具组不可拆边界）、SSE 帧序列化、错误码映射。
- 关键用例：多轮拼接正确、滑动窗口边界、流中途取消落库 `cancelled`、LLM 失败落 `failed` + SSE error、RAG 注入位置。

---

## 12. 决策记录

| 决策 | 结论 | 影响 |
|---|---|---|
| **A** 工具调用循环 | **一期不做** | assistant 不发 tool_call；tool 字段备用；无 `tool` SSE 帧；无 Mcp 依赖 |
| **B** token 估算 | **字符粗估** | 不引入 tokenizer 依赖 |
| **C1** `IAgentQuery` | **不存在 → 本期实现** | 新增 Contracts 接口 + Agent 模块实现 |
| **C2** RAG / Knowledge | **见下，待确认** | Knowledge 是空壳 |
| **D** 会话标题 | **首条用户消息截断** | 新建会话时空标题，首条消息后回填 |
| **E** 会话归属 | **不绑用户** | 不记 created_by |

### C2 —— 唯一待你确认的 scope 分叉 ⭐

核实发现：**Knowledge 模块当前是完全空壳**（仅 `KnowledgeModule.cs` stub，无检索接口、无文档摄取/分块/向量化管线）。RAG 不能靠"补一个接口"实现，需要整个 Knowledge 模块先落地，那是独立的大工作量。「没有就实现」我不打算扩张成"顺手把整个知识库模块也写了"。三个走法：

- **选项 1（推荐）**：一期对话引擎**先不接 RAG**，把 RAG 做成一个 `IRetriever` seam——Agent 无知识库绑定就跳过，有绑定但 Knowledge 未就绪则记日志跳过。对话引擎可独立交付、独立测试；Knowledge 模块作为单独任务推进，就绪后再接上，无需改对话引擎主流程。
- **选项 2**：本期连 Knowledge 模块一起实现（文档上传→分块→embedding→pgvector 检索 + RAG 接入）。范围大幅扩大，对话引擎交付被拖慢。
- **选项 3**：本期只定义 Knowledge 的 RAG Contracts 接口（不实现），对话引擎按接口编程但运行时注入一个返回空的实现。介于 1、2 之间。

> **已定：选项 1。** 对话引擎一期不接 RAG，检索做成 `IRetriever` seam（默认空实现，无知识库绑定即跳过）；Knowledge 模块作为独立任务，就绪后再接，不改对话引擎主流程。
