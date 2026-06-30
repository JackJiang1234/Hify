import { computed, ref } from 'vue'

import { mcpApi } from '@/api/mcp'

/** 可供 Agent 绑定的 MCP 工具选项（跨 Server 汇总，仅服务端仍提供的 available 工具）。 */
export interface McpToolOption {
  id: number
  name: string
  serverId: number
  serverName: string
  /** 管理员是否启用（停用项仍可绑定，运行时不生效，下拉里标注）。 */
  enabled: boolean
}

/**
 * 加载全部 MCP Server 下「可用工具」，供 Agent 表单选择。
 * 一期规模（≤数十 Server）直接全量拉取、按 Server 并行请求；单个 Server 失败不影响其余。
 */
export function useMcpTools() {
  const tools = ref<McpToolOption[]>([])
  const loading = ref(false)

  async function load(): Promise<void> {
    loading.value = true
    try {
      const servers = await mcpApi.list({ page: 1, size: 100 })
      const perServer = await Promise.all(
        servers.items.map((server) =>
          mcpApi
            .listTools(server.id)
            .then((list) => ({ server, list }))
            .catch(() => ({ server, list: [] })),
        ),
      )

      tools.value = perServer.flatMap(({ server, list }) =>
        list
          .filter((tool) => tool.available)
          .map((tool) => ({
            id: tool.id,
            name: tool.name,
            serverId: server.id,
            serverName: server.name,
            enabled: tool.enabled,
          })),
      )
    } catch {
      // 拦截器已统一提示
    } finally {
      loading.value = false
    }
  }

  /** 按 Server 分组，供 el-select option-group 展示。 */
  const groups = computed(() => {
    const grouped = new Map<string, McpToolOption[]>()
    for (const tool of tools.value) {
      const items = grouped.get(tool.serverName) ?? []
      items.push(tool)
      grouped.set(tool.serverName, items)
    }
    return [...grouped.entries()].map(([serverName, items]) => ({ serverName, items }))
  })

  /** toolId → 选项，便于按 Id 反查名称。 */
  const byId = computed(() => new Map(tools.value.map((tool) => [tool.id, tool])))

  return { tools, loading, load, groups, byId }
}
