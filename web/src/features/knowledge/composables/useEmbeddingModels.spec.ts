import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@/api/provider', () => ({
  providerApi: { list: vi.fn() },
  modelApi: { listByProvider: vi.fn() },
}))

import { modelApi, providerApi } from '@/api/provider'
import { useEmbeddingModels } from './useEmbeddingModels'

const listProviders = vi.mocked(providerApi.list)
const listModels = vi.mocked(modelApi.listByProvider)

// 仅 useEmbeddingModels 用到的字段；以 never 旁路严格类型（运行时按真实形状取值）。
function page(items: unknown[]) {
  return { items, total: items.length, page: 1, size: 100 } as never
}

beforeEach(() => {
  vi.clearAllMocks()
})

describe('useEmbeddingModels', () => {
  it('只保留启用且 1536 维的 embedding 模型', async () => {
    listProviders.mockResolvedValue(page([{ id: 1, name: 'OpenAI' }]))
    listModels.mockResolvedValue([
      { id: 10, modelType: 'embedding', enabled: true, embeddingDimensions: 1536, name: 'emb', displayName: '' },
      { id: 11, modelType: 'embedding', enabled: true, embeddingDimensions: 768, name: 'emb768', displayName: '' },
      { id: 12, modelType: 'embedding', enabled: false, embeddingDimensions: 1536, name: 'emboff', displayName: '' },
      { id: 13, modelType: 'chat', enabled: true, embeddingDimensions: 0, name: 'chat', displayName: '' },
    ] as never)

    const { models, load } = useEmbeddingModels()
    await load()

    expect(models.value.map((m) => m.id)).toEqual([10])
    expect(models.value[0].providerName).toBe('OpenAI')
  })

  it('跨供应商汇总，单个供应商拉取失败不影响其余', async () => {
    listProviders.mockResolvedValue(page([{ id: 1, name: 'P1' }, { id: 2, name: 'P2' }]))
    listModels.mockImplementation((providerId: number) =>
      providerId === 1
        ? Promise.reject(new Error('boom'))
        : Promise.resolve([
            { id: 20, modelType: 'embedding', enabled: true, embeddingDimensions: 1536, name: 'ok', displayName: '' },
          ] as never),
    )

    const { models, load } = useEmbeddingModels()
    await load()

    expect(models.value.map((m) => m.id)).toEqual([20])
  })

  it('displayName 优先于 name，byId 可反查', async () => {
    listProviders.mockResolvedValue(page([{ id: 1, name: 'P1' }]))
    listModels.mockResolvedValue([
      { id: 30, modelType: 'embedding', enabled: true, embeddingDimensions: 1536, name: 'raw', displayName: '展示名' },
    ] as never)

    const { models, load, byId } = useEmbeddingModels()
    await load()

    expect(models.value[0].name).toBe('展示名')
    expect(byId.value.get(30)?.name).toBe('展示名')
  })
})
