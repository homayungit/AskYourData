<script setup lang="ts">
import type { ErpDatabase } from '@/types'

defineProps<{ databases: readonly ErpDatabase[] }>()
</script>

<template>
  <div v-if="databases.length" class="flex flex-wrap gap-2 px-4 py-2 bg-gray-50 border-b border-gray-200">
    <span
      v-for="db in databases"
      :key="db.name"
      class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-white border border-gray-200 text-gray-600 shadow-xs"
    >
      <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 flex-shrink-0"></span>
      {{ db.displayName }}
    </span>
  </div>
</template>
