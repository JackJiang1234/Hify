# CLAUDE.md

## 项目概述

### 产品定位

Hify 是简版 AI Agent 开发平台（参考 Dify），可本地部署，面向团队内部小规模使用（20-50 人同时在线）。

### 做什么

- 多模型提供商管理（OpenAI、Claude、Ollama）
- Agent 创建与配置（选模型、绑工具、设系统提示词）
- 对话引擎（流式响应、多轮对话、上下文管理）
- 知识库 + RAG（一期只支持 TXT 文档，固定长度分块）
- 简版工作流（JSON 配置，线性 + 条件分支，不做可视化拖拽）
- MCP 工具接入（Agent 通过 MCP 协议调用外部工具）
- 管理控制台（模型管理、Agent 配置、对话界面）

### 不做什么

- 不做可视化工作流拖拽编排
- 不做多租户 / 权限体系
- 不做插件市场、计费系统
- 不做文本生成应用、WebApp 发布、嵌入组件
- 不做标注与微调

### 技术栈

- **后端**：.NET 10 + ASP.NET Core 10 + EF Core 10.0 + PostgreSQL 18.x + pgvector + Redis 7.x
- **前端**：Vue 3 + TypeScript + Vite + Element Plus + Pinia + vue-router；前后端分离，独立构建部署；包管理器 pnpm
- **容器化**：Docker + Docker Compose

### 部署与运维预期

- Docker Compose 本地一键部署
- 目标 20-50 人同时在线，峰值 3-5 QPS，瓶颈在 LLM 长连接
- 缓存：Redis Cache-Aside（配置信息 + 会话上下文）
- 监控：起步 HealthChecks + 日志，后期 Prometheus + Grafana

## 架构决策

### 后端代码组织：模块化单体 + 垂直切片

```
Hify.sln
├── src/
│   ├── Hify.Host/          # 启动项目：DI 组装、中间件、路由聚合、Swagger
│   ├── Hify.Shared/        # 共享内核：Result<T>、分页、领域事件、EF 基类、Redis/异常封装
│   ├── Hify.Contracts/     # 模块对外公开的接口与 DTO（模块间唯一可见，用来打断循环依赖）
│   └── Modules/
│       ├── Hify.Modules.ModelProvider/   # 模型提供商管理（OpenAI/Claude/Ollama 适配）
│       ├── Hify.Modules.Agent/           # Agent 创建与配置
│       ├── Hify.Modules.Conversation/    # 对话引擎：流式、多轮、上下文
│       ├── Hify.Modules.Knowledge/       # 知识库 + RAG
│       ├── Hify.Modules.Workflow/        # 简版工作流（JSON 配置执行）
│       └── Hify.Modules.Mcp/             # MCP 工具接入
└── tests/
```

模块内部垂直切片：`*Module.cs`（唯一 public 注册入口）+ `Endpoints/` + `Features/`（命令+处理+校验聚在一起）+ `Domain/`（internal）+ `Persistence/`（独立 DbContext / 独立 schema）。

### 模块依赖方向与防循环规则（强制）

分层，依赖只能从上往下指，绝不反向：

- **L0 基础能力**：ModelProvider、Mcp（纯叶子，不依赖任何业务模块）
- **L1 领域能力**：Knowledge（→ ModelProvider 算 embedding）、Agent（纯配置存储，只存引用 ID）
- **L2 编排层**：Conversation（→ Agent/ModelProvider/Knowledge/Mcp）、Workflow（→ Agent/ModelProvider/Mcp）

单向依赖、不循环；共用逻辑提取到 Hify.Contracts。

### 前端代码组织：特性切片

前端独立项目，放在与 `src/`、`tests/` 平级的 `web/` 目录（独立 npm 项目，不挂进 .NET solution）。

```
web/
├── package.json / vite.config.ts / tsconfig*.json   # pnpm + Vite + 严格 TS
├── .env / .env.development / .env.production         # VITE_API_BASE_URL 等
├── eslint.config.ts / .prettierrc.json              # 前端编码基线（对应后端 .editorconfig）
├── Dockerfile / nginx.conf                          # Nginx 托管静态产物 + 反代 /api
└── src/
    ├── main.ts / App.vue
    ├── api/            # HTTP 层：client.ts(拦截器拆 Result<T>) + types.ts + 按模块分文件
    ├── features/       # 业务特性切片，与后端 6 模块一一对应
    │   └── {feature}/  #   views/(路由页) + components/ + composables/ + store.ts + types.ts
    ├── components/      # 跨特性通用组件（layout/ 控制台外壳等）
    ├── composables/     # 全局通用 hook（useSse 流式、useTable 分页等）
    ├── stores/          # 全局 Pinia store
    ├── router/          # 路由聚合（各 feature 路由在此挂载）
    ├── constants/       # 错误码等常量（与后端分段对齐）
    ├── utils/ / styles/ / assets/
```

`features/` 下特性对应后端模块：provider、agent、conversation、knowledge、workflow、mcp。

### 前端约定（强制）

- **与后端契约对齐**：`api/client.ts` 拦截器统一拆 `Result<T>` / `PageResult<T>`——`code===200` 取 `data`，否则抛 `ApiError` 并提示。分页见「接口规范」。
- **错误码**：`constants/error-code.ts` 与后端四位分段一一对应，优先展示后端 `message`，本地仅兜底。
- **空值渲染**：列表当 `[]`、字符串当 `""`、对象可能 `null`，TS 类型据此标注。
- **SSE 流式**：用原生 `fetch` + `ReadableStream`（不走 axios），120s 超时；Nginx 反代须关 buffering。axios 同步调用 60s 超时。
- **命名**：组件文件 PascalCase（`ProviderListView.vue`）、composable 以 `use` 开头、其余 TS 文件 kebab-case。
- **Element Plus 按需自动导入**（unplugin-auto-import + unplugin-vue-components），组件无需手动 import。

### LLM 外部调用容错

- 每个提供商熔断器 + 舱壁隔离
- 超时：同步调用 60s、SSE 流式 120s、连通性测试 10s
- 重试按异常类型区分：网络抖动重试、认证失败不重试、限流退避重试

### 数据库规范（强制，PostgreSQL 18 + pgvector）

通用字段：

- **主键** `bigint GENERATED ALWAYS AS IDENTITY`，**禁用 uuid**（含 uuidv7）；将来分片改 snowflake 风格 bigint，列类型不变
- **禁用 NULL**：所有列 `NOT NULL` + `DEFAULT`，字符串空值 `''`、数值/引用空值 `0`
- **软删除** `deleted_at bigint NOT NULL DEFAULT 0`（0=未删，否则=删除时刻 epoch ms），部分索引用 `WHERE deleted_at = 0`
- **金额 / Token 用量** 用 `bigint` 存最小精度，**禁用 DECIMAL**
- **枚举字段** `varchar(32) NOT NULL DEFAULT ''`，**禁用原生 ENUM**

索引与关系：

- 命名 `idx_{表名}_{字段名}`；逻辑删除字段必须进组合索引
- 组合索引等值列在前、范围列在后；多对多关联表两个方向都要索引
- 唯一约束用 UNIQUE INDEX，不只在代码层校验
- 禁止在 TEXT/BLOB 建索引；不建数据库级外键，应用层维护

分页：

- 默认游标分页（`WHERE id < lastId ORDER BY id DESC LIMIT N`）
- OFFSET 分页最大 10000 条；COUNT 只在第一页查，翻页不重复查

大表预判：

- message 增长最快，必须建 `(conversation_id, created_at)` 索引
- document/chunk：关系数据存 PostgreSQL，向量存 pgvector

pgvector：维度固定 1536，必须建 HNSW 索引，检索必须加 LIMIT、禁止全量排序。

## 接口规范

RESTful 风格 `/api/v1/{资源复数名}`，非 CRUD 操作用动词（如 `POST /api/v1/providers/{id}/test-connection`）。

- **统一响应** `Result<T>`：`{ "code": 200, "message": "success", "data": {...} }`
- **分页** 请求 `page`（从 1 开始）、`size`（默认 20，最大 100）；响应 `PageResult<T>`（继承 `Result<IReadOnlyList<T>>`，data 即当前页列表）额外含 `total`、`page`、`size`
- **空值** 列表空返回 `[]`、字符串空返回 `""`、对象不存在返回 `null`
- **错误码** 四位数字按模块分段：1xxx 通用 | 2xxx Provider | 3xxx Agent | 4xxx Chat | 5xxx MCP | 6xxx Workflow | 7xxx Knowledge

## 编码规范（C#，基于微软官方约定）

基线：微软 C# Coding Conventions + Framework Design Guidelines + .NET runtime 风格。强制方式：根目录 `.editorconfig` + 分析器，`Nullable=enable`、`ImplicitUsings=enable`、`TreatWarningsAsErrors=true`。

### 命名

- 类型/方法/属性/事件/常量/枚举成员/命名空间：PascalCase
- 局部变量/参数：camelCase；private/protected 字段：`_camelCase`（含只读字段）
- 接口加 `I` 前缀（`IModelProvider`）；泛型参数加 `T` 前缀（`TResult`）；异步方法加 `Async` 后缀
- 缩写按普通词大小写（`HttpClient`、`userId`）；禁匈牙利命名、禁单字母变量（循环/lambda 短参除外）

### 文件与组织

- 一个文件一个顶层类型，文件名=类型名；文件级命名空间（不用大括号块）
- 命名空间与目录一致，前缀 `Hify.Modules.{模块}.{切片}`
- using 放文件顶部、命名空间之外，`System.*` 在前
- 成员顺序：常量→字段→构造函数→属性→方法→嵌套类型；同类 public→internal→protected→private

### 格式

- Allman 大括号；所有控制语句必须带大括号，禁单行无括号 if
- 4 空格缩进，禁 Tab；行宽 ≤ 120；文件以单个换行结尾，连续空行最多 1 行

### 类型与封装

- 默认 internal；只有 `*Module.cs` 入口和 Hify.Contracts 里的接口/DTO 才 public
- 非继承类加 sealed；字段默认 `private readonly`，对外用属性，不暴露 public 字段
- DTO/值对象用 record（init 访问器，不可变），实体用 class
- 依赖注入用主构造函数，构造函数只赋值、不放 I/O

### 语言特性

- 右侧类型明显时用 var，否则显式类型；用目标类型 `new()`、集合表达式 `[]`、模式匹配/switch 表达式
- 字符串用内插 `$"..."`；引用成员名用 `nameof`
- 多返回值用元组或 record，不堆 out；禁 `#region`、禁 goto、禁 dynamic（确需对接无类型外部数据就地说明）

### 可空性（NRT 全程开启）

- 不得 `#nullable disable`，不滥用 `!` 抑制符（确需就地注释原因）
- 可能为空显式 `T?` 并处理；不可空引用类型不得返回/赋 null
- 入参校验用 `ArgumentNullException.ThrowIfNull(x)`
- 此为 C# 引用可空性，与数据库「禁 NULL」互不冲突

### 异步

- I/O 一律 async，返回 `Task/Task<T>` 加 `Async` 后缀；禁 `.Result`/`.Wait()`/`.GetAwaiter().GetResult()`、禁 sync-over-async
- 禁 async void（事件处理器除外）
- `CancellationToken` 逐层透传到所有 async 方法（含 EF、HttpClient、SSE），作最后一个参数
- 库代码 await 加 `ConfigureAwait(false)`；默认用 Task，热路径多同步完成才用 ValueTask

### 异常与错误（对齐 Result<T>）

- 可预期业务失败（校验/不存在/外部拒绝）返回 `Result<T>`，不抛异常；异常只用于真正异常情况
- 禁裸 `catch {}` 吞异常；重抛用 `throw;` 不用 `throw ex;`
- 捕获具体异常类型，不笼统 `catch (Exception)`（全局处理中间件除外）
- 抛框架内置异常类型，消息带上下文、不含敏感数据

### LINQ 与集合

- 避免多次枚举 `IEnumerable`，需多次先 `ToList()`；判空用 `Any()` 不用 `Count() > 0`
- 对外返回 `IReadOnlyList<T>`/`IReadOnlyCollection<T>`，不返回可变 `List<T>`

### EF Core

- 查询全 async 并传 `CancellationToken`；只读查询加 `AsNoTracking()`，只取所需列用 `Select` 投影
- 禁惰性加载，关联用 `Include` 或投影
- 遵循数据库规范（bigint 主键、软删 `deleted_at=0`、游标分页、应用层维护外键）
- 每模块独立 DbContext / 独立 schema

### 注释与安全

- Hify.Contracts 的 public 接口/DTO 必须有 XML 文档注释，internal 按需
- 注释解释为什么、不复述代码；过时注释即删；TODO 带责任范围
- 禁硬编码密钥/连接串/Token/内部主机名，走配置+Secret，示例用占位符
- 外部输入（API 入参、LLM 输出、MCP 返回）一律不可信，落库/执行前校验
- SQL 全参数化（EF 默认满足），禁字符串拼 SQL
- 日志/异常消息不输出 PII、凭证、完整提示词

## 开发规范与行为指令

### 简单性原则（Simplicity First）

「少即是多」：绝不过度抽象，绝不引入非必需依赖。

- 组合优于继承；每个类/接口单一职责
- 每个方法只做一件事，命名清晰，长度建议 20-50 行
- 用最简单直接的方式实现，不引入不必要的设计模式（除非我明确要求）
- 不引入技术栈以外的依赖，需要时先问我
- 所有外部调用必须设超时；配置项外化到 `appsettings.json`，不硬编码

### 测试先行（Test-First，不可协商）

- 新功能/Bug 修复都从一个失败的测试开始，严格 Red-Green-Refactor
- 单元测试优先表格驱动（Table-Driven Tests）
- 优先集成测试、用真实依赖，拒绝过度 Mock

### 明确性原则（Clarity）

- **错误处理不可协商**：所有错误显式处理，传递时用领域自定义异常包装
- 绝不用全局变量传递状态，依赖通过接口解耦

### 改代码时

- 先理解相关模块的设计意图；不为新功能破坏已有接口契约；改完确保已有测试通过

### 不确定时

- 架构选择给我 2-3 个方案对比，我来拍板
- 规范没覆盖的情况先问我，不要自己编规矩
