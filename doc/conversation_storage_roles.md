# 对话引擎的存储分工：PostgreSQL / Redis / pgvector

> 适用模块：`Hify.Modules.Conversation`（对话引擎）。
> 相关文档：[conversation_streaming_sequence.md](conversation_streaming_sequence.md)（端到端时序）、[conversation_context_strategies.md](conversation_context_strategies.md)（上下文裁剪策略）。

## 重要澄清：Hify 没有独立向量数据库

Hify 用 **PostgreSQL 18 + pgvector 扩展**，向量与关系数据存在**同一个 Postgres** 里（`knowledge.chunk.embedding vector(1536)`）。
所以是"**两套存储（PG + Redis）、三种角色**"，而非三套独立组件。不引入独立向量库，少一个运维组件，符合简单性原则。

## 一句话定位

| 存储 | 角色 | 一句话 |
|---|---|---|
| **PostgreSQL** | 对话历史的唯一事实来源（source of truth） | 持久、可靠、可查；丢了 Redis 也能恢复 |
| **Redis** | 热会话上下文的缓存 + 加速层 | 快、临时；挂了不丢数据，只是变慢 |
| **pgvector**（在 PG 内） | 语义检索（RAG / 可选的历史检索） | 按"意思相近"找内容，不是按 ID/时间找 |

对话历史本身存在 PostgreSQL（`conversation.conversation` + `conversation.message`），这是权威副本；Redis 只是它的缓存。

## 三者各自扮演什么

### PostgreSQL —— 事实来源

- **存什么**：每条消息（`message` 表）、会话元数据（`conversation` 表）、token 用量、工具调用记录、状态。
- **为什么必须有它**：
  - 持久：服务/Redis 重启都不丢历史（Redis 是内存型，默认不保证持久）。
  - 可查询/可审计：翻历史、统计 token、按时间分页，靠 `(conversation_id, created_at)` 索引在 PG 上做。
  - 唯一权威：Redis 与 PG 不一致时以 PG 为准。
- **怎么用**：每轮对话结束，新消息**落库到 PG**（不可省）。

### Redis —— 缓存与加速（Cache-Aside）

- **存什么**：热会话的上下文（最近活跃对话、已裁剪好的消息序列）、Agent 配置等热点只读数据。
- **为什么要它**：
  - 省往返：每轮都要读历史拼上下文，若每次查 PG + 重新裁剪，QPS 上来后 PG 压力大、延迟高；Redis 命中直接拿到拼好的上下文。
  - 天然适配会话：用 TTL 让冷会话自动过期、释放内存。
- **怎么用（Cache-Aside）**：
  ```
  读: 先查 Redis → 命中直接用
              → 未命中 → 查 PG → 回填 Redis(带 TTL) → 用
  写: 新消息先写 PG(事实来源) → 再更新/失效 Redis
  ```
- **关键原则**：Redis 可随时清空、可挂——挂了只退化成每次查 PG（慢一点），**绝不能因 Redis 丢数据就丢对话历史**。"只把 Redis 当历史存储、不落 PG"是错的。

### pgvector —— 语义检索

- 干的是另一类活：PG/Redis 按 id、conversation_id、时间这种"精确键"查；pgvector 按"语义相近"查（给一段文字的 embedding，找最相似的若干条）。
- **两处用途**：
  1. **RAG（主用途，一期就有）**：检索知识库 `knowledge.chunk`，把相关片段注入上下文；走 HNSW 索引 + LIMIT。
  2. **历史向量检索（可选，二期）**：把历史消息也 embedding，按相关性召回很久以前的相关对话。一期不做。
- 注意：它解决"找相关内容"，**不负责存对话历史的事实**；历史权威仍在 PG 的关系表里。

## 一次对话里三者怎么配合

```
用户发消息
  │
  ├─ 读 Agent 配置 ──────→ Redis(命中) / PG(未命中回填)
  ├─ 读会话历史拼上下文 ──→ Redis(命中) / PG 按索引查 + 裁剪 + 回填
  ├─ RAG 检索相关知识 ───→ pgvector (HNSW + LIMIT)
  │
  ├─ 调 LLM ...流式回复...
  │
  └─ 收尾:
       新消息 ──写入──→ PostgreSQL(事实来源, 必做)
                  └──→ 更新 Redis 会话上下文(刷新 TTL)
```

## 给 Hify 的落地要点

1. **历史的家是 PostgreSQL，Redis 只是快取**——不能含糊。
2. **Cache-Aside**：写穿 PG 再更 Redis；读 Redis 优先、未命中回填。
3. **Redis 存"裁剪好的"上下文**而非全量历史，省得每轮重算（配合滑动窗口策略）。
4. **TTL 管理冷会话**，避免 Redis 内存无限涨。
5. **不引入独立向量库**：pgvector 在同一个 PG 里就够 MVP。

## 一句话总结

> 对话历史的事实来源是 PostgreSQL（持久、可查、权威）；Redis 是它的 Cache-Aside 缓存，存裁剪好的热会话上下文来省往返、用 TTL 管冷会话，挂了只变慢不丢数据；pgvector（在同一个 PG 里，不是独立向量库）负责语义检索——一期给 RAG 用，历史向量检索是二期可选项。
