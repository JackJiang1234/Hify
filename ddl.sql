-- =============================================================================
-- Hify 数据库 DDL（PostgreSQL 18 + pgvector）
-- =============================================================================
-- 适用范围：一期 MVP（参见 CLAUDE.md「做什么/不做什么」）。
--
-- 强制规范（全表遵循）：
--   * 主键 bigint GENERATED ALWAYS AS IDENTITY，禁 uuid；将来可平滑换 snowflake 风格 bigint。
--   * 所有列 NOT NULL + DEFAULT：字符串 ''、数值/引用 0、布尔 false、jsonb '{}'/'[]'。
--     唯一例外：vector 列无有意义默认值，NOT NULL 但不设 DEFAULT，插入时必给（已逐处注明）。
--   * 时间一律 bigint 存 epoch ms；软删 deleted_at（0=未删，否则=删除时刻），部分索引用 WHERE deleted_at = 0。
--   * 金额/Token 用量用 bigint 存最小精度，禁 DECIMAL；模型生成参数放 jsonb，避免浮点。
--   * 枚举用 varchar(32)，禁原生 ENUM（允许值见各列注释）。
--   * 命名 idx_{表}_{字段}；组合索引等值列在前、范围列在后；多对多两个方向都建索引。
--   * 唯一性用 UNIQUE INDEX（部分索引，仅约束未删行）；不建库级外键，应用层维护引用完整性。
--   * 每模块独立 schema / 独立 DbContext。
--
-- 执行顺序：扩展 -> schema -> 各模块表与索引。可重复执行（IF NOT EXISTS）。
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS vector;

CREATE SCHEMA IF NOT EXISTS model_provider;
CREATE SCHEMA IF NOT EXISTS agent;
CREATE SCHEMA IF NOT EXISTS knowledge;
CREATE SCHEMA IF NOT EXISTS conversation;
CREATE SCHEMA IF NOT EXISTS workflow;
CREATE SCHEMA IF NOT EXISTS mcp;


-- =============================================================================
-- 模块：model_provider（L0）—— 多模型提供商与模型管理
-- =============================================================================

-- 提供商实例（一份 OpenAI/Claude/Ollama 接入配置）。
-- 鉴权差异统一：auth_type(注入方式) + auth_header_name(头名) + api_key_cipher(密文)。
-- 健康状态另存 provider_health（与本表 1:1），隔离高频探活写与可缓存的配置行。
CREATE TABLE IF NOT EXISTS model_provider.provider (
    id               bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name             varchar(128)  NOT NULL DEFAULT '',
    provider_type    varchar(32)   NOT NULL DEFAULT '',     -- openai | claude | ollama
    base_url         varchar(512)  NOT NULL DEFAULT '',
    auth_type        varchar(32)   NOT NULL DEFAULT 'none', -- none | bearer | header
    auth_header_name varchar(64)   NOT NULL DEFAULT '',     -- header 模式下的头名，如 x-api-key
    api_key_cipher   varchar(1024) NOT NULL DEFAULT '',     -- 应用层加密后的密钥，禁明文
    api_key_hint     varchar(16)   NOT NULL DEFAULT '',     -- 末位明文，仅供展示
    settings         jsonb         NOT NULL DEFAULT '{}',   -- 私有静态配置（如 anthropic-version、organization）
    enabled          boolean       NOT NULL DEFAULT true,
    created_at       bigint        NOT NULL DEFAULT 0,
    updated_at       bigint        NOT NULL DEFAULT 0,
    deleted_at       bigint        NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_provider_name
    ON model_provider.provider (name) WHERE deleted_at = 0;
CREATE INDEX IF NOT EXISTS idx_provider_provider_type
    ON model_provider.provider (provider_type) WHERE deleted_at = 0;

-- 提供商下的具体模型（chat/embedding），一期仅手动录入（source 恒为 manual）。
CREATE TABLE IF NOT EXISTS model_provider.model (
    id                   bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    provider_id          bigint       NOT NULL DEFAULT 0,        -- -> model_provider.provider.id（应用层维护）
    name                 varchar(128) NOT NULL DEFAULT '',       -- 模型标识，如 gpt-4o / claude-opus-4-8
    display_name         varchar(128) NOT NULL DEFAULT '',
    model_type           varchar(32)  NOT NULL DEFAULT '',       -- chat | embedding
    context_window       bigint       NOT NULL DEFAULT 0,        -- 上下文窗口 token 数
    max_output_tokens    bigint       NOT NULL DEFAULT 0,
    embedding_dimensions integer      NOT NULL DEFAULT 0,        -- 仅 embedding 模型有意义（如 1536）
    supports_streaming   boolean      NOT NULL DEFAULT false,
    supports_tools       boolean      NOT NULL DEFAULT false,
    supports_vision      boolean      NOT NULL DEFAULT false,
    source               varchar(32)  NOT NULL DEFAULT 'manual', -- manual（一期仅手动录入）
    enabled              boolean      NOT NULL DEFAULT true,
    is_default           boolean      NOT NULL DEFAULT false,
    sort_order           integer      NOT NULL DEFAULT 0,
    created_at           bigint       NOT NULL DEFAULT 0,
    updated_at           bigint       NOT NULL DEFAULT 0,
    deleted_at           bigint       NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_model_provider_id
    ON model_provider.model (provider_id) WHERE deleted_at = 0;
CREATE UNIQUE INDEX IF NOT EXISTS idx_model_provider_id_name
    ON model_provider.model (provider_id, name) WHERE deleted_at = 0;
-- 每个供应商每种类型至多一个默认模型。
CREATE UNIQUE INDEX IF NOT EXISTS idx_model_default
    ON model_provider.model (provider_id, model_type)
    WHERE is_default = true AND deleted_at = 0;

-- 供应商健康（与 provider 1:1）。探活/连通性测试的结果落此；运行时熔断状态在内存，不入库。
CREATE TABLE IF NOT EXISTS model_provider.provider_health (
    id                   bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    provider_id          bigint       NOT NULL DEFAULT 0,           -- -> model_provider.provider.id（1:1）
    status               varchar(32)  NOT NULL DEFAULT 'unknown',  -- unknown | healthy | unhealthy
    latency_ms           integer      NOT NULL DEFAULT 0,
    consecutive_failures integer      NOT NULL DEFAULT 0,
    last_error           varchar(512) NOT NULL DEFAULT '',         -- 截断、不含凭证
    checked_at           bigint       NOT NULL DEFAULT 0,          -- 最近探活 epoch ms
    created_at           bigint       NOT NULL DEFAULT 0,
    updated_at           bigint       NOT NULL DEFAULT 0,
    deleted_at           bigint       NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_provider_health_provider_id
    ON model_provider.provider_health (provider_id) WHERE deleted_at = 0;


-- =============================================================================
-- 模块：agent（L1）—— Agent 配置（只存引用 ID，不嵌业务实体）
-- =============================================================================

CREATE TABLE IF NOT EXISTS agent.agent (
    id               bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name             varchar(128)  NOT NULL DEFAULT '',
    description      varchar(512)  NOT NULL DEFAULT '',
    model_id         bigint        NOT NULL DEFAULT 0,    -- -> model_provider.model.id
    system_prompt    text          NOT NULL DEFAULT '',
    model_params     jsonb         NOT NULL DEFAULT '{}', -- 生成参数：temperature/top_p/max_tokens 等（避免浮点列）
    retrieval_params jsonb         NOT NULL DEFAULT '{}', -- RAG 检索参数：top_k/score_threshold（Agent 级，避免浮点列）
    max_iterations   integer       NOT NULL DEFAULT 5,    -- 工具调用循环上限，防死循环烧 token
    enabled          boolean       NOT NULL DEFAULT true,
    created_at       bigint        NOT NULL DEFAULT 0,
    updated_at       bigint        NOT NULL DEFAULT 0,
    deleted_at       bigint        NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_agent_name
    ON agent.agent (name) WHERE deleted_at = 0;
CREATE INDEX IF NOT EXISTS idx_agent_model_id
    ON agent.agent (model_id) WHERE deleted_at = 0;

-- Agent <-> MCP 工具 绑定（多对多）。
CREATE TABLE IF NOT EXISTS agent.agent_tool (
    id         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    agent_id   bigint NOT NULL DEFAULT 0,   -- -> agent.agent.id
    tool_id    bigint NOT NULL DEFAULT 0,   -- -> mcp.mcp_tool.id
    created_at bigint NOT NULL DEFAULT 0,
    updated_at bigint NOT NULL DEFAULT 0,
    deleted_at bigint NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_agent_tool_agent_id_tool_id
    ON agent.agent_tool (agent_id, tool_id) WHERE deleted_at = 0;
CREATE INDEX IF NOT EXISTS idx_agent_tool_tool_id
    ON agent.agent_tool (tool_id) WHERE deleted_at = 0;

-- Agent <-> 知识库 绑定（多对多，用于 RAG）。
CREATE TABLE IF NOT EXISTS agent.agent_knowledge (
    id               bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    agent_id         bigint NOT NULL DEFAULT 0,   -- -> agent.agent.id
    knowledge_base_id bigint NOT NULL DEFAULT 0,  -- -> knowledge.knowledge_base.id
    created_at       bigint NOT NULL DEFAULT 0,
    updated_at       bigint NOT NULL DEFAULT 0,
    deleted_at       bigint NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_agent_knowledge_agent_id_kb_id
    ON agent.agent_knowledge (agent_id, knowledge_base_id) WHERE deleted_at = 0;
CREATE INDEX IF NOT EXISTS idx_agent_knowledge_kb_id
    ON agent.agent_knowledge (knowledge_base_id) WHERE deleted_at = 0;


-- =============================================================================
-- 模块：knowledge（L1）—— 知识库 + RAG（一期仅 TXT，固定长度分块）
-- =============================================================================

CREATE TABLE IF NOT EXISTS knowledge.knowledge_base (
    id                 bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name               varchar(128) NOT NULL DEFAULT '',
    description        varchar(512) NOT NULL DEFAULT '',
    embedding_model_id bigint       NOT NULL DEFAULT 0,   -- -> model_provider.model.id（embedding 模型）
    chunk_size         integer      NOT NULL DEFAULT 0,   -- 固定分块长度（字符数）
    chunk_overlap      integer      NOT NULL DEFAULT 0,   -- 分块重叠长度
    created_at         bigint       NOT NULL DEFAULT 0,
    updated_at         bigint       NOT NULL DEFAULT 0,
    deleted_at         bigint       NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_knowledge_base_name
    ON knowledge.knowledge_base (name) WHERE deleted_at = 0;

CREATE TABLE IF NOT EXISTS knowledge.document (
    id                bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    knowledge_base_id bigint        NOT NULL DEFAULT 0,   -- -> knowledge.knowledge_base.id
    name              varchar(256)  NOT NULL DEFAULT '',
    file_type         varchar(32)   NOT NULL DEFAULT '',  -- txt（一期）
    content_hash      varchar(64)   NOT NULL DEFAULT '',  -- 去重/变更检测
    status            varchar(32)   NOT NULL DEFAULT '',  -- pending | processing | completed | failed
    char_count        bigint        NOT NULL DEFAULT 0,
    chunk_count       integer       NOT NULL DEFAULT 0,   -- 已生成分块数，供进度/结果展示，免去 COUNT chunk
    error_message     varchar(512)  NOT NULL DEFAULT '',
    created_at        bigint        NOT NULL DEFAULT 0,
    updated_at        bigint        NOT NULL DEFAULT 0,
    deleted_at        bigint        NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_document_knowledge_base_id
    ON knowledge.document (knowledge_base_id) WHERE deleted_at = 0;
-- 同一知识库内内容去重：相同 content_hash 不重复入库（应用层先查、唯一索引兜底并发）。
CREATE UNIQUE INDEX IF NOT EXISTS idx_document_kb_id_content_hash
    ON knowledge.document (knowledge_base_id, content_hash) WHERE deleted_at = 0;

-- 分块 + 向量。关系数据存 PostgreSQL，向量存 pgvector（维度固定 1536）。
CREATE TABLE IF NOT EXISTS knowledge.chunk (
    id                bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    document_id       bigint        NOT NULL DEFAULT 0,   -- -> knowledge.document.id
    knowledge_base_id bigint        NOT NULL DEFAULT 0,   -- 冗余便于按库检索 -> knowledge.knowledge_base.id
    chunk_index       integer       NOT NULL DEFAULT 0,   -- 文档内分块序号
    content           text          NOT NULL DEFAULT '',
    embedding         vector(1536)  NOT NULL,             -- 例外：vector 无有意义默认值，插入时必给
    created_at        bigint        NOT NULL DEFAULT 0,
    updated_at        bigint        NOT NULL DEFAULT 0,
    deleted_at        bigint        NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_chunk_document_id
    ON knowledge.chunk (document_id) WHERE deleted_at = 0;
CREATE INDEX IF NOT EXISTS idx_chunk_knowledge_base_id
    ON knowledge.chunk (knowledge_base_id) WHERE deleted_at = 0;
-- 同一文档内分块序号唯一：文档重新处理时按 (doc, index) 幂等覆盖，避免失败重试产生重复块。
CREATE UNIQUE INDEX IF NOT EXISTS idx_chunk_document_id_chunk_index
    ON knowledge.chunk (document_id, chunk_index) WHERE deleted_at = 0;
-- 向量检索 HNSW 索引（余弦距离）。检索须加 LIMIT，禁全量排序。
CREATE INDEX IF NOT EXISTS idx_chunk_embedding
    ON knowledge.chunk USING hnsw (embedding vector_cosine_ops) WHERE deleted_at = 0;


-- =============================================================================
-- 模块：conversation（L2）—— 对话引擎（message 增长最快）
-- =============================================================================

CREATE TABLE IF NOT EXISTS conversation.conversation (
    id         bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    agent_id   bigint        NOT NULL DEFAULT 0,   -- -> agent.agent.id
    title      varchar(256)  NOT NULL DEFAULT '',
    created_at bigint        NOT NULL DEFAULT 0,
    updated_at bigint        NOT NULL DEFAULT 0,
    deleted_at bigint        NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_conversation_agent_id
    ON conversation.conversation (agent_id) WHERE deleted_at = 0;

-- 一次用户提问到最终答复可能产生多行（工具循环：user -> assistant(tool_calls) -> tool -> assistant）。
-- 时序以单调递增的 id 为准（created_at 为 epoch ms，同毫秒会撞，不可作排序键）。
CREATE TABLE IF NOT EXISTS conversation.message (
    id              bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    conversation_id bigint       NOT NULL DEFAULT 0,   -- -> conversation.conversation.id
    role            varchar(32)  NOT NULL DEFAULT '',  -- system | user | assistant | tool
    content         text         NOT NULL DEFAULT '',
    tool_calls      jsonb        NOT NULL DEFAULT '[]', -- assistant 请求的工具调用 [{id,name,arguments}]，非工具轮为 []
    tool_call_id    varchar(64)  NOT NULL DEFAULT '',  -- tool 结果消息回指的调用 id（关联上游 tool_calls 某项）
    finish_reason   varchar(32)  NOT NULL DEFAULT '',  -- stop | length | tool_calls | error
    status          varchar(32)  NOT NULL DEFAULT '',  -- streaming | completed | failed | cancelled
    error_message   varchar(512) NOT NULL DEFAULT '',  -- 失败原因，截断、不含凭证/PII
    model_id        bigint       NOT NULL DEFAULT 0,   -- 实际使用的模型 -> model_provider.model.id
    prompt_tokens   bigint       NOT NULL DEFAULT 0,   -- token 用量用 bigint
    completion_tokens bigint     NOT NULL DEFAULT 0,
    created_at      bigint       NOT NULL DEFAULT 0,
    updated_at      bigint       NOT NULL DEFAULT 0,
    deleted_at      bigint       NOT NULL DEFAULT 0
);
-- 大表必备：等值列 conversation_id 在前、范围列 created_at 在后。
CREATE INDEX IF NOT EXISTS idx_message_conversation_id_created_at
    ON conversation.message (conversation_id, created_at) WHERE deleted_at = 0;


-- =============================================================================
-- 模块：workflow（L2）—— 简版工作流（JSON 配置 + 简单拖拽，线性 + 单层条件分支）
-- =============================================================================
-- 定义存单 jsonb（definition: {nodes,edges}），前端 Vue Flow 拖拽产出、引擎按图遍历。
-- 节点类型：start | llm | tool(MCP) | condition | end。执行为同步（一期），逐节点轨迹存 run.trace。

CREATE TABLE IF NOT EXISTS workflow.workflow (
    id          bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name        varchar(128)  NOT NULL DEFAULT '',
    description varchar(512)  NOT NULL DEFAULT '',
    definition  jsonb         NOT NULL DEFAULT '{}',     -- 工作流 JSON 定义 {nodes,edges}（节点 + 连线 + 条件）
    status      varchar(32)   NOT NULL DEFAULT 'draft',  -- draft | published（发布前跑图校验）
    created_at  bigint        NOT NULL DEFAULT 0,
    updated_at  bigint        NOT NULL DEFAULT 0,
    deleted_at  bigint        NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_workflow_name
    ON workflow.workflow (name) WHERE deleted_at = 0;
-- 列表按 status 过滤 + id 游标倒序。
CREATE INDEX IF NOT EXISTS idx_workflow_status
    ON workflow.workflow (status) WHERE deleted_at = 0;

-- 工作流执行记录（节点级明细可后续按需扩展 workflow_node_run；一期逐节点轨迹内联 trace jsonb）。
CREATE TABLE IF NOT EXISTS workflow.workflow_run (
    id            bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_id   bigint       NOT NULL DEFAULT 0,    -- -> workflow.workflow.id
    status        varchar(32)  NOT NULL DEFAULT '',   -- running | succeeded | failed
    inputs        jsonb        NOT NULL DEFAULT '{}',  -- 触发输入（满足 start.inputs）
    output        text         NOT NULL DEFAULT '',    -- 最终输出文本（end 节点产出，纯文本非 JSON）
    trace         jsonb        NOT NULL DEFAULT '[]',  -- 逐节点轨迹 [{nodeId,status,ms,input,output}]，供调试/展示
    error_message varchar(512) NOT NULL DEFAULT '',    -- 失败原因，截断、不含凭证/PII
    started_at    bigint       NOT NULL DEFAULT 0,
    finished_at   bigint       NOT NULL DEFAULT 0,
    created_at    bigint       NOT NULL DEFAULT 0,
    updated_at    bigint       NOT NULL DEFAULT 0,
    deleted_at    bigint       NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_workflow_run_workflow_id_created_at
    ON workflow.workflow_run (workflow_id, created_at) WHERE deleted_at = 0;


-- =============================================================================
-- 模块：mcp（L0）—— MCP 工具接入（Client 侧：连接外部 MCP Server，发现并缓存工具）
-- =============================================================================
-- 一期仅支持 Streamable HTTP 传输（不做 stdio 子进程 / 老式 HTTP+SSE）。
-- 鉴权差异复用 model_provider.provider 的三件套：auth_type + auth_header_name + api_key_cipher。
-- 工具调用的请求/结果记录在 conversation.message（tool_calls + role=tool），本模块不另建日志表。

-- MCP Server 连接配置 + 鉴权 + 连接/发现状态（低频探活，状态内联本表，不拆健康表）。
CREATE TABLE IF NOT EXISTS mcp.mcp_server (
    id               bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name             varchar(128)  NOT NULL DEFAULT '',
    transport        varchar(32)   NOT NULL DEFAULT 'streamable_http', -- 一期固定 streamable_http
    endpoint         varchar(512)  NOT NULL DEFAULT '',     -- Streamable HTTP 端点 URL，如 https://host/mcp
    auth_type        varchar(32)   NOT NULL DEFAULT 'none', -- none | bearer | header
    auth_header_name varchar(64)   NOT NULL DEFAULT '',     -- header 模式下的头名，如 x-api-key
    api_key_cipher   varchar(1024) NOT NULL DEFAULT '',     -- 应用层加密后的凭证，禁明文、禁入日志
    api_key_hint     varchar(16)   NOT NULL DEFAULT '',     -- 末位明文，仅供展示
    timeout_ms       integer       NOT NULL DEFAULT 0,      -- 0=用 appsettings 全局默认；>0 覆盖
    enabled          boolean       NOT NULL DEFAULT true,
    status           varchar(32)   NOT NULL DEFAULT 'unknown', -- unknown | connected | error
    last_error       varchar(512)  NOT NULL DEFAULT '',     -- 最近错误，截断、不含凭证
    last_synced_at   bigint        NOT NULL DEFAULT 0,      -- 最近一次 tools/list 成功时刻 epoch ms
    tool_count       integer       NOT NULL DEFAULT 0,      -- 冗余计数，免 COUNT mcp_tool
    created_at       bigint        NOT NULL DEFAULT 0,
    updated_at       bigint        NOT NULL DEFAULT 0,
    deleted_at       bigint        NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_mcp_server_name
    ON mcp.mcp_server (name) WHERE deleted_at = 0;

-- 从 MCP Server 发现并缓存的工具。
-- 重新发现按 (server_id, name) 原地 upsert：id 永不变，保护 agent.agent_tool.tool_id 引用稳定。
-- 服务端移除某工具时仅置 available=false（不软删、不换 id），重现则置回 true。
-- available（服务端是否仍提供）与 enabled（管理员是否启用）含义独立，互不覆盖。
CREATE TABLE IF NOT EXISTS mcp.mcp_tool (
    id           bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    server_id    bigint        NOT NULL DEFAULT 0,    -- -> mcp.mcp_server.id（应用层维护）
    name         varchar(128)  NOT NULL DEFAULT '',
    description  text          NOT NULL DEFAULT '',   -- 工具描述，喂给模型、可能较长，用 text
    input_schema jsonb         NOT NULL DEFAULT '{}', -- 工具入参 JSON Schema
    available    boolean       NOT NULL DEFAULT true, -- 最近一次发现中服务端是否仍提供该工具
    enabled      boolean       NOT NULL DEFAULT true, -- 管理员开关
    created_at   bigint        NOT NULL DEFAULT 0,
    updated_at   bigint        NOT NULL DEFAULT 0,
    deleted_at   bigint        NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_mcp_tool_server_id
    ON mcp.mcp_tool (server_id) WHERE deleted_at = 0;
CREATE UNIQUE INDEX IF NOT EXISTS idx_mcp_tool_server_id_name
    ON mcp.mcp_tool (server_id, name) WHERE deleted_at = 0;
