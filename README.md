# Hify

简版 AI Agent 开发平台（参考 Dify），可本地部署，面向团队内部小规模使用。
详细产品定位、架构决策与编码规范见 [CLAUDE.md](./CLAUDE.md)。

## 技术栈

- 后端：.NET 10 + ASP.NET Core 10 + EF Core 10 + PostgreSQL 18（pgvector）+ Redis 7
- 前端：Vue 3 + TypeScript + Vite + Element Plus + Pinia + vue-router（包管理器 pnpm）
- 容器化：Docker + Docker Compose

## 端口约定（统一）

| 场景 | 后端 | 前端 |
| --- | --- | --- |
| 本地调试 | **5155**（`start.cmd` / `dev.ps1` / `dev.cmd` / `launchSettings`；Vite 代理 `/api`→5155） | **5173**（Vite dev server） |
| 容器部署 | 容器内 **8080**；宿主 `API_PORT`（默认 8080） | Nginx 容器内 **80**，反代 `/api`→`api:8080`；宿主 `WEB_PORT`（默认 8081） |

> 本地调试用 `scripts\start.cmd`（后端 5155 + 前端 5173 一键起）或分别 `dev.ps1` / `pnpm dev`。

## 快速启动（Docker Compose）

一条命令拉起 PostgreSQL（含 pgvector，首次自动执行根目录 `ddl.sql` 建库表）、Redis、后端 API 与前端 Nginx：

```bash
docker compose up -d --build
```

- 前端：http://localhost:8081 （Nginx 托管，`/api` 反代到后端）
- 后端 API：http://localhost:8080 （健康检查 `GET /api/v1/health`，OpenAPI 文档 `/openapi/v1.json`）
- 端口被占用时可覆盖：`DB_PORT`（5432）、`REDIS_PORT`（6379）、`API_PORT`（8080）、`WEB_PORT`（8081）。
  例：`DB_PORT=5433 WEB_PORT=8088 docker compose up -d --build`
- 仅起依赖（本地 `dotnet run` / `pnpm dev` 连它）：`docker compose up -d db redis`
- 改了 `ddl.sql` 需重置数据卷再初始化：`docker compose down -v && docker compose up -d`

> **生产务必覆盖内置开发默认值**（密码与加密密钥）：
> ```bash
> POSTGRES_PASSWORD=<强密码> MODELPROVIDER_KEY=$(openssl rand -base64 32) docker compose up -d --build
> ```

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

### ModelProvider 配置

| 配置键 | 说明 | 默认值 | 是否敏感 |
| --- | --- | --- | --- |
| `ModelProvider:CredentialProtection:Key` | 供应商密钥加密用 AES 密钥（base64，16/24/32 字节）。**须跨重启稳定**，否则既有密文无法解密。 | （无，须注入） | **是** |
| `ModelProvider:HealthProbe:Enabled` | 是否启用周期健康探活 | `true` | 否 |
| `ModelProvider:HealthProbe:IntervalSeconds` | 探活间隔（秒） | `60` | 否 |
| `ModelProvider:HealthProbe:InitialDelaySeconds` | 启动后首次探活延迟（秒） | `30` | 否 |

加密密钥本地设置（生产用环境变量 `ModelProvider__CredentialProtection__Key`）：

```bash
dotnet user-secrets set "ModelProvider:CredentialProtection:Key" "$(openssl rand -base64 32)" --project src/Hify.Host
```

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

## API 与文档

统一响应 `{ code, message, data }`（`code=200` 成功，否则四位业务码；2xxx 为模型提供商模块段）。OpenAPI 文档见 `/openapi/v1.json`。供应商与模型管理接口（节选）：

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| `POST` | `/api/v1/providers` | 创建供应商（同事务建健康行）|
| `GET` | `/api/v1/providers?page=&size=` | 分页列出（带健康）|
| `GET` / `PUT` / `DELETE` | `/api/v1/providers/{id}` | 详情 / 更新 / 删除（级联软删模型与健康）|
| `POST` | `/api/v1/providers/{id}/enable` \| `/disable` | 启用 / 停用 |
| `POST` | `/api/v1/providers/{id}/test-connection` | 测试连通性并刷新健康 |
| `POST` / `GET` | `/api/v1/providers/{providerId}/models` | 在供应商下新增 / 列出模型 |
| `GET` / `PUT` / `DELETE` | `/api/v1/models/{id}` | 模型详情 / 更新 / 删除 |
| `POST` | `/api/v1/models/{id}/set-default` \| `/enable` \| `/disable` | 设默认 / 启停 |

## 构建与测试

```bash
dotnet build Hify.sln
dotnet test  Hify.sln
```

> 部分集成测试需要可连的 PostgreSQL（连不上则自动跳过，不报错）。指定测试库：
> ```bash
> # 先起依赖：docker compose up -d db   （或 DB_PORT=5433 docker compose up -d db）
> HIFY_TEST_DB="Host=localhost;Port=5432;Database=hify;Username=hify;Password=hify" dotnet test Hify.sln
> ```

### 本地开发脚本

封装常用命令，默认以 Development 运行 Host（http://localhost:5155，健康检查 `/api/v1/health`）。

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
