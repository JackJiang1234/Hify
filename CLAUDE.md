# CLAUDE.md

## 项目概述

### 产品定位

Hify 是一个简版的 AI Agent 开发平台（参考 Dify），可本地部署，
面向团队内部小规模使用（20-50 人同时在线）。

### 做什么

- 多模型提供商管理（OpenAI、Claude、Gemini、Ollama）
- Agent 创建与配置（选模型、绑工具、设系统提示词）
- 对话引擎（流式响应、多轮对话、上下文管理）
- 知识库 + RAG（一期只支持 TXT 文档，固定长度分块）
- 简版工作流（JSON 配置，线性 + 条件分支，不做可视化拖拽）
- MCP 工具接入（Agent 可通过 MCP 协议调用外部工具）
- 管理控制台（模型管理、Agent 配置、对话界面）

### 不做什么

- 不做可视化工作流拖拽编排
- 不做多租户 / 权限体系
- 不做插件市场、计费系统
- 不做文本生成应用、WebApp 发布、嵌入组件
- 不做标注与微调

### 技术栈

- **后端**: .NET 10 + ASP.NET Core 10 + EF Core 10.0 + PostgreSQL 18.x + pgvector + Redis 7.x。
- **前端**:Vue 3 + TypeScript + Element Plus。
- **容器化**:Docker + Docker Compose。

### 部署与运维预期

- Docker Compose 本地一键部署
- 目标：20-50 人同时在线，峰值 3-5 QPS，瓶颈在 LLM 长连接
- 缓存：Redis Cache-Aside（配置信息 + 会话上下文）
- 监控：起步HealthChecks + 日志，后期 Prometheus + Grafana

## 架构决策

### 代码组织：模块化单体（Modular Monolith）+ 垂直切片

目录结构：

```
Hify.sln
├── src/
│   ├── Hify.Host/          # 启动项目：DI 组装、中间件、路由聚合、Swagger
│   ├── Hify.Shared/        # 共享内核：Result<T>、分页、领域事件、EF 基类、Redis/异常封装
│   ├── Hify.Contracts/     # 各模块对外公开的接口与 DTO（模块间唯一可见的东西，用来打断循环依赖）
│   └── Modules/
│       ├── Hify.Modules.ModelProvider/   # 模型提供商管理（OpenAI/Claude/Gemini/Ollama 适配）
│       ├── Hify.Modules.Agent/           # Agent 创建与配置
│       ├── Hify.Modules.Conversation/    # 对话引擎：流式、多轮、上下文
│       ├── Hify.Modules.Knowledge/       # 知识库 + RAG
│       ├── Hify.Modules.Workflow/        # 简版工作流（JSON 配置执行）
│       └── Hify.Modules.Mcp/             # MCP 工具接入
└── tests/
```

模块内部仍是垂直切片：`*Module.cs`（唯一 public 注册入口）+ `Endpoints/` + `Features/`（命令+处理+校验聚在一起）

- `Domain/`（internal）+ `Persistence/`（独立 DbContext / 独立 schema）。

### 模块依赖方向与防循环规则（强制）

分层（依赖只能从上往下指，绝不反向）：

- **L0 基础能力**：ModelProvider、Mcp（纯叶子，不依赖任何业务模块）
- **L1 领域能力**：Knowledge（→ ModelProvider 算 embedding）、Agent（纯配置存储，只存引用 ID）
- **L2 编排层**：Conversation（→ Agent/ModelProvider/Knowledge/Mcp）、Workflow（→ Agent/ModelProvider/Mcp）

依赖原则

- 单向依赖，不循环。共用逻辑提取Hify.Contracts。

### LLM 外部调用的容错方案

- **每个提供商熔断器+舱壁隔离**
- **超时**：同步调用 60s 超时，SSE 流式 120s 超时，连通性测试 10s
- **重试**： 按异常类型区分重试：网络抖动重试、认证失败不重试、限流退避重试。

### 数据库规范（强制，PostgreSQL 18 + pgvector）

通用字段约定：

- **主键**：`bigint GENERATED ALWAYS AS IDENTITY`。**禁用 uuid**（含 uuidv7）。自增 bigint 单调，索引局部性好；将来分片改 snowflake 风格 bigint，列类型不变。
- **禁用 NULL**：所有列 `NOT NULL` + `DEFAULT`。字符串空值用 `''`，数值/引用空值用 `0`。
- **软删除**：`deleted_at bigint NOT NULL DEFAULT 0`（0=未删，否则=删除时刻 epoch ms），不用可空 timestamptz。部分索引用 `WHERE deleted_at = 0`。
- **金额 / Token 用量**：`bigint` 存最小精度，**禁用 DECIMAL**。
- **枚举字段**：`varchar(32) NOT NULL DEFAULT ''`，**禁用原生 ENUM**（加值要改表）。

索引与关系：

- 命名 idx_{表名}_{字段名}
- 逻辑删除字段必须加进组合索引
- 组合索引等值列在前，范围列在后
- 多对多关联表两个方向都要索引
- 唯一约束用 UNIQUE INDEX，不只在代码层校验
- 禁止在 TEXT/BLOB 字段建索引
- 不建数据库级外键约束，应用层维护

分页规则：

- 默认用游标分页（WHERE id < lastId ORDER BY id DESC LIMIT N）
- OFFSET 分页限制最大 10000 条
- COUNT 只在第一页查，翻页不重复查

大表预判：

- message：增长最快，必须建 (conversation_id, created_at) 索引
- document_chunk：MySQL 只存元数据，向量存 pgvector

pgvector 规范：

- 向量表建在 PostgreSQL，维度固定 1536
- 必须建 HNSW 索引
- 检索必须加 LIMIT，禁止全量排序

## 接口规范

### 路径

RESTful 风格：/api/v1/{资源复数名}
GET    /api/v1/providers          # 列表（分页）
POST   /api/v1/providers          # 创建
GET    /api/v1/providers/{id}     # 详情
PUT    /api/v1/providers/{id}     # 更新
DELETE /api/v1/providers/{id}     # 删除
POST   /api/v1/providers/{id}/test-connection  # 非 CRUD 操作用动词

### 统一响应

所有接口返回 Result<T>：
{ "code": 200, "message": "success", "data": {...} }

### 分页

请求：page（从 1 开始）、pageSize（默认 20，最大 100）
响应：Result<PageResult<T>>，PageResult 包含 list、total、page、pageSize

### 空值

- 列表字段空时返回 []，不返回 null
- 字符串字段空时返回 ""，不返回 null
- 对象不存在时返回 null

### 错误码

四位数字，按模块分段：
1000-1999 通用 | 2000-2999 Provider | 3000-3999 Agent
4000-4999 Chat | 5000-5999 MCP | 6000-6999 Workflow | 7000-7999 Knowledge

## 行为指令

### 写代码时

- 每个功能用最简单直接的方式实现
- 不引入不必要的设计模式，除非我明确要求
- 不做过度抽象
- 不引入技术栈以外的依赖，需要时先问我
- 所有外部调用必须有超时设置
- 配置项外化到appsettings.json，不硬编码

### 改代码时

- 先理解相关模块的设计意图
- 不要为了新功能破坏已有接口契约
- 改完确保已有测试通过

### 不确定时

- 架构选择给我 2-3 个方案对比，我来拍板
- 规范没覆盖的情况，先问我，不要自己编规矩

