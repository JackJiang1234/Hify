import { ref } from 'vue'

import { conversationApi, type ConversationDto } from '@/api/conversation'

/** 会话列表（侧栏）。一期规模小，加载近期 50 条，不做翻页。 */
export function useConversations() {
  const items = ref<ConversationDto[]>([])
  const loading = ref(false)

  async function load(): Promise<void> {
    loading.value = true
    try {
      const result = await conversationApi.list({ page: 1, size: 50 })
      items.value = result.items
    } catch {
      // 拦截器已统一提示
    } finally {
      loading.value = false
    }
  }

  /** 新建会话并置顶到列表。返回新会话；失败返回 null（已提示）。 */
  async function create(agentId: number): Promise<ConversationDto | null> {
    try {
      const conversation = await conversationApi.create(agentId)
      items.value = [conversation, ...items.value]
      return conversation
    } catch {
      return null
    }
  }

  /** 删除会话并从列表移除。成功返回 true。 */
  async function remove(id: number): Promise<boolean> {
    try {
      await conversationApi.remove(id)
      items.value = items.value.filter((item) => item.id !== id)
      return true
    } catch {
      return false
    }
  }

  return { items, loading, load, create, remove }
}
