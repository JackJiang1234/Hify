import { computed, ref } from 'vue'

import { modelApi, providerApi } from '@/api/provider'

/** 向量库固定维度；仅 1536 维嵌入模型可用于知识库（与后端建库校验一致）。 */
export const REQUIRED_EMBEDDING_DIMENSIONS = 1536

/** 可供知识库选用的 embedding 模型选项（跨供应商汇总，仅启用且 1536 维）。 */
export interface EmbeddingModelOption {
  id: number
  name: string
  providerId: number
  providerName: string
  embeddingDimensions: number
}

/**
 * 加载全部供应商下「启用的 1536 维 embedding 模型」，供建库表单选择与列表展示。
 * 与 useChatModels 同构：一期规模直接全量拉取、按供应商并行请求。
 */
export function useEmbeddingModels() {
  const models = ref<EmbeddingModelOption[]>([])
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
          .filter(
            (model) =>
              model.modelType === 'embedding' &&
              model.enabled &&
              model.embeddingDimensions === REQUIRED_EMBEDDING_DIMENSIONS,
          )
          .map((model) => ({
            id: model.id,
            name: model.displayName || model.name,
            providerId: provider.id,
            providerName: provider.name,
            embeddingDimensions: model.embeddingDimensions,
          })),
      )
    } catch {
      // 拦截器已统一提示
    } finally {
      loading.value = false
    }
  }

  /** modelId → 选项，便于列表与表单按 Id 反查名称。 */
  const byId = computed(() => new Map(models.value.map((model) => [model.id, model])))

  return { models, loading, load, byId }
}
