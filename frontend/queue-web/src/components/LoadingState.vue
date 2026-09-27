<script setup lang="ts">
withDefaults(defineProps<{ rows?: number; label?: string }>(), { rows: 3, label: '載入中' })
</script>

<template>
  <div class="flex flex-col items-center justify-center gap-3 py-10 text-slate-500">
    <div class="size-8 animate-spin rounded-full border-2 border-slate-300 border-t-brand-600" />
    <p class="text-sm">{{ label }}…</p>
    <div v-if="rows > 0" class="w-full max-w-md space-y-2">
      <div
        v-for="i in rows"
        :key="i"
        class="h-3 animate-pulse rounded bg-slate-200"
        :style="{ width: `${100 - i * 8}%` }"
      />
    </div>
  </div>
</template>
