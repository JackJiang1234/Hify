# Hify 工程骨架搭建结果

记录当前已完成的工程骨架、关键设计决策与验证状态。配置/运行细节见根目录 [README.md](../README.md)，规范见 [CLAUDE.md](../CLAUDE.md)。

## 一、完成范围

- 模块化单体解决方案骨架（.NET 10）
- 统一响应 `Result<T>` / `PageResult<T>`
- 错误码 `ErrorCode` 与业务异常 `BizException`
- 全局异常处理中间件（异常 → 统一响应）
- 全局 JSON 序列化（Newtonsoft）
- 模块装配机制（`IModule` + 各模块注册入口 + 控制器发现）
- 数据库 / Redis 连接配置（敏感信息隔离到私密文件）
- 健康检查端点 `/health`
- 本地开发脚本（PowerShell / cmd）

## 二、工程结构

```
Hify.sln (经典 .sln 格式)
├── Directory.Build.props        # 全局：net10.0 / Nullable / ImplicitUsings / TreatWarningsAsErrors / EnforceCodeStyleInBuild
├── src/
│   ├── Hify.Host/               # 组合根：配置校验、模块装配、异常中间件、全局 JSON、健康检查
│   ├── Hify.Shared/             # 共享内核：Results / Exceptions / Configuration / Modularity
│   ├── Hify.Contracts/          # 模块对外接口与 DTO（模块间唯一可见，按模块分区）
│   └── Modules/                 # ModelProvider / Mcp / Knowledge / Agent / Conversation / Workflow
└── tests/                       # Shared.Tests + IntegrationTests + 6 个模块测试项目
```

### 依赖方向

- `Hify.Contracts` → `Hify.Shared`
- 各模块 → `Hify.Shared` + `Hify.Contracts`（**模块间无直接引用**）
- `Hify.Host` → 全部模块 + Shared + Contracts（仅组合根知道所有模块）
- 跨模块协作走 Contracts 接口 + DI；L0→L1→L2 分层为运行时约束，编译期由「模块只能引用 Contracts」兜底。

## 三、关键设计决策

| 决策点 | 选择 | 理由 / 说明 |
| --- | --- | --- |
| 解决方案格式 | 经典 `.sln` | .NET 10 默认生成 `.slnx`，按 CLAUDE.md 改回 `.sln` |
| 分页结构 | `PageResult<T> : Result<IReadOnlyList<T>>` | 用户拍板；已同步更新 CLAUDE.md 接口规范 |
| 字段命名 | 请求/响应统一 `size`（非 `pageSize`） | 用户拍板，入参出参一致 |
| JSON 序列化 | Newtonsoft.Json（全局 + 中间件共用 `HifyJsonSettings`） | 用户要求；camelCase + 保留 null |
| 端点风格 | Controllers + `AddNewtonsoftJson` | 用户拍板；全局 Newtonsoft 官方支持方式 |
| 控制器可见性 | internal（`InternalControllerFeatureProvider` 支持发现） | 对齐「默认 internal」约定 |
| 异常 HTTP 状态 | BizException→200(业务码)，未捕获→500 | 业务错误走统一响应体；服务端错误保留 5xx 供监控 |
| 健康检查 HTTP 状态 | 健康 200 / 不健康 503 | 供负载均衡探测；body 仍为统一 `Result<T>` |
| 错误码枚举 | 真 `enum` + `GetMessage()` 扩展 | 通用段 1000–1999 |

## 四、已实现组件

### Result / PageResult — `Hify.Shared/Results/`
- `Result<T>`：`Code` / `Message` / `Data`，静态 `Ok` / `Fail`。
- `PageResult<T> : Result<IReadOnlyList<T>>`：附加 `Total` / `Page` / `Size`，`Ok` 工厂；null 列表归一化为 `[]`。

### 错误码与异常 — `Hify.Shared/Exceptions/`
- `ErrorCode`（1000 InternalError … 1007 Timeout）+ `ErrorCodeExtensions`（`ToCode` / `GetMessage`）。
- `BizException`：持有 `ErrorCode`，支持自定义 message 覆盖与包裹 InnerException。

### 全局异常中间件 — `Hify.Host/Middleware/ExceptionHandlingMiddleware.cs`
- BizException → HTTP 200 + `Result.Fail(业务码, message)`；其它 → HTTP 500 + `1000 系统内部错误`，不泄露异常细节。

### 全局 JSON — `Hify.Host/Json/HifyJsonSettings.cs`
- 单一策略源（camelCase + 保留 null），MVC 全局与异常中间件共用。

### 模块装配 — `Hify.Shared/Modularity/IModule.cs` + 各模块 `*Module.cs` + `Hify.Host/Modularity/ModuleHostExtensions.cs`
- 每模块一个 `public sealed class XxxModule : IModule`（`RegisterServices` 当前为空 + TODO）。
- Host 统一：`AddControllers().AddNewtonsoftJson()` + 逐模块 `AddApplicationPart` + `RegisterServices`。
- `InternalControllerFeatureProvider` 支持发现 internal 控制器。

### 配置与密钥 — `Hify.Shared/Configuration/` + `Hify.Host/Configuration/ConfigurationHostExtensions.cs`
- `DatabaseOptions` / `RedisOptions`（DataAnnotations 校验）。
- 非敏感项入 `appsettings.json`；密码经 User Secrets（本地）/ 环境变量（生产）。
- `Bind + ValidateDataAnnotations + ValidateOnStart`：缺密码等非法配置启动即失败。

### 健康检查 — `Hify.Host/HealthChecks/`
- `GET /health`，`self` 存活检查，自定义写出器输出统一 `Result<T>`（含各检查项明细）。
- DB/Redis 就绪检查待连接落地后追加。

### 开发脚本 — `scripts/`
- `dev.ps1`（PowerShell/pwsh）、`dev.cmd`（Windows）：`run`(默认) / `test` / `build`，可指定端口。脚本内全英文。

## 五、依赖包

| 项目 | 包 / 引用 |
| --- | --- |
| Hify.Host | Newtonsoft.Json 13.0.3、Microsoft.AspNetCore.Mvc.NewtonsoftJson 10.0.9 |
| Hify.Shared | Microsoft.Extensions.DependencyInjection.Abstractions / Configuration.Abstractions 10.0.9 |
| Hify.IntegrationTests | Microsoft.AspNetCore.Mvc.Testing 10.0.9 + FrameworkReference Microsoft.AspNetCore.App |

## 六、验证状态

- **构建**：全解决方案成功，0 警告 0 错误（`TreatWarningsAsErrors=true`）。
- **测试**：45 通过（`Hify.Shared.Tests` 35 + `Hify.IntegrationTests` 10），0 失败。
- **运行**：Host 以 Development 启动正常（监听 5080），`GET /health` 返回 200：
  ```json
  {"code":200,"message":"healthy","data":{"status":"Healthy","totalDurationMs":11,
   "checks":[{"name":"self","status":"Healthy","description":"Host 存活"}]}}
  ```

## 七、待办 / 下一步

- 各模块落地首个垂直切片：DbContext（Npgsql/EF，独立 schema）+ 控制器（CRUD）+ 数据库迁移。
- 接入真实 DB/Redis 连接（消费 `DatabaseOptions` / `RedisOptions`），并补 `/health` 的就绪检查（拆 `live` / `ready`）。
- Swagger / OpenAPI。
- Docker Compose（PostgreSQL+pgvector / Redis）一键部署。
