# 对话引擎：一次流式对话的端到端时序

> 场景：用户向某个 Agent 发送一条消息，前端通过 SSE 收到流式回复；其中可能触发一次 MCP 工具调用。
> 对应模块：`Hify.Modules.Conversation`（L2 编排层）→ `Agent` / `Knowledge` / `ModelProvider` / `Mcp`。

## 总体时序图

```mermaid
sequenceDiagram
    autonumber
    actor U as 用户
    participant FE as 前端<br/>(fetch + ReadableStream)
    participant NG as Nginx<br/>(proxy_buffering off)
    participant EP as Endpoint<br/>(Conversation 接入层)
    participant SVC as SendMessage Feature<br/>(对话引擎核心)
    participant RDS as Redis<br/>(Cache-Aside)
    participant DB as PostgreSQL<br/>(message / config)
    participant KN as Knowledge<br/>(RAG + pgvector)
    participant MP as ModelProvider<br/>(裸 HttpClient 适配)
    participant LLM as LLM API<br/>(OpenAI/Claude/Ollama)
    participant MCP as Mcp 模块<br/>(工具执行)

    U->>FE: 输入并发送
    FE->>NG: POST /api/v1/conversations/{id}/messages<br/>Accept: text/event-stream
    NG->>EP: 反代（关闭 buffering）

    Note over EP: 鉴权 / 入参校验（外部输入不可信）<br/>设 Content-Type: text/event-stream<br/>取 RequestAborted (CancellationToken)
    EP->>SVC: 调用 Feature，移交响应流的"笔" + CT

    rect rgb(238, 246, 255)
    Note over SVC,KN: 阶段 2 — 调模型前的编排准备
    SVC->>RDS: 查 Agent 配置（提示词/模型/工具）
    alt 缓存未命中
        RDS-->>SVC: miss
        SVC->>DB: 查配置并回填 Redis
    end
    SVC->>RDS: 查会话上下文
    SVC->>DB: 按 (conversation_id, created_at) 取最近 N 条历史
    Note over SVC: 按 token 预算裁剪历史
    SVC->>KN: RAG 检索（embedding → HNSW + LIMIT）
    KN-->>SVC: top-k 相关片段
    SVC->>DB: 落库用户消息 (role=user)
    Note over SVC: 装配 messages：[system+RAG, ...历史, user] + tools
    end

    rect rgb(245, 240, 255)
    Note over SVC,LLM: 阶段 3/4 — 调模型 + 流式回传
    SVC->>MP: StreamAsync(messages, tools, CT)
    Note over MP: 选 OpenAI/Claude/Ollama 适配器<br/>熔断 + 舱壁 + 120s 超时
    MP->>LLM: POST stream=true<br/>HttpCompletionOption.ResponseHeadersRead
    loop 每个 token delta
        LLM-->>MP: SSE chunk
        MP-->>SVC: 解析+归一化的 delta (IAsyncEnumerable)
        SVC->>EP: 写 data: {...}\n\n + FlushAsync()
        EP->>NG: 实时透传
        NG->>FE: SSE 帧
        FE->>U: 追加渲染（逐字蹦出）
    end
    end

    rect rgb(240, 250, 240)
    Note over SVC,MCP: 阶段 5 — 工具调用循环（可选）
    alt 模型返回 tool_call
        Note over SVC: 校验工具名/参数（LLM 输出不可信）
        SVC->>MCP: 执行工具
        MCP-->>SVC: 工具结果
        SVC->>SVC: 追加 role=tool 消息，回到阶段 3 再次调模型<br/>（循环至产出最终文本，设最大轮次上限）
    end
    end

    rect rgb(255, 248, 238)
    Note over SVC,FE: 阶段 6 — 收尾
    LLM-->>MP: 流结束 (stop / [DONE])
    SVC->>DB: 落库完整回复 (role=assistant)
    SVC->>RDS: 更新会话上下文
    SVC->>EP: 推 data: [DONE]
    EP->>FE: 结束事件
    FE->>U: 气泡定型，恢复输入框
    end
```

## 异常路径

```mermaid
sequenceDiagram
    autonumber
    actor U as 用户
    participant FE as 前端
    participant EP as Endpoint
    participant SVC as SendMessage Feature
    participant MP as ModelProvider
    participant LLM as LLM API

    alt 用户中途关闭页面
        U->>FE: 关闭 / 断网
        FE-->>EP: 连接断开
        Note over EP: RequestAborted 触发
        EP->>SVC: CancellationToken 取消
        SVC->>MP: 取消传播
        MP->>LLM: 主动断开连接（不再空烧 token）
    end

    alt 首字之前失败（响应头未发出）
        SVC->>MP: StreamAsync
        MP-->>SVC: 连通失败 / 认证失败
        SVC-->>EP: 返回 Result<T> 错误（4xxx Chat 段）
        EP-->>FE: 标准错误信封
    end

    alt 流式中途失败（响应头已发出）
        Note over SVC: 已在流中，无法再返回 Result<T>
        SVC->>EP: 推 event: error / data: {code, message}
        EP->>FE: 错误事件
        FE->>U: 提示失败（已落库的 user 消息不丢）
    end
```

## 关键技术点（流式的"命门"）

整条链路的本质是一个跨 4 层的**背压管道**，没有任何一层"先收全再转发"。任意一层做了缓冲/聚合，流式都会断在那一层：

| 层 | 正确做法 | 错误做法（会断流） |
|---|---|---|
| 前端 | 原生 `fetch` + `ReadableStream` | axios（等响应体完整才 resolve） |
| Nginx | `proxy_buffering off` | 默认 buffering（攒一坨再发） |
| Endpoint | 每帧 `Response.Body` + `FlushAsync()` | 攒完整字符串再写 / 忘记 Flush |
| HttpClient | `HttpCompletionOption.ResponseHeadersRead` | 默认 `ResponseContentRead`（等下载完） |

其它要点：

- **CancellationToken 全程透传**（Endpoint `RequestAborted` → Service → ModelProvider → HttpClient），用户断开即取消对 LLM 的调用。
- **一次用户请求 ≠ 一次 LLM 调用**：工具调用循环让调模型次数运行时才确定，需设最大轮次上限。
- **超时分级**：SSE 流式 120s、同步 60s、连通性测试 10s；每提供商独立熔断器 + 舱壁隔离。
- **LLM 输出不可信**：工具名/参数落库或执行前必须校验。
