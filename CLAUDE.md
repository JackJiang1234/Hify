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
- document/chunk：关系数据存 PostgreSQL，向量存 pgvector

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

请求：page（从 1 开始）、size（默认 20，最大 100）
响应：PageResult<T>（继承 Result<IReadOnlyList<T>>，data 即当前页列表），额外包含 total、page、size

### 空值

- 列表字段空时返回 []，不返回 null
- 字符串字段空时返回 ""，不返回 null
- 对象不存在时返回 null

### 错误码

四位数字，按模块分段：
1000-1999 通用 | 2000-2999 Provider | 3000-3999 Agent
4000-4999 Chat | 5000-5999 MCP | 6000-6999 Workflow | 7000-7999 Knowledge

## 编码规范（C#，基于微软官方约定）

基线：微软 C# Coding Conventions + Framework Design Guidelines + .NET runtime 风格。
强制方式：根目录 .editorconfig + 分析器，`Nullable=enable`、`ImplicitUsings=enable`、`TreatWarningsAsErrors=true`。

### 命名

- 类型/方法/属性/事件/常量/枚举成员/命名空间：PascalCase
- 局部变量/参数：camelCase；private/protected 字段：_camelCase（含只读字段）
- 接口加 I 前缀（IModelProvider）；泛型参数加 T 前缀（TResult）
- 异步方法加 Async 后缀（SendMessageAsync）
- 缩写按普通词大小写（HttpClient、userId），禁匈牙利命名、禁单字母变量（循环/lambda 短参除外）

### 文件与组织

- 一个文件一个顶层类型，文件名=类型名
- 文件级命名空间（namespace Hify.Modules.Agent;），不用大括号块
- 命名空间与目录一致，前缀 Hify.Modules.{模块}.{切片}
- using 放文件顶部、命名空间之外，System.* 在前
- 成员顺序：常量→字段→构造函数→属性→方法→嵌套类型；同类 public→internal→protected→private

### 格式

- Allman 大括号（左括号另起一行）
- 所有控制语句必须带大括号，禁单行无括号 if
- 4 空格缩进，禁 Tab；行宽 ≤ 120
- 文件以单个换行结尾，连续空行最多 1 行

### 类型与封装（对齐模块化单体）

- 默认 internal；只有 *Module.cs 入口和 Hify.Contracts 里的接口/DTO 才 public
- 非继承类加 sealed
- 字段默认 private readonly，对外用属性，不暴露 public 字段
- DTO/值对象用 record（init 访问器，不可变）；实体用 class
- 依赖注入用主构造函数；构造函数只赋值，不放 I/O

### 语言特性

- 右侧类型明显时用 var，否则显式类型
- 用目标类型 new()、集合表达式 []、模式匹配/switch 表达式
- 字符串用内插 $"..."；引用成员名用 nameof，禁硬编码名字
- 多返回值用元组或 record，不堆 out
- 禁 #region、禁 goto、禁 dynamic（确需对接无类型外部数据时就地说明）

### 可空性（NRT 全程开启）

- 不得 #nullable disable，不滥用 ! 抑制符（确需就地注释原因）
- 可能为空显式 T? 并处理；不可空引用类型不得返回/赋 null
- 入参校验用 ArgumentNullException.ThrowIfNull(x)
- 此为 C# 引用可空性，与数据库"禁 NULL"互不冲突

### 异步

- I/O 一律 async，返回 Task/Task<T>，加 Async 后缀
- 禁 .Result/.Wait()/.GetAwaiter().GetResult()，禁 sync-over-async
- 禁 async void（事件处理器除外）
- CancellationToken 逐层透传到所有 async 方法（含 EF、HttpClient、SSE），作最后一个参数
- 库代码 await 加 ConfigureAwait(false)；默认用 Task，热路径多同步完成才用 ValueTask

### 异常与错误（对齐 Result<T>）

- 可预期业务失败（校验/不存在/外部拒绝）返回 Result<T>，不抛异常
- 异常只用于真正异常情况（编程错误/不可恢复）
- 禁裸 catch {} 吞异常；重抛用 throw; 不用 throw ex;
- 捕获具体异常类型，不笼统 catch (Exception)（全局处理中间件除外）
- 抛框架内置异常类型，消息带上下文、不含敏感数据

### LINQ 与集合

- 避免多次枚举 IEnumerable，需多次先 ToList()
- 判空用 Any()，不用 Count() > 0
- 对外返回 IReadOnlyList<T>/IReadOnlyCollection<T>，不返回可变 List<T>

### EF Core

- 查询全 async 并传 CancellationToken
- 只读查询加 AsNoTracking()，只取所需列用 Select 投影
- 禁惰性加载，关联用 Include 或投影
- 遵循数据库规范（bigint 主键、软删 deleted_at=0、游标分页、应用层维护外键）
- 每模块独立 DbContext / 独立 schema

### 注释与安全

- Hify.Contracts 的 public 接口/DTO 必须有 XML 文档注释；internal 按需
- 注释解释为什么，不复述代码；过时注释即删；TODO 带责任范围
- 禁硬编码密钥/连接串/Token/内部主机名，走配置+Secret，示例用占位符
- 外部输入（API 入参、LLM 输出、MCP 返回）一律不可信，落库/执行前校验
- SQL 全参数化（EF 默认满足），禁字符串拼 SQL
- 日志/异常消息不输出 PII、凭证、完整提示词

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

## 开发规范

### 第一条：简单性原则 (Simplicity First)

**核心：** 遵循“少即是多”哲学。绝不进行不必要的抽象，绝不引入非必需的依赖。

- **1.1 (反过度工程):** 组合优于继承
- **1.2 (核心设计原则):** 每个类或接口保持单一职责，
- **1.2 (方法实现原则):** 类的一个方法只做一件事，命名清晰，长度适中，建议20-50行以内

### 第二条：测试先行铁律 (Test-First Imperative) - 不可协商

**核心：** 所有新功能或Bug修复，都必须从编写一个（或多个）失败的测试开始。

- **2.1 (TDD循环):** 严格遵循“Red-Green-Refactor”循环。
- **2.2 (表格驱动):** 单元测试必须优先采用表格驱动测试（Table-Driven Tests）的风格。
- **2.3 (拒绝Mocks):** 优先编写集成测试，使用真实的依赖。

### 第三条：明确性原则 (Clarity and Explicitness)

**核心：** 代码的首要目的是让人类易于理解。

- **3.1 (错误处理):** **不可协商**：所有错误都必须被显式处理。错误传递时必须使用特定的领域自定义异常进行包装。
- **3.2 (无全局变量):** 绝不允许使用全局变量来传递状态，所有依赖必须通过接口解藕。
