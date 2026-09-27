<script setup lang="ts">
import { AlertCircle, CheckCircle2, Loader2, TriangleAlert } from 'lucide-vue-next'

withDefaults(
  defineProps<{
    variant?: 'info' | 'success' | 'warning' | 'error'
  }>(),
  { variant: 'info' },
)
</script>

<template>
  <div
    class="flex items-start gap-2 rounded-lg border px-4 py-3 text-sm"
    :class="{
      'border-slate-200 bg-slate-50 text-slate-700': variant === 'info',
      'border-emerald-200 bg-emerald-50 text-emerald-800': variant === 'success',
      'border-amber-200 bg-amber-50 text-amber-800': variant === 'warning',
      'border-rose-200 bg-rose-50 text-rose-800': variant === 'error',
    }"
    role="alert"
  >
    <Loader2 v-if="variant === 'info'" class="mt-0.5 size-4 shrink-0 animate-spin" />
    <CheckCircle2 v-else-if="variant === 'success'" class="mt-0.5 size-4 shrink-0" />
    <TriangleAlert v-else-if="variant === 'warning'" class="mt-0.5 size-4 shrink-0" />
    <AlertCircle v-else class="mt-0.5 size-4 shrink-0" />
    <div class="min-w-0 flex-1">
      <slot />
    </div>
  </div>
</template>
