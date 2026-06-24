<script setup lang="ts">
import type { ChatMessage } from '@/types'

const props = defineProps<{ message: ChatMessage }>()

function formatTime(d: Date) {
  return new Date(d).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })
}
</script>

<template>
  <div :class="['flex w-full mb-4', message.role === 'user' ? 'justify-end' : 'justify-start']">

    <!-- Assistant avatar -->
    <div v-if="message.role === 'assistant'"
      class="w-8 h-8 rounded-xl flex items-center justify-center mr-2.5 flex-shrink-0 mt-0.5" style="
        background: linear-gradient(135deg, #0891b2 0%, #6366f1 60%, #8b5cf6 100%);
        box-shadow: 0 4px 16px rgba(6,182,212,0.45), inset 0 1px 0 rgba(255,255,255,0.12);">
      <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M9.75 3.104v5.714a2.25 2.25 0 01-.659 1.591L5 14.5M9.75 3.104c-.251.023-.501.05-.75.082m.75-.082a24.301 24.301 0 014.5 0m0 0v5.714c0 .597.237 1.17.659 1.591L19.8 15.3M14.25 3.104c.251.023.501.05.75.082" />
      </svg>
    </div>

    <!-- Bubble -->
    <div class="max-w-[75%] rounded-2xl px-4 py-3"
      :class="message.role === 'user' ? 'rounded-br-none' : 'rounded-bl-none'"
      :style="message.role === 'user'
        ? `background: linear-gradient(135deg, #0e7490 0%, #4f46e5 50%, #7c3aed 100%);
           color: white;
           box-shadow: 0 4px 24px rgba(6,182,212,0.35), inset 0 1px 0 rgba(255,255,255,0.1);`
        : message.isError
          ? `background: rgba(239,68,68,0.1);
             border: 1px solid rgba(239,68,68,0.25);
             color: #fca5a5;`
          : `background: rgba(15,10,35,0.7);
             border: 1px solid rgba(139,92,246,0.18);
             color: #e2e8f0;
             box-shadow: inset 0 1px 0 rgba(255,255,255,0.03);`
      ">

      <!-- Role label for assistant -->
      <div v-if="message.role === 'assistant' && !message.isError"
        class="flex items-center gap-1.5 mb-2">
        <span class="text-xs font-semibold tracking-wide" style="
          background: linear-gradient(90deg, #22d3ee, #818cf8);
          -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text;">
          AskYourData
        </span>
        <span class="text-xs" style="color: rgba(148,163,184,0.4);">·</span>
        <span class="text-xs" style="color: rgba(148,163,184,0.4);">AI</span>
      </div>

      <!-- Message content -->
      <p class="text-sm leading-relaxed whitespace-pre-wrap">{{ message.content }}</p>

      <!-- Timestamp -->
      <div class="mt-2 flex items-center" :class="message.role === 'user' ? 'justify-end' : 'justify-start'">
        <span class="text-xs" :style="
          message.role === 'user'
            ? 'color: rgba(221,214,254,0.5)'
            : 'color: rgba(100,116,139,0.6)'">
          {{ formatTime(message.timestamp) }}
        </span>
      </div>
    </div>

    <!-- User avatar -->
    <div v-if="message.role === 'user'"
      class="w-8 h-8 rounded-xl flex items-center justify-center ml-2.5 flex-shrink-0 mt-0.5" style="
        background: rgba(139,92,246,0.15);
        border: 1px solid rgba(139,92,246,0.3);">
      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"
        style="color: #a78bfa;">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"/>
      </svg>
    </div>

  </div>
</template>
