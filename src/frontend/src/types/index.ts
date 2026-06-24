export interface DebugInfo {
  databaseUsed:   string
  tableUsed:      string
  generatedSql:   string
  rowsReturned:   number
  executionMs:    number
  routingReason:  string
}

export interface ChatMessage {
  id:        string
  role:      'user' | 'assistant'
  content:   string
  timestamp: Date
  debug?:    DebugInfo
  isError?:  boolean
}

export interface ErpDatabase {
  name:        string
  displayName: string
  topics:      readonly string[]
}
