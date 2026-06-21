---
name: module-delivery
description: >-
  Hify（模块化单体 .NET 10 + ASP.NET Core + EF Core + PostgreSQL/pgvector + Redis，前端 Vue3/TS/Element Plus）中，
  端到端交付一个垂直业务模块（设计 → 数据模型 → 持久化 → 契约 → 安全 → 外部适配 → 功能 → API → 部署 → 前端）的标准流程。
  当用户要开发/交付一个新模块（Agent / Conversation / Knowledge / Workflow / Mcp 等）、或为现有模块补齐某一层时使用。
  含每步「产出物 + 验证方式」，关键技术取舍标注「⏸ 等待用户确认」，并附实战踩坑注意事项。
---

# 垂直模块端到端交付（Hify）

源自 ModelProvider 模块的实战流程。目标：每一步都有**明确产出物**与**可执行的验证**，关键岔路**先和用户确认再动手**。

## 适用前提

- 架构：模块化单体 + 垂直切片；分层依赖只能从上往下（L0 基础 → L1 领域 → L2 编排），共用逻辑提到 `Hify.Contracts`。
- 强约束（不可协商，细节见 `CLAUDE.md`）：测试先行（Red-Green）、可预期失败返回 `Result<T>` 不抛异常、DB 规范（bigint 主键 / 禁 NULL / 软删 `deleted_at=0` / varchar 枚举 / 手写 DDL）、默认 `internal`（仅 `*Module.cs` 与 Contracts 才 public）、`TreatWarningsAsErrors`。

## 全程铁律（每个节点都要满足）

1. 每个可验证节点：`dotnet build Hify.sln` **0 警告 0 错误**。
2. 测试先行；集成测试用**真实依赖**（PostgreSQL/Redis），连不上则**静默跳过**（早返回），绝不 Mock 掉真实行为。
3. 真实库验证完务必 `docker compose down -v` 清理，不留容器/卷。
4. **文件工具一律用绝对路径**（见踩坑 #1）。

---

## 阶段 0 — 设计与决策（动手写代码前）

**产出物**：技术选型对比、数据模型草案、分阶段任务清单。
**验证**：用户 review 通过，决策点全部敲定。
**⏸ 等待用户确认**：
- 外部集成策略（例：用官方 SDK vs 裸 `HttpClient` 自实现）——给 2~3 个方案对比利弊，用户拍板。
- 一期范围（哪些子能力做、哪些砍/推迟）。
- 关键技术取舍（迁移方式、是否引入新依赖等）。

> 注意：**先勘察既有产出物再设计**——`ddl.sql`、`appsettings.json`、已有脚手架工程、`CLAUDE.md`、项目记忆。旧 DDL/配置常与新设计冲突（我们遇到旧 `model_provider` 表里还带 Gemini、明文 `api_key`、行内 health），要按新设计重写而非叠加。

### 0a. 数据模型
**产出物**：表结构（全表遵循 DB 规范）。
**⏸ 等待用户确认**：高频易变状态（如健康）是否独立成表；`jsonb` 兜底 vs 具名列；密钥是否拆独立表。
> 注意：把**稳定可缓存的配置**与**高频写的运行时状态**分到不同表（provider 配置表 ↔ provider_health 1:1），探活频繁 UPDATE 不会反复触动配置行、不与缓存争锁。

### 0b. 把关键决策写入项目记忆
**产出物**：`memory/<slug>.md`（type: project）+ `MEMORY.md` 一行索引。
**为何**：跨会话延续，避免下次重新拍板。

### 0c. 任务清单（里程碑制）
**产出物**：P0–P8 任务，每条标注产出物、验证方式，并用 `[BLOCKS]`（不做就崩）/`[TUNE]`（优化项）区分。
**⏸ 等待用户确认**：清单整体 + 范围岔口（例：某能力本期做还是推迟）。

---

## 阶段 1 — 持久化层

**产出物**：实体（`internal`，继承 `EntityBase`，`Domain/`）；模块独立 `DbContext`（独立 schema、snake_case、软删全局过滤、jsonb 映射、列长）；**手写、版本化、幂等的 DDL**（并入根 `ddl.sql`）。
**验证**：
- 离线映射单测（**不连库**）：断言表落在本模块 schema、列名 snake_case、jsonb 列类型等。
- 真实 PG 集成测试：软删过滤、唯一/部分唯一约束、游标分页。
**⏸ 等待用户确认**：DDL 方式（手写 SQL vs EF Migrations）。
> 注意：
> - DDL 手写 → `DbContext` **禁用 Migrations**，只做映射。
> - 枚举建议用 `string` + 常量类（值小写，如 `openai`），别用 C# enum——全局约定的 `EnumToStringConverter` 存的是成员名（`OpenAi`），与 DDL 小写值不符。
> - EF Core 10 部分元数据 API（如查询过滤器）可能 `[Obsolete]`→在 `TreatWarningsAsErrors` 下会编译失败；这类断言改用**真实库行为测试**，别靠反射断言。

### 1a. 本地依赖基建（compose）
**产出物**：`docker-compose.yml`（PostgreSQL 18 + pgvector、Redis 7；首次初始化自动跑 `ddl.sql`）。
**验证**：`DB_PORT=5433 docker compose up -d db` → healthy → 用 `HIFY_TEST_DB` 指向它跑集成测试全绿 → `down -v`。
> 注意（都踩过）：
> - ⚠️ **PG18 镜像数据卷必须挂 `/var/lib/postgresql`**（不是 `/var/lib/postgresql/data`），否则容器启动即退出。
> - ⚠️ **宿主端口做成可覆盖**（`${DB_PORT:-5432}` / `${REDIS_PORT:-6379}`）——本机常已有 PG 占用 5432。
> - 重建前先 `docker compose down -v`，否则残留的 Created 容器会重名冲突、旧卷不会重新初始化。

---

## 阶段 2 — Contracts（对外契约）

**产出物**：脱敏 DTO（`record`、`public`、XML 文档；密钥只出 `*Hint`）；跨模块只读查询/调用接口；枚举字符串常量。
**验证**：`dotnet build` 0 警告。
> 注意：常量取值与**前端、DDL** 一一对齐；Contracts 可依赖 `Hify.Shared`（用 `Result<T>`）。

---

## 阶段 3 — 密钥/敏感数据安全

**产出物**：对称加解密服务（AES-GCM，**密钥从配置注入**）+ 脱敏工具。
**验证**：单测覆盖往返、同明文密文各异、篡改抛异常、密文不含明文、密钥缺失/非法报错。
> 注意：
> - ⚠️ **别用 DataProtection 存长期密文**——自托管容器重建后默认密钥环丢失，既有密文无法解密。用配置注入的固定 AES 密钥（须跨重启稳定）。
> - 加解密服务注册为**延迟单例**：缺密钥时不阻断无关模块的 Host 启动，首次真正使用才校验。
> - 日志/异常**绝不输出密钥**。

---

## 阶段 4 — 外部适配器层（裸 HttpClient 方案）

**产出物**：适配器契约 + 工厂（按类型分发）+ 各家实现 + resilience 装配（每家熔断 + 舱壁 + 超时分级 + 按异常分类重试）。
**验证**：用**自写 stub `HttpMessageHandler`**（零外部依赖，比 WireMock 更确定）打桩真实 HTTP 管道，覆盖成功/鉴权失败/限流/流式增量与用量/失败映射。
**⏸ 等待用户确认**：各能力（chat/stream/embed 等）本期实现还是推迟。
> 注意：
> - 协议差异要吸收：SSE（`data:` 前缀）vs NDJSON（逐行 JSON），各家流式格式不同。
> - 流式返回 `Result<IAsyncEnumerable<...>>`：初始请求失败走 `Result`，流**中途**异常才抛；生成器用 `try/finally` 释放 response/request（注意 `yield` 不能在带 `catch` 的 `try` 里）。
> - 失败状态→错误码、失败正文摘要等**抽公共方法**复用，别每个适配器复制（早期 OpenAI 适配器复制过，后来去重）。

---

## 阶段 5 — 功能切片（Features）

**产出物**：每片「命令 + 处理 + FluentValidation 校验」聚在一起；应用服务（CRUD/动作）；跨模块门面（id → 解析 → 解密 → 选适配器调用，凭证不出模块）；后台任务（`IHostedService`，每轮新建 DI scope，可配间隔/可关/有初始延迟）。
**验证**：真实 PG 集成测试（skip-if-unavailable；用事务回滚或唯一名隔离）。
> 注意（都踩过）：
> - ⚠️ **后台任务默认开启 + 连库**，会在集成测试期间打 DB → 给**初始延迟**（短测试窗口内不触发）并在测试工厂里 `Enabled=false` 关掉。
> - 「每类型至多一个默认」靠**部分唯一索引** + 服务里用事务「先清旧默认、再设新默认」（两次 SaveChanges），否则违反唯一索引。
> - ⚠️ FluentValidation 链式 `NotEmpty().MaximumLength(n).WithMessage("…")`，`WithMessage` **只挂到最后一条规则**——要每条规则各自 `WithMessage`。
> - 跨模块门面解析失败要有明确分支码（不存在/已停用/解密失败/不支持等）。

---

## 阶段 6 — API 端点

**产出物**：内部 Controller（`[ApiController]`，动作直接返回 `Result<T>`/`PageResult<T>`）；OpenAPI 文档（**.NET 10 内置** `AddOpenApi`/`MapOpenApi`，不引第三方）。
**验证**：`WebApplicationFactory` **HTTP 端到端**（真实 PG）——验证路由、**内部 request record 的模型绑定**、全局校验过滤器拦截（返回 1001）、`Result`/`PageResult` 序列化（camelCase）、脱敏（响应无明文密钥）、`/openapi/v1.json` 含路由。
> 注意：
> - ⚠️ 模块类库要加 `<FrameworkReference Include="Microsoft.AspNetCore.App" />` 才能用 MVC 类型（首个带 Controller 的模块会缺）。
> - 内部 Controller 靠 Host 的 `InternalControllerFeatureProvider` 才能被发现。

---

## 阶段 7 — 前端特性切片（`web/features/<feature>`）

**产出物**：`api/<feature>.ts`（对齐**真实** DTO，经 `client.ts` 拦截器拆 `Result`）+ `constants.ts`（取值与后端对齐）+ 视图/对话框（鉴权等字段**条件渲染**、表单校验、乐观切换失败回滚、空/加载态）+ 路由挂载。
**验证**：`pnpm -C web build`（`vue-tsc` 类型检查 + `vite build`）0 错误；产物体积变化可佐证真实组件被编译进来；**手动 UI 联调**（无法驱动浏览器时交给用户：起后端 + `pnpm dev` + 刷新）。
> 注意（最坑）：
> - ⚠️⚠️ **文件工具用绝对路径**。Bash 里 `cd .../web` 会改变工作目录，之后用相对路径写文件会漂移（我们把新页面误写到 `web/web/src/...`，而 `web/src` 还是旧占位，导致「新页面不生效」）。排查信号：`git status` 出现 `?? web/web/`。
> - 既有 `client.ts` 已统一拆 `Result`/弹错；Element Plus 组件按需自动导入（模板里 `el-*` 无需 import），但 `ElMessage`/`ElMessageBox` 在脚本里要显式 `import`。

---

## 阶段 8 — 部署 / 让后端真正跑起来

**产出物**：`appsettings.json`（仅非敏感项）+ User Secrets（本地）/ 环境变量（生产）注入敏感项（DB 密码、加密密钥）+ 多阶段 `Dockerfile`（后端）+ `.dockerignore`（后端**和**前端各一份）+ compose 的**后端与前端服务**（env 注入、端口可覆盖、`depends_on` 依赖健康）。
**验证**：`docker compose up -d --build` → 三容器健康 → curl 冒烟：建资源（密钥脱敏）、列表、非法入参得 1001、**测试连接真实外呼**记录健康、`/api/v1/health` 200。
**⏸ 等待用户确认**：是否容器化 app、用 compose 起容器验证还是 `dotnet run`。
> 注意：
> - 敏感项用环境变量（嵌套键用 `__` 分隔，如 `ModelProvider__CredentialProtection__Key`），compose 内置仅作开发默认值，**生产必须覆盖**。
> - ⚠️ **改用户已有 User Secrets 前先 `dotnet user-secrets list` 确认**——别覆盖人家原有的本地配置（我们覆盖过 `Database:Password`）。
> - 健康端点路径以本项目为准（这里是 `/api/v1/health`，不是 `/health`）。

### 8a. 端口统一（前后端 × 本地/容器）

**产出物**：一套贯穿所有启动入口的端口约定（写进 README 一张表）。
**验证**：全库 grep 端口号——本地一个值、容器一个值，跨所有启动器/配置/反代完全一致；`docker compose config` 校验映射；起栈后 `curl 前端端口/api/v1/health` 确认**经反代**打通（不只是直连后端端口）。

| 场景 | 后端 | 前端 |
| --- | --- | --- |
| 本地调试 | 一个固定 HTTP 端口，须对齐 `start.cmd` / `dev.ps1` / `dev.cmd` / `launchSettings.json` / Vite 代理 target | Vite dev 端口（`vite.config` `server.port` 与启动脚本 `--port` 一致） |
| 容器部署 | 容器内固定端口（Dockerfile `EXPOSE` + compose 内部 + ASP.NET 默认 8080），宿主映射可覆盖 | Nginx 容器内端口 + 宿主映射；Nginx `/api` 反代须指向 **compose 里后端的服务名** |

> 注意（都踩过）：
> - ⚠️ 同一个本地后端端口会散落在 5+ 处，极易漂移（我们出现过 `dev.ps1`/`dev.cmd` 一个端口、`start.cmd`/Vite 另一个）。改完务必 **grep 全库核对**，别漏 `dev.cmd` 这种 `.ps1` 的副本。
> - ⚠️ Nginx `proxy_pass http://<服务名>:<端口>` 的**服务名必须等于 compose 后端服务名**（我们写了 `hify-host`，而 compose 实际叫 `api`，导致生产前端容器连不上后端）。
> - ⚠️ 容器部署要把**前端服务也接入 compose**，否则"前后端容器端口"无从对齐；宿主端口全部做成可覆盖（`API_PORT` / `WEB_PORT` / `DB_PORT` / `REDIS_PORT`）。

---

## 决策确认点清单（动手前对齐）

| 点 | 阶段 | 典型选项 |
| --- | --- | --- |
| 外部集成策略 | 0 | 官方 SDK / 裸 HttpClient |
| 一期范围 | 0 | 哪些子能力做/砍 |
| 数据模型：状态独立表？jsonb？拆密钥表？ | 0a | 1:1 独立表 / 行内列 |
| DDL 方式 | 1 | 手写 SQL / EF Migrations |
| 各调用能力本期做？ | 4 | 实现 / 推迟 |
| 周期后台任务本期做？ | 5 | 是 / 否 |
| 部署验证方式 | 8 | 容器化 compose / dotnet run |

## 踩坑总表（⚠️ 速查）

1. **文件工具用绝对路径**——Bash `cd` 会让相对路径漂移，文件写错目录（`web/web/src`）。
2. **PG18 卷挂 `/var/lib/postgresql`**（非 `/data`）。
3. **compose 宿主端口可覆盖**（本机常占用 5432）；重建先 `down -v`。
4. **别用 DataProtection 存长期密文**；用配置注入的固定 AES 密钥（跨重启稳定）。
5. **加解密服务延迟单例**，缺密钥不阻断 Host 启动。
6. **模块带 Controller 需 `FrameworkReference Microsoft.AspNetCore.App`**。
7. **后台任务默认开+连库**→集成测试要初始延迟 + 测试工厂关闭。
8. **共享 PG 的集成测试关并行**（`[assembly: CollectionBehavior(DisableTestParallelization = true)]`），否则 OFFSET 分页等断言会因并发非确定性。
9. **FluentValidation 链式 `WithMessage` 只挂最后一条规则**；每条各自附消息。
10. **改用户 User Secrets 前先 list 确认**，别覆盖既有值。
11. **枚举存 varchar 用 string 常量**（小写），别用 C# enum（名不符）。
12. **集成测试 skip-if-unavailable**：连不上真实依赖就早返回，不报错。
13. **端口跨启动器统一**：本地后端端口散落 5+ 处易漂移；改完 grep 全库核对（含 `dev.cmd` 这类副本）。本地一套值、容器一套值。
14. **Nginx 反代服务名 = compose 后端服务名**：曾写死 `hify-host` 而服务实为 `api`，前端容器连不上后端。
15. **前端镜像要有 `web/.dockerignore`**（排除 `node_modules`/`dist`）：否则 `COPY . .` 把宿主（Windows）依赖带入 Linux 镜像，构建/运行必坏。
16. **容器部署需把前端服务接入 compose**：否则前后端容器端口无从对齐；宿主端口全做成可覆盖（`API_PORT`/`WEB_PORT`/…）。
