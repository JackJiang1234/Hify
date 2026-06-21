import { computed, ref } from 'vue'

import { modelApi, providerApi } from '@/api/provider'

/** 可供 Agent 选用的 chat 模型选项（跨供应商汇总，仅启用项）。 */
export interface ChatModelOption {
  id: number
  name: string
  providerId: number
  providerName: string
  supportsTools: boolean
  maxOutputTokens: number
}

/**
 * 加载全部供应商下「启用的 chat 模型」，供 Agent 表单选择与列表展示。
 * 一期规模（≤数十供应商）直接全量拉取，按供应商分组并行请求。
 */
export function useChatModels() {
  const models = ref<ChatModelOption[]>([])
  const loading = ref(false)

  async function load(): Promise<void> {
    loading.value = true
    try {
      const providers = await providerApi.list({ page: 1, size: 100 })
      const perProvider = await Promise.all(
        providers.items.map((provider) =>
          modelApi
            .listByProvider(provider.id)
            .then((list) => ({ provider, list }))
            .catch(() => ({ provider, list: [] })),
        ),
      )

      models.value = perProvider.flatMap(({ provider, list }) =>
        list
          .filter((model) => model.modelType === 'chat' && model.enabled)
          .map((model) => ({
            id: model.id,
            name: model.displayName || model.name,
            providerId: provider.id,
            providerName: provider.name,
            supportsTools: model.supportsTools,
            maxOutputTokens: model.maxOutputTokens,
          })),
      )
    } catch {
      // 拦截器已统一提示
    } finally {
      loading.value = false
    }
  }

  /** modelId → 选项，便于列表与表单按 Id 反查名称/能力位。 */
  const byId = computed(() => new Map(models.value.map((model) => [model.id, model])))

  return { models, loading, load, byId }
}
