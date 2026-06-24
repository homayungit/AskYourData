<script setup lang="ts">
import { ref, nextTick, onMounted, watch } from 'vue'
import MessageBubble from './MessageBubble.vue'
import DatabaseStatusBar from './DatabaseStatusBar.vue'
import { useChat } from '@/composables/useChat'

const { messages, isLoading, databases, send, clearHistory, loadDatabases } = useChat()

const input       = ref('')
const messagesEnd = ref<HTMLDivElement | null>(null)

onMounted(() => loadDatabases())

watch(messages, async () => {
  await nextTick()
  messagesEnd.value?.scrollIntoView({ behavior: 'smooth' })
}, { deep: true })

async function handleSend() {
  const q = input.value.trim()
  if (!q || isLoading.value) return
  input.value = ''
  await send(q)
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    handleSend()
  }
}
</script>

<template>
  <!-- Page background — AI gradient -->
  <div class="min-h-screen flex items-center justify-center p-4"
       style="background: linear-gradient(135deg, #0f0c29 0%, #302b63 50%, #24243e 100%);">

    <!-- Floating orbs for depth -->
    <div class="fixed top-20 left-20 w-64 h-64 rounded-full opacity-20 blur-3xl pointer-events-none"
         style="background: radial-gradient(circle, #7c3aed, transparent)"></div>
    <div class="fixed bottom-20 right-20 w-80 h-80 rounded-full opacity-15 blur-3xl pointer-events-none"
         style="background: radial-gradient(circle, #4f46e5, transparent)"></div>

    <!-- Chat card -->
    <div class="w-full max-w-3xl flex flex-col rounded-2xl overflow-hidden shadow-2xl"
         style="height: 88vh; background: rgba(15, 12, 41, 0.85); backdrop-filter: blur(20px);
                border: 1px solid rgba(124, 58, 237, 0.3);">

      <!-- Header -->
      <header class="flex items-center justify-between px-5 py-4 flex-shrink-0"
              style="background: linear-gradient(90deg, rgba(124,58,237,0.25), rgba(79,70,229,0.15));
                     border-bottom: 1px solid rgba(124,58,237,0.25);">
        <div class="flex items-center gap-3">
          <!-- AI brain icon -->
          <div class="w-9 h-9 rounded-xl flex items-center justify-center flex-shrink-0"
               style="background: linear-gradient(135deg, #7c3aed, #4f46e5);">
            <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8"
                d="M9.75 3.104v5.714a2.25 2.25 0 01-.659 1.591L5 14.5M9.75 3.104c-.251.023-.501.05-.75.082m.75-.082a24.301 24.301 0 014.5 0m0 0v5.714c0 .597.237 1.17.659 1.591L19.8 15.3M14.25 3.104c.251.023.501.05.75.082M19.8 15.3l-1.57.393A9.065 9.065 0 0112 15a9.065 9.065 0 00-6.23-.693L5 14.5m14.8.8l1.402 1.402c1 1 .03 2.798-1.442 2.798H4.24c-1.47 0-2.441-1.799-1.442-2.798L4.2 15.3" />
            </svg>
          </div>
          <div>
            <h1 class="text-sm font-bold text-white tracking-wide">AskYourData</h1>
            <div class="flex items-center gap-1.5 mt-0.5">
              <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
              <p class="text-xs" style="color: rgba(196,181,253,0.8)">AdventureWorks 2017 · AI Powered</p>
            </div>
          </div>
        </div>
        <button
          @click="clearHistory"
          class="text-xs px-3 py-1.5 rounded-lg transition-all"
          style="color: rgba(196,181,253,0.7); border: 1px solid rgba(124,58,237,0.3);"
          onmouseover="this.style.background='rgba(239,68,68,0.15)';this.style.color='#fca5a5';this.style.borderColor='rgba(239,68,68,0.4)'"
          onmouseout="this.style.background='';this.style.color='rgba(196,181,253,0.7)';this.style.borderColor='rgba(124,58,237,0.3)'"
        >
          Clear Chat
        </button>
      </header>

      <!-- Database status bar -->
      <div v-if="databases.length" class="flex flex-wrap gap-2 px-4 py-2 flex-shrink-0"
           style="background: rgba(79,70,229,0.08); border-bottom: 1px solid rgba(124,58,237,0.15);">
        <span
          v-for="db in databases"
          :key="db.name"
          class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium"
          style="background: rgba(124,58,237,0.15); border: 1px solid rgba(124,58,237,0.3); color: #c4b5fd;"
        >
          <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
          {{ db.displayName }}
        </span>
      </div>

      <!-- Messages -->
      <main class="flex-1 overflow-y-auto px-4 py-5 space-y-1"
            style="scrollbar-width: thin; scrollbar-color: rgba(124,58,237,0.3) transparent;">

        <!-- Welcome state -->
        <div v-if="messages.length === 0" class="flex flex-col items-center justify-center h-full text-center gap-5">
          <div class="w-20 h-20 rounded-2xl flex items-center justify-center"
               style="background: linear-gradient(135deg, rgba(124,58,237,0.3), rgba(79,70,229,0.2));
                      border: 1px solid rgba(124,58,237,0.4);">
            <svg class="w-10 h-10" fill="none" stroke="currentColor" viewBox="0 0 24 24"
                 style="color: #a78bfa;">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
                d="M20.25 6.375c0 2.278-3.694 4.125-8.25 4.125S3.75 8.653 3.75 6.375m16.5 0c0-2.278-3.694-4.125-8.25-4.125S3.75 4.097 3.75 6.375m16.5 0v11.25c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125V6.375m16.5 5.625c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125" />
            </svg>
          </div>
          <div>
            <h2 class="text-lg font-semibold text-white mb-1">What would you like to know?</h2>
            <p class="text-sm" style="color: rgba(196,181,253,0.65)">
              Ask anything about AdventureWorks data in English or Bengali
            </p>
          </div>
          <!-- Hint chips -->
          <div class="flex flex-wrap justify-center gap-2 max-w-lg">
            <button
              v-for="hint in [
                'How many employees are there?',
                'Top 5 best-selling products?',
                'Total sales revenue this year?',
                'List all departments',
                'Which vendor supplies the most?'
              ]"
              :key="hint"
              @click="send(hint)"
              class="px-3 py-1.5 rounded-full text-xs font-medium transition-all"
              style="background: rgba(124,58,237,0.15); border: 1px solid rgba(124,58,237,0.35); color: #c4b5fd;"
              onmouseover="this.style.background='rgba(124,58,237,0.3)';this.style.borderColor='rgba(167,139,250,0.6)';this.style.color='#ede9fe'"
              onmouseout="this.style.background='rgba(124,58,237,0.15)';this.style.borderColor='rgba(124,58,237,0.35)';this.style.color='#c4b5fd'"
            >
              {{ hint }}
            </button>
          </div>
        </div>

        <!-- Message list -->
        <template v-else>
          <MessageBubble
            v-for="msg in messages"
            :key="msg.id"
            :message="msg"
          />

          <!-- Typing indicator -->
          <div v-if="isLoading" class="flex justify-start mb-4">
            <div class="px-4 py-3 rounded-2xl rounded-bl-none"
                 style="background: rgba(124,58,237,0.15); border: 1px solid rgba(124,58,237,0.25);">
              <div class="flex gap-1.5 items-center h-5">
                <span v-for="i in 3" :key="i" :style="`animation-delay: ${(i-1)*0.15}s`"
                  class="w-2 h-2 rounded-full animate-bounce"
                  style="background: #a78bfa;"></span>
              </div>
            </div>
          </div>
        </template>

        <div ref="messagesEnd"></div>
      </main>

      <!-- Developer credit -->
      <div class="text-center py-1.5 flex-shrink-0"
           style="background: linear-gradient(90deg, rgba(124,58,237,0.3), rgba(79,70,229,0.3), rgba(124,58,237,0.3));
                  border-top: 1px solid rgba(124,58,237,0.2);">
        <span class="text-xs font-medium tracking-wide" style="color: rgba(196,181,253,0.8);">
          ✦ Developed by <span class="font-bold" style="color: #fbbf24;">Homayun Kabir</span> ✦
        </span>
      </div>

      <!-- Input area -->
      <footer class="px-4 py-3 flex-shrink-0"
              style="background: rgba(15,12,41,0.6); border-top: 1px solid rgba(124,58,237,0.2);">
        <div class="flex gap-2 items-end">
          <textarea
            v-model="input"
            @keydown="handleKeydown"
            :disabled="isLoading"
            rows="1"
            placeholder="Ask a question… (Enter to send, Shift+Enter for new line)"
            class="flex-1 resize-none rounded-xl px-4 py-3 text-sm transition-all"
            style="background: rgba(124,58,237,0.1); border: 1px solid rgba(124,58,237,0.3);
                   color: #e9d5ff; max-height: 8rem; overflow-y: auto; field-sizing: content;
                   outline: none;"
            onfocus="this.style.borderColor='rgba(167,139,250,0.7)';this.style.boxShadow='0 0 0 3px rgba(124,58,237,0.15)'"
            onblur="this.style.borderColor='rgba(124,58,237,0.3)';this.style.boxShadow='none'"
          />
          <button
            @click="handleSend"
            :disabled="!input.trim() || isLoading"
            class="flex-shrink-0 w-11 h-11 rounded-xl flex items-center justify-center transition-all"
            style="background: linear-gradient(135deg, #7c3aed, #4f46e5);"
            :style="(!input.trim() || isLoading) ? 'opacity:0.4; cursor:not-allowed' : 'opacity:1; cursor:pointer'"
            aria-label="Send"
          >
            <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8"/>
            </svg>
          </button>
        </div>
      </footer>

    </div>
  </div>
</template>
