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

