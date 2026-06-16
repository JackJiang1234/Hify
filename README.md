# Hify

简版 AI Agent 开发平台（参考 Dify），可本地部署，面向团队内部小规模使用。
详细产品定位、架构决策与编码规范见 [CLAUDE.md](./CLAUDE.md)。

## 技术栈

- 后端：.NET 10 + ASP.NET Core 10 + EF Core 10 + PostgreSQL 18（pgvector）+ Redis 7
- 前端：Vue 3 + TypeScript + Vite + Element Plus + Pinia + vue-router（包管理器 pnpm）
- 容器化：Docker + Docker Compose

## 环境要求

- .NET SDK 10.0+
- PostgreSQL 18（启用 pgvector 扩展）
- Redis 7+
- Node.js 20.19+（前端，推荐 20.19+ / 22.12+）+ pnpm 9+

## 配置说明

配置遵循「非敏感项入仓库、敏感项隔离」原则：

- **非敏感项**（主机、端口、库名、连接池、超时等）放 `src/Hify.Host/appsettings.json`。
- **敏感项**（密码、生产内部主机名等）**不进仓库**：本地用 .NET User Secrets，生产用环境变量。

启动时会对配置做校验（`ValidateOnStart`）：缺失或非法配置（如未提供数据库密码）会**直接启动失败**，以尽早暴露问题。

### 配置项

`appsettings.json` 中的 `Database` 与 `Redis` 节：

| 配置键 | 说明 | 默认值 | 是否敏感 |
| --- | --- | --- | --- |
| `Database:Host` | PostgreSQL 主机 | `localhost` | 生产敏感 |
| `Database:Port` | 端口 | `5432` | 否 |
| `Database:Database` | 数据库名 | `hify` | 否 |
| `Database:Username` | 用户名 | `hify` | 否 |
| `Database:Password` | 密码 | （空，须注入） | **是** |
| `Database:MaxPoolSize` | 连接池最大连接数 | `50` | 否 |
| `Database:CommandTimeoutSeconds` | 命令超时（秒） | `30` | 否 |
| `Redis:Host` | Redis 主机 | `localhost` | 生产敏感 |
| `Redis:Port` | 端口 | `6379` | 否 |
| `Redis:Password` | 密码（无认证可留空） | （空） | **是** |
| `Redis:Database` | 逻辑库索引 | `0` | 否 |
| `Redis:ConnectTimeoutMs` | 连接超时（毫秒） | `5000` | 否 |

### 本地开发：设置私密配置（User Secrets）

私密信息保存在用户目录下的 `secrets.json`（**不在仓库内**，不会被提交），仅 Development 环境加载。

每位开发者在本机执行一次（密码替换为本地实际值）：

```bash
cd src/Hify.Host
dotnet user-secrets set "Database:Password" "<你的本地数据库密码>"
# Redis 有认证时再设置：
dotnet user-secrets set "Redis:Password" "<你的本地Redis密码>"
```

查看 / 移除：

```bash
dotnet user-secrets list --project src/Hify.Host
dotnet user-secrets remove "Database:Password" --project src/Hify.Host
```

> 私密文件位置：`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`（Windows）/
> `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`（Linux/macOS）。`UserSecretsId` 见 `src/Hify.Host/Hify.Host.csproj`。

### 生产部署：环境变量覆盖

生产环境通过环境变量注入敏感项（含真实内部主机名），不使用 User Secrets。配置键的层级用双下划线 `__` 分隔：

```bash
Database__Host=<内部数据库主机>
Database__Password=<数据库密码>
Redis__Host=<内部Redis主机>
Redis__Password=<Redis密码>
```

（在 Docker Compose 中通过 `environment` 或 `.env` 提供，`.env` 不应提交。）

## 构建与测试

```bash
dotnet build Hify.sln
dotnet test  Hify.sln
```

### 本地开发脚本

封装常用命令，默认以 Development 运行 Host（http://localhost:5080，健康检查 `/api/v1/health`）。

PowerShell / pwsh：

```powershell
./scripts/dev.ps1            # 运行 Host
./scripts/dev.ps1 test       # 运行全部测试
./scripts/dev.ps1 build      # 构建解决方案
./scripts/dev.ps1 run -Port 5090
```

Windows cmd：

```bat
scripts\dev.cmd            :: 运行 Host
scripts\dev.cmd test       :: 运行全部测试
scripts\dev.cmd build      :: 构建解决方案
scripts\dev.cmd run 5090
```

### 前端开发脚本

前端为独立 npm 项目（位于 `web/`，不挂进 .NET solution）。脚本自动定位 `web/` 并在缺依赖时先 `pnpm install`，默认在 http://localhost:5173 启动 Vite dev server。dev server 会把 `/api`（含 `/api/v1/health`）反代到后端（见 `web/vite.config.ts`），需另行用 `scripts/dev.ps1` 启动后端。

PowerShell / pwsh：

```powershell
./scripts/web.ps1            # 启动 dev server（http://localhost:5173）
./scripts/web.ps1 build      # 类型检查 + 生产构建到 web/dist
./scripts/web.ps1 preview    # 预览生产构建
./scripts/web.ps1 install    # 安装依赖
./scripts/web.ps1 lint       # ESLint 自动修复
./scripts/web.ps1 dev -Port 5180
```

Windows cmd：

```bat
scripts\web.cmd            :: 启动 dev server
scripts\web.cmd build      :: 类型检查 + 生产构建
scripts\web.cmd preview    :: 预览生产构建
scripts\web.cmd install    :: 安装依赖
scripts\web.cmd dev 5180
```
