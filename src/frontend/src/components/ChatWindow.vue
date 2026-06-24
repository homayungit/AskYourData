<script setup lang="ts">
import { ref, nextTick, onMounted, watch } from 'vue'
import MessageBubble from './MessageBubble.vue'
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
  <!-- Background: deep dark with vibrant teal + purple -->
  <div class="min-h-screen flex items-center justify-center p-4" style="
    background: linear-gradient(135deg, #020b18 0%, #030d1a 40%, #080415 100%);
    position: relative; overflow: hidden;">

    <!-- Vivid ambient glows -->
    <div class="fixed pointer-events-none" style="
      top: -5%; left: -8%; width: 55vw; height: 55vw; border-radius: 50%;
      background: radial-gradient(circle, rgba(6,182,212,0.13) 0%, transparent 65%);
      filter: blur(70px);"></div>
    <div class="fixed pointer-events-none" style="
      bottom: -5%; right: -8%; width: 55vw; height: 55vw; border-radius: 50%;
      background: radial-gradient(circle, rgba(139,92,246,0.18) 0%, transparent 65%);
      filter: blur(80px);"></div>
    <div class="fixed pointer-events-none" style="
      top: 50%; left: 50%; transform: translate(-50%,-50%); width: 35vw; height: 35vw; border-radius: 50%;
      background: radial-gradient(circle, rgba(59,130,246,0.08) 0%, transparent 70%);
      filter: blur(60px);"></div>

    <!-- Dot grid -->
    <div class="fixed inset-0 pointer-events-none" style="
      background-image: radial-gradient(rgba(6,182,212,0.07) 1px, transparent 1px);
      background-size: 28px 28px;"></div>

    <!-- Chat card with teal glow border -->
    <div class="w-full max-w-3xl flex flex-col rounded-2xl overflow-hidden relative" style="
      height: 88vh;
      background: linear-gradient(160deg, rgba(5,15,30,0.97) 0%, rgba(8,4,22,0.98) 100%);
      backdrop-filter: blur(24px);
      border: 1px solid rgba(6,182,212,0.22);
      box-shadow:
        0 0 0 1px rgba(6,182,212,0.06),
        0 32px 80px rgba(0,0,0,0.85),
        0 0 80px rgba(6,182,212,0.07),
        0 0 40px rgba(139,92,246,0.06),
        inset 0 1px 0 rgba(255,255,255,0.03);">

      <!-- Glowing top edge — teal to purple -->
      <div class="absolute top-0 left-1/2 -translate-x-1/2 pointer-events-none" style="
        width: 70%; height: 1px;
        background: linear-gradient(90deg, transparent, rgba(6,182,212,0.8), rgba(139,92,246,0.8), transparent);
        box-shadow: 0 0 24px 3px rgba(6,182,212,0.25);"></div>

      <!-- Header -->
      <header class="flex items-center justify-between px-5 py-4 flex-shrink-0" style="
        background: linear-gradient(90deg, rgba(6,182,212,0.1) 0%, rgba(139,92,246,0.08) 60%, rgba(59,130,246,0.05) 100%);
        border-bottom: 1px solid rgba(6,182,212,0.15);">

        <div class="flex items-center gap-3">
          <!-- Logo icon — teal glow -->
          <div class="w-10 h-10 rounded-xl flex items-center justify-center flex-shrink-0" style="
            background: linear-gradient(135deg, #0891b2 0%, #6366f1 60%, #8b5cf6 100%);
            box-shadow: 0 4px 20px rgba(6,182,212,0.5), 0 0 0 1px rgba(6,182,212,0.2), inset 0 1px 0 rgba(255,255,255,0.15);">
            <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8"
                d="M9.75 3.104v5.714a2.25 2.25 0 01-.659 1.591L5 14.5M9.75 3.104c-.251.023-.501.05-.75.082m.75-.082a24.301 24.301 0 014.5 0m0 0v5.714c0 .597.237 1.17.659 1.591L19.8 15.3M14.25 3.104c.251.023.501.05.75.082M19.8 15.3l-1.57.393A9.065 9.065 0 0112 15a9.065 9.065 0 00-6.23-.693L5 14.5m14.8.8l1.402 1.402c1 1 .03 2.798-1.442 2.798H4.24c-1.47 0-2.441-1.799-1.442-2.798L4.2 15.3" />
            </svg>
          </div>

          <div>
            <!-- Teal → purple gradient brand name -->
            <h1 class="text-base font-bold tracking-wide" style="
              background: linear-gradient(90deg, #22d3ee, #818cf8, #a78bfa);
              -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text;">
              AskYourData
            </h1>
            <div class="flex items-center gap-1.5 mt-0.5">
              <span class="w-1.5 h-1.5 rounded-full bg-emerald-400" style="
                box-shadow: 0 0 8px rgba(52,211,153,0.9);
                animation: pulse 2s infinite;"></span>
              <p class="text-xs font-medium" style="color: rgba(148,163,184,0.65);">
                AdventureWorks 2017 · AI Powered
              </p>
            </div>
          </div>
        </div>

        <button
          @click="clearHistory"
          class="text-xs px-3 py-1.5 rounded-lg font-medium transition-all duration-200"
          style="color: rgba(148,163,184,0.6); border: 1px solid rgba(139,92,246,0.2); background: transparent;"
          onmouseover="this.style.background='rgba(239,68,68,0.1)';this.style.color='#fca5a5';this.style.borderColor='rgba(239,68,68,0.35)'"
          onmouseout="this.style.background='transparent';this.style.color='rgba(148,163,184,0.6)';this.style.borderColor='rgba(139,92,246,0.2)'"
        >
          Clear Chat
        </button>
      </header>

      <!-- Database status bar -->
      <div v-if="databases.length" class="flex flex-wrap gap-2 px-4 py-2 flex-shrink-0" style="
        background: rgba(6,182,212,0.04);
        border-bottom: 1px solid rgba(6,182,212,0.1);">
        <span
          v-for="db in databases"
          :key="db.name"
          class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium"
          style="background: rgba(6,182,212,0.1); border: 1px solid rgba(6,182,212,0.25); color: #67e8f9;">
          <span class="w-1.5 h-1.5 rounded-full bg-emerald-400"
            style="box-shadow: 0 0 6px rgba(52,211,153,0.9);"></span>
          {{ db.displayName }}
        </span>
      </div>

      <!-- Messages area -->
      <main class="flex-1 overflow-y-auto px-4 py-5 space-y-1" style="
        scrollbar-width: thin;
        scrollbar-color: rgba(139,92,246,0.2) transparent;">

        <!-- Welcome state -->
        <div v-if="messages.length === 0"
          class="flex flex-col items-center justify-center h-full text-center gap-6">

          <!-- Icon -->
          <div class="w-20 h-20 rounded-2xl flex items-center justify-center" style="
            background: linear-gradient(135deg, rgba(6,182,212,0.15), rgba(139,92,246,0.15));
            border: 1px solid rgba(6,182,212,0.3);
            box-shadow: 0 0 50px rgba(6,182,212,0.12), 0 0 30px rgba(139,92,246,0.1);">
            <svg class="w-10 h-10" fill="none" stroke="currentColor" viewBox="0 0 24 24"
              style="color: #22d3ee;">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
                d="M20.25 6.375c0 2.278-3.694 4.125-8.25 4.125S3.75 8.653 3.75 6.375m16.5 0c0-2.278-3.694-4.125-8.25-4.125S3.75 4.097 3.75 6.375m16.5 0v11.25c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125V6.375m16.5 5.625c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125" />
            </svg>
          </div>

          <div>
            <h2 class="text-xl font-semibold text-white mb-2">What would you like to know?</h2>
            <p class="text-sm" style="color: rgba(148,163,184,0.6);">
              Ask anything about your data in plain English or Bengali
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
              class="px-3 py-1.5 rounded-full text-xs font-medium transition-all duration-200"
              style="background: rgba(6,182,212,0.08); border: 1px solid rgba(6,182,212,0.22); color: #67e8f9;"
              onmouseover="this.style.background='rgba(6,182,212,0.18)';this.style.borderColor='rgba(6,182,212,0.45)';this.style.color='#cffafe'"
              onmouseout="this.style.background='rgba(6,182,212,0.08)';this.style.borderColor='rgba(6,182,212,0.22)';this.style.color='#67e8f9'"
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
            <div class="w-7 h-7 rounded-lg flex items-center justify-center mr-2 flex-shrink-0" style="
              background: linear-gradient(135deg, #7c3aed, #4f46e5);
              box-shadow: 0 4px 12px rgba(124,58,237,0.4);">
              <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M9.75 3.104v5.714a2.25 2.25 0 01-.659 1.591L5 14.5M9.75 3.104c-.251.023-.501.05-.75.082m.75-.082a24.301 24.301 0 014.5 0m0 0v5.714c0 .597.237 1.17.659 1.591L19.8 15.3M14.25 3.104c.251.023.501.05.75.082" />
              </svg>
            </div>
            <div class="px-4 py-3 rounded-2xl rounded-bl-none" style="
              background: rgba(139,92,246,0.1);
              border: 1px solid rgba(139,92,246,0.2);">
              <div class="flex gap-1.5 items-center h-5">
                <span v-for="i in 3" :key="i"
                  class="w-2 h-2 rounded-full animate-bounce"
                  :style="`background: #a78bfa; animation-delay: ${(i-1)*0.15}s`"></span>
              </div>
            </div>
          </div>
        </template>

        <div ref="messagesEnd"></div>
      </main>

      <!-- Developer credit -->
      <div class="text-center py-2 flex-shrink-0" style="
        background: linear-gradient(90deg, transparent, rgba(6,182,212,0.07), rgba(139,92,246,0.07), transparent);
        border-top: 1px solid rgba(6,182,212,0.12);">
        <span class="text-xs tracking-widest uppercase" style="color: rgba(148,163,184,0.4); letter-spacing: 0.12em;">
          crafted by
          <span class="font-semibold" style="
            background: linear-gradient(90deg, #fbbf24, #f59e0b);
            -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text;">
            Homayun Kabir
          </span>
        </span>
      </div>

      <!-- Input area -->
      <footer class="px-4 py-3 flex-shrink-0" style="
        background: rgba(3,8,18,0.7);
        border-top: 1px solid rgba(6,182,212,0.1);">
        <div class="flex gap-2 items-end">
          <textarea
            v-model="input"
            @keydown="handleKeydown"
            :disabled="isLoading"
            rows="1"
            placeholder="Ask a question… (Enter to send, Shift+Enter for new line)"
            class="flex-1 resize-none rounded-xl px-4 py-3 text-sm transition-all duration-200"
            style="
              background: rgba(6,182,212,0.06);
              border: 1px solid rgba(6,182,212,0.18);
              color: #e2e8f0;
              max-height: 8rem;
              overflow-y: auto;
              field-sizing: content;
              outline: none;"
            onfocus="this.style.borderColor='rgba(6,182,212,0.5)';this.style.background='rgba(6,182,212,0.1)';this.style.boxShadow='0 0 0 3px rgba(6,182,212,0.08)'"
            onblur="this.style.borderColor='rgba(6,182,212,0.18)';this.style.background='rgba(6,182,212,0.06)';this.style.boxShadow='none'"
          />
          <button
            @click="handleSend"
            :disabled="!input.trim() || isLoading"
            class="flex-shrink-0 w-11 h-11 rounded-xl flex items-center justify-center transition-all duration-200"
            :style="(!input.trim() || isLoading)
              ? 'opacity:0.35; cursor:not-allowed; background: linear-gradient(135deg, #0891b2, #6366f1);'
              : 'opacity:1; cursor:pointer; background: linear-gradient(135deg, #06b6d4, #6366f1); box-shadow: 0 4px 20px rgba(6,182,212,0.45);'"
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
