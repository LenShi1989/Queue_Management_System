<script setup lang="ts">
import { computed } from 'vue'
import type { CounterStatus, TicketStatus } from '@/types/api'

const props = defineProps<{
  status: TicketStatus | CounterStatus | string
}>()

const MAP: Record<string, { label: string; cls: string }> = {
  Waiting: { label: '等待中', cls: 'bg-slate-100 text-slate-700' },
  Calling: { label: '叫號中', cls: 'bg-amber-100 text-amber-800' },
  Serving: { label: '服務中', cls: 'bg-brand-100 text-brand-800' },
  Completed: { label: '已完成', cls: 'bg-emerald-100 text-emerald-800' },
  NoShow: { label: '已過號', cls: 'bg-orange-100 text-orange-800' },
  Cancelled: { label: '已取消', cls: 'bg-rose-100 text-rose-800' },
  Transferred: { label: '已轉移', cls: 'bg-violet-100 text-violet-800' },
  Suspended: { label: '暫停中', cls: 'bg-slate-200 text-slate-700' },
  Idle: { label: '空閒', cls: 'bg-slate-100 text-slate-600' },
  Busy: { label: '服務中', cls: 'bg-brand-100 text-brand-800' },
  Paused: { label: '已暫停', cls: 'bg-amber-100 text-amber-800' },
  Offline: { label: '離線', cls: 'bg-slate-200 text-slate-600' },
}

const entry = computed(() => MAP[props.status] ?? { label: props.status, cls: 'bg-slate-100 text-slate-700' })
</script>

<template>
  <span class="badge" :class="entry.cls">{{ entry.label }}</span>
</template>
