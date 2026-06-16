import { ref } from 'vue'

import { providerApi, type ProviderDto } from '@/api/provider'

/** Provider 列表的取数逻辑：分页与后端约定一致（page 从 1，size 默认 20） */
export function useProviderList() {
  const rows = ref<ProviderDto[]>([])
  const total = ref(0)
  const page = ref(1)
  const size = ref(20)
  const loading = ref(false)

  async function load() {
    loading.value = true
    try {
      const result = await providerApi.list({ page: page.value, size: size.value })
      rows.value = result.items
      total.value = result.total
    } finally {
      loading.value = false
    }
  }

  return { rows, total, page, size, loading, load }
}
