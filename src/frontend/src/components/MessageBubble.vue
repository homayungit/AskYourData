<script setup lang="ts">
import type { ChatMessage } from '@/types'

const props = defineProps<{ message: ChatMessage }>()

function formatTime(d: Date) {
  return new Date(d).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })
}
</script>

<template>
  <div :class="['flex w-full mb-3', message.role === 'user' ? 'justify-end' : 'justify-start']">
    <!-- Assistant avatar -->
    <div v-if="message.role === 'assistant'" class="w-7 h-7 rounded-lg flex items-center justify-center mr-2 flex-shrink-0 mt-1"
         style="background: linear-gradient(135deg, #7c3aed, #4f46e5);">
      <svg class="w-4 h-4 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M9.75 3.104v5.714a2.25 2.25 0 01-.659 1.591L5 14.5M9.75 3.104c-.251.023-.501.05-.75.082m.75-.082a24.301 24.301 0 014.5 0m0 0v5.714c0 .597.237 1.17.659 1.591L19.8 15.3M14.25 3.104c.251.023.501.05.75.082" />
      </svg>
    </div>

    <div :class="['max-w-[75%] rounded-2xl px-4 py-3',
      message.role === 'user' ? 'rounded-br-none' : message.isError ? 'rounded-bl-none' : 'rounded-bl-none'
    ]"
    :style="message.role === 'user'
      ? 'background: linear-gradient(135deg, #7c3aed, #4f46e5); color: white;'
      : message.isError
        ? 'background: rgba(239,68,68,0.15); border: 1px solid rgba(239,68,68,0.3); color: #fca5a5;'
        : 'background: rgba(124,58,237,0.12); border: 1px solid rgba(124,58,237,0.25); color: #e9d5ff;'
    ">
      <!-- Content -->
      <p class="text-sm leading-relaxed whitespace-pre-wrap">{{ message.content }}</p>

      <!-- Footer row -->
      <div class="mt-1.5">
        <span class="text-xs" :style="message.role === 'user' ? 'color: rgba(221,214,254,0.7)' : 'color: rgba(167,139,250,0.6)'">
          {{ formatTime(message.timestamp) }}
        </span>
      </div>
    </div>

    <!-- User avatar -->
    <div v-if="message.role === 'user'" class="w-7 h-7 rounded-lg flex items-center justify-center ml-2 flex-shrink-0 mt-1"
         style="background: rgba(124,58,237,0.3); border: 1px solid rgba(124,58,237,0.4);">
      <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" style="color: #c4b5fd;">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"/>
      </svg>
    </div>
  </div>
</template>
