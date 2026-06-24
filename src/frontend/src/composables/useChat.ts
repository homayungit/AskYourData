import { ref, readonly } from 'vue'
import { v4 as uuidv4 } from 'uuid'
import { askQuestion, getDatabases } from '@/api/chatbot'
import type { ChatMessage, ErpDatabase } from '@/types'

export function useChat() {
  const messages  = ref<ChatMessage[]>([])
  const isLoading = ref(false)
  const databases = ref<ErpDatabase[]>([])
  const sessionId = ref<string>(uuidv4())

  async function loadDatabases() {
    try {
      databases.value = await getDatabases()
    } catch {
      // non-critical — keep empty
    }
  }

  async function send(question: string) {
    if (!question.trim() || isLoading.value) return

    // Add user message
    messages.value.push({
      id:        uuidv4(),
      role:      'user',
      content:   question.trim(),
      timestamp: new Date()
    })

    isLoading.value = true

    try {
      const res = await askQuestion(question.trim(), sessionId.value)

      messages.value.push({
        id:        uuidv4(),
        role:      'assistant',
        content:   res.success ? res.answer : (res.errorMessage ?? 'অজানা ত্রুটি'),
        timestamp: new Date(),
        debug:     res.debug ?? undefined,
        isError:   !res.success
      })
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Network error'
      const isTimeout = msg.toLowerCase().includes('timeout')
      messages.value.push({
        id:        uuidv4(),
        role:      'assistant',
        content:   isTimeout
          ? 'The AI model is taking too long to respond. Please try again — it may be busy with indexing.'
          : `Connection failed: ${msg}. Make sure the API server is running on port 5011.`,
        timestamp: new Date(),
        isError:   true
      })
    } finally {
      isLoading.value = false
    }
  }

  function clearHistory() {
    messages.value  = []
    sessionId.value = uuidv4()
  }

  return {
    messages:   readonly(messages),
    isLoading:  readonly(isLoading),
    databases:  readonly(databases),
    send,
    clearHistory,
    loadDatabases
  }
}
