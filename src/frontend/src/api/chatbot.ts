import axios from 'axios'
import type { DebugInfo, ErpDatabase } from '@/types'

const apiBase = import.meta.env.VITE_API_BASE_URL
  ?? `http://${window.location.hostname}:5011`

const api = axios.create({
  baseURL: apiBase,
  timeout: 130_000
})

export interface AskResponse {
  answer:       string
  success:      boolean
  errorMessage: string | null
  debug:        DebugInfo | null
}

export async function askQuestion(question: string, sessionId?: string): Promise<AskResponse> {
  const { data } = await api.post<AskResponse>('/api/chatbot/ask', { question, sessionId })
  return data
}

export async function getDatabases(): Promise<ErpDatabase[]> {
  const { data } = await api.get<ErpDatabase[]>('/api/chatbot/databases')
  return Array.isArray(data) ? data : []
}

export async function getStatus(): Promise<Record<string, unknown>> {
  const { data } = await api.get('/api/chatbot/status')
  return data
}
