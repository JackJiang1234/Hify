# 数据库初始化 & PostgreSQL MCP 接入会话记录

> 日期：2026-06-16
> 主题：执行 `ddl.sql` 建库、安装并接入 PostgreSQL MCP，测试数据库连通性。
> 形式：按用户请求顺序记录「需求 → 处理 / 决策 → 结果」。

---

## 1. 执行 ddl.sql、测试 DB 连接

**需求**：执行 `ddl.sql`，测试数据库连接。

**处理与发现**：
- 读取 `ddl.sql` 与 `src/Hify.Host/appsettings.json`。
- 发现 `appsettings.json` 中 `Database` 的 `Username`/`Password` 为空，无法直接拼连接串。
- 准备探测本机 `psql` 客户端时被用户中断，转为安装 MCP 方案。

## 2. 安装 PostgreSQL MCP

**需求**：安装 postgresql MCP。

**处理**：
- 检查环境：无现成 `.mcp.json`；`claude` CLI 2.1.178、`npx` 10.7.0、`uvx`/`uv` 0.10.2、Docker 29.5.3 均可用（无 pipx、无独立 python）。
- 经用户确认两项决策：
  - **MCP 实现**：crystaldba `postgres-mcp`（Postgres MCP Pro，支持健康检查、索引调优、执行计划分析；用 `uvx` 运行）。
  - **配置范围**：local（仅本机/本项目，不进 git，避免凭据泄露）。

**关键修正（连接地址）**：
- 用户更新 `appsettings.json` 后凭据为 `postgres / 123456`，库 `vector_db`，但 `Host` 写的是 docker 服务名 `postgres`（仅容器网络内可解析）。
- MCP 经 `uvx` 跑在**宿主机**，`postgres` 无法解析。经 `docker ps` 确认容器 `pg18-pgvector`（镜像 `pgvector/pgvector:0.8.2-pg18-trixie`）已将 `5432` 映射到宿主机，故连接改用 `localhost:5432`。

**执行命令**：
```
claude mcp add postgres-mcp -s local \
  -e DATABASE_URI="postgresql://postgres:123456@localhost:5432/vector_db" \
  -- uvx postgres-mcp --access-mode=unrestricted
```

**结果**：
- 写入 `~/.claude.json`（本项目节点，local 范围）。
- 首次 `claude mcp list` 显示 `✘ Failed to connect`——为 `uvx` 首次下载包超时所致；包缓存后复测 `✔ Connected`。
- 容器内 `psql` 验证 DB 可连：PostgreSQL **18.4** + pgvector。
- ⚠️ 注意：当前为 `unrestricted` 读写模式，且密码明文存于 `~/.claude.json`，本机自用可，勿外传该文件。

## 3. 用容器内 psql 执行 ddl.sql

**需求**：用容器内 psql 把 ddl.sql 跑了。

**处理**：
```
docker exec -i pg18-pgvector psql -U postgres -d vector_db -v ON_ERROR_STOP=1 < ddl.sql
```

**结果**（`ON_ERROR_STOP=1` 全程零报错，脚本幂等 `IF NOT EXISTS`）：

| 项 | 结果 |
|---|---|
| schema | 6 个：`model_provider` / `agent` / `knowledge` / `conversation` / `workflow` / `mcp` |
| 表 | 14 张（全部建成） |
| 索引 | 36 个（含 14 主键约束 + 22 显式索引） |
| pgvector | 已启用，版本 0.8.2 |

## 4. 重启会话加载 postgres-mcp

**需求**：重启会话加载 postgres-mcp。

**处理与结果**：
- 说明 MCP server 在 `claude` 启动时加载，需用户自行退出后 `claude --continue` 重启。
- 给出验证方式：`/mcp` 查看 `postgres-mcp` 是否 connected 及其工具列表。
- 重启后 `mcp__postgres-mcp__*` 系列工具已注入（execute_sql / explain_query / list_objects / analyze_db_health / analyze_*_indexes / get_top_queries 等）。

## 5. 将讨论记录写入 doc

**需求**：将讨论记录写入 doc。

**结果**：生成本文件 `doc/db_mcp_setup_session.md`。
