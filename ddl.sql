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

-- 提供商实例（一个 OpenAI/Claude/Gemini/Ollama 接入配置）。
CREATE TABLE IF NOT EXISTS model_provider.provider (
    id            bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name          varchar(128)  NOT NULL DEFAULT '',
    provider_type varchar(32)   NOT NULL DEFAULT '',   -- openai | claude | gemini | ollama
    base_url      varchar(512)  NOT NULL DEFAULT '',
    api_key       varchar(1024) NOT NULL DEFAULT '',   -- 密文存储（应用层加密），不存明文
    enabled       boolean       NOT NULL DEFAULT true,
    status        varchar(32)   NOT NULL DEFAULT '',   -- unknown | active | error | disabled
    created_at    bigint        NOT NULL DEFAULT 0,
    updated_at    bigint        NOT NULL DEFAULT 0,
    deleted_at    bigint        NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_provider_name
    ON model_provider.provider (name) WHERE deleted_at = 0;
CREATE INDEX IF NOT EXISTS idx_provider_provider_type
    ON model_provider.provider (provider_type) WHERE deleted_at = 0;

-- 提供商下的具体模型（chat/embedding）。
CREATE TABLE IF NOT EXISTS model_provider.model (
    id             bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    provider_id    bigint       NOT NULL DEFAULT 0,    -- -> model_provider.provider.id（应用层维护）
    name           varchar(128) NOT NULL DEFAULT '',  -- 模型标识，如 gpt-4o / claude-3-5-sonnet
    model_type     varchar(32)  NOT NULL DEFAULT '',  -- chat | embedding
    context_window integer      NOT NULL DEFAULT 0,   -- 上下文窗口 token 数
    dimension      integer      NOT NULL DEFAULT 0,   -- 仅 embedding 模型有意义（如 1536）
    enabled        boolean      NOT NULL DEFAULT true,
    created_at     bigint       NOT NULL DEFAULT 0,
    updated_at     bigint       NOT NULL DEFAULT 0,
    deleted_at     bigint       NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_model_provider_id
    ON model_provider.model (provider_id) WHERE deleted_at = 0;
CREATE UNIQUE INDEX IF NOT EXISTS idx_model_provider_id_name
    ON model_provider.model (provider_id, name) WHERE deleted_at = 0;


-- =============================================================================
-- 模块：agent（L1）—— Agent 配置（只存引用 ID，不嵌业务实体）
-- =============================================================================

CREATE TABLE IF NOT EXISTS agent.agent (
    id            bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name          varchar(128)  NOT NULL DEFAULT '',
    description   varchar(512)  NOT NULL DEFAULT '',
    model_id      bigint        NOT NULL DEFAULT 0,    -- -> model_provider.model.id
    system_prompt text          NOT NULL DEFAULT '',
    model_params  jsonb         NOT NULL DEFAULT '{}', -- 生成参数：temperature/top_p/max_tokens 等（避免浮点列）
    enabled       boolean       NOT NULL DEFAULT true,
    created_at    bigint        NOT NULL DEFAULT 0,
    updated_at    bigint        NOT NULL DEFAULT 0,
    deleted_at    bigint        NOT NULL DEFAULT 0
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
    error_message     varchar(512)  NOT NULL DEFAULT '',
    created_at        bigint        NOT NULL DEFAULT 0,
    updated_at        bigint        NOT NULL DEFAULT 0,
    deleted_at        bigint        NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_document_knowledge_base_id
    ON knowledge.document (knowledge_base_id) WHERE deleted_at = 0;

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

CREATE TABLE IF NOT EXISTS conversation.message (
    id              bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    conversation_id bigint       NOT NULL DEFAULT 0,   -- -> conversation.conversation.id
    role            varchar(32)  NOT NULL DEFAULT '',  -- system | user | assistant | tool
    content         text         NOT NULL DEFAULT '',
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
-- 模块：workflow（L2）—— 简版工作流（JSON 配置，线性 + 条件分支）
-- =============================================================================

CREATE TABLE IF NOT EXISTS workflow.workflow (
    id          bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name        varchar(128)  NOT NULL DEFAULT '',
    description varchar(512)  NOT NULL DEFAULT '',
    definition  jsonb         NOT NULL DEFAULT '{}',  -- 工作流 JSON 定义（节点 + 连线 + 条件）
    enabled     boolean       NOT NULL DEFAULT true,
    created_at  bigint        NOT NULL DEFAULT 0,
    updated_at  bigint        NOT NULL DEFAULT 0,
    deleted_at  bigint        NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_workflow_name
    ON workflow.workflow (name) WHERE deleted_at = 0;

-- 工作流执行记录（节点级明细可后续按需扩展 workflow_node_run）。
CREATE TABLE IF NOT EXISTS workflow.workflow_run (
    id          bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_id bigint       NOT NULL DEFAULT 0,   -- -> workflow.workflow.id
    status      varchar(32)  NOT NULL DEFAULT '',  -- pending | running | succeeded | failed
    input       jsonb        NOT NULL DEFAULT '{}',
    output      jsonb        NOT NULL DEFAULT '{}',
    error_message varchar(512) NOT NULL DEFAULT '',
    started_at  bigint       NOT NULL DEFAULT 0,
    finished_at bigint       NOT NULL DEFAULT 0,
    created_at  bigint       NOT NULL DEFAULT 0,
    updated_at  bigint       NOT NULL DEFAULT 0,
    deleted_at  bigint       NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_workflow_run_workflow_id_created_at
    ON workflow.workflow_run (workflow_id, created_at) WHERE deleted_at = 0;


-- =============================================================================
-- 模块：mcp（L0）—— MCP 工具接入
-- =============================================================================

CREATE TABLE IF NOT EXISTS mcp.mcp_server (
    id         bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name       varchar(128)  NOT NULL DEFAULT '',
    transport  varchar(32)   NOT NULL DEFAULT '',   -- stdio | sse | http
    endpoint   varchar(512)  NOT NULL DEFAULT '',   -- sse/http 端点
    command    varchar(512)  NOT NULL DEFAULT '',   -- stdio 启动命令
    args       jsonb         NOT NULL DEFAULT '[]', -- stdio 启动参数数组
    enabled    boolean       NOT NULL DEFAULT true,
    status     varchar(32)   NOT NULL DEFAULT '',   -- unknown | connected | error | disabled
    created_at bigint        NOT NULL DEFAULT 0,
    updated_at bigint        NOT NULL DEFAULT 0,
    deleted_at bigint        NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_mcp_server_name
    ON mcp.mcp_server (name) WHERE deleted_at = 0;

-- 从 MCP server 发现的工具。
CREATE TABLE IF NOT EXISTS mcp.mcp_tool (
    id           bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    server_id    bigint        NOT NULL DEFAULT 0,    -- -> mcp.mcp_server.id
    name         varchar(128)  NOT NULL DEFAULT '',
    description  varchar(512)  NOT NULL DEFAULT '',
    input_schema jsonb         NOT NULL DEFAULT '{}', -- 工具入参 JSON Schema
    enabled      boolean       NOT NULL DEFAULT true,
    created_at   bigint        NOT NULL DEFAULT 0,
    updated_at   bigint        NOT NULL DEFAULT 0,
    deleted_at   bigint        NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_mcp_tool_server_id
    ON mcp.mcp_tool (server_id) WHERE deleted_at = 0;
CREATE UNIQUE INDEX IF NOT EXISTS idx_mcp_tool_server_id_name
    ON mcp.mcp_tool (server_id, name) WHERE deleted_at = 0;
