<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

import { counterApi, queryApi } from '@/api'
import { useQueueHub, type QueueEventName } from '@/api/queueHub'
import AlertMessage from '@/components/AlertMessage.vue'
import LoadingState from '@/components/LoadingState.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import type { CounterDto, CurrentCallDto, TodayStatisticsDto } from '@/types/api'
import { formatTime } from '@/utils/format'

const stats = ref<TodayStatisticsDto | null>(null)
const counters = ref<CounterDto[]>([])
const current = ref<CurrentCallDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const hub = useQueueHub()
const unsubscribers: Array<() => void> = []

const cards = computed(() => {
  const s = stats.value
  if (!s) return []
  return [
    { label: '今日取號', value: s.totalTickets, tone: 'text-slate-800' },
    { label: '等待中', value: s.waitingCount + s.callingCount, tone: 'text-amber-600' },
    { label: '已完成', value: s.completedCount, tone: 'text-emerald-600' },
    { label: '平均等待', value: `${Math.round(s.averageWaitingMinutes)} 分`, tone: 'text-brand-700' },
    { label: '平均服務', value: `${Math.round(s.averageServiceMinutes)} 分`, tone: 'text-brand-700' },
    { label: '過號數', value: s.noShowCount, tone: 'text-orange-600' },
  ]
})

async function refresh(): Promise<void> {
  try {
    const [s, c, cur] = await Promise.all([
      queryApi.statistics(),
      counterApi.list(false),
      queryApi.current(),
    ])
    stats.value = s
    counters.value = c
    current.value = cur
    error.value = null
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法載入資料'
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await refresh()
  await hub.start()
  for (const event of [
    'QueueCreated',
    'QueueCalled',
    'QueueRecalled',
    'QueueStarted',
    'QueueCompleted',
    'QueueNoShow',
    'QueueCancelled',
    'QueueTransferred',
  ] as QueueEventName[]) {
    unsubscribers.push(hub.on(event, () => void refresh()))
  }
})

onBeforeUnmount(() => unsubscribers.forEach((fn) => fn()))
</script>

<template>
  <div class="space-y-5">
    <header>
      <h1 class="text-xl font-bold text-slate-800">總覽</h1>
      <p class="text-sm text-slate-500">今日即時排隊狀態</p>
    </header>

    <LoadingState v-if="loading" label="載入中" />
    <AlertMessage v-else-if="error" variant="error">{{ error }}</AlertMessage>

    <template v-else>
      <section class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
        <div v-for="card in cards" :key="card.label" class="card p-4">
          <p class="text-sm text-slate-500">{{ card.label }}</p>
          <p class="mt-2 text-3xl font-bold tabular-nums" :class="card.tone">{{ card.value }}</p>
        </div>
      </section>

      <section class="card p-5">
        <h2 class="mb-4 font-semibold text-slate-700">櫃台狀態</h2>
        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <div
            v-for="counter in counters"
            :key="counter.id"
            class="rounded-lg border border-slate-200 p-3"
          >
            <div class="flex items-center justify-between">
              <span class="font-medium">{{ counter.code }}・{{ counter.name }}</span>
              <StatusBadge :status="counter.status" />
            </div>
            <p class="mt-2 text-2xl font-bold tabular-nums">
              {{ counter.currentTicketNo ?? '--' }}
            </p>
            <p class="text-xs text-slate-500">
              {{ counter.serviceName ?? '未指定服務' }} ・ 等待 {{ counter.waitingCount ?? 0 }} 筆
            </p>
          </div>
        </div>
      </section>

      <section class="card p-5">
        <h2 class="mb-4 font-semibold text-slate-700">目前叫號</h2>
        <div v-if="current.length" class="space-y-2">
          <div
            v-for="item in current"
            :key="item.ticketId"
            class="flex items-center justify-between rounded-lg bg-slate-50 px-4 py-3"
          >
            <span class="text-2xl font-bold tabular-nums text-brand-700">{{ item.ticketNo }}</span>
            <span class="text-sm text-slate-600">
              {{ item.counterNo }} 號櫃台 ・ {{ item.serviceName }} ・ {{ formatTime(item.calledAt) }}
            </span>
          </div>
        </div>
        <p v-else class="py-6 text-center text-slate-400">目前沒有叫號中的票</p>
      </section>

      <section v-if="stats" class="card p-5">
        <h2 class="mb-4 font-semibold text-slate-700">各服務統計</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="border-b border-slate-200 text-left text-slate-500">
              <tr>
                <th class="py-2 pr-4 font-medium">服務</th>
                <th class="py-2 pr-4 font-medium">總數</th>
                <th class="py-2 pr-4 font-medium">等待</th>
                <th class="py-2 pr-4 font-medium">完成</th>
                <th class="py-2 pr-4 font-medium">過號</th>
                <th class="py-2 font-medium">平均服務</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="s in stats.services" :key="s.serviceId" class="border-b border-slate-100 last:border-0">
                <td class="py-2 pr-4">{{ s.serviceName }}</td>
                <td class="py-2 pr-4 tabular-nums">{{ s.total }}</td>
                <td class="py-2 pr-4 tabular-nums">{{ s.waiting }}</td>
                <td class="py-2 pr-4 tabular-nums">{{ s.completed }}</td>
                <td class="py-2 pr-4 tabular-nums">{{ s.noShow }}</td>
                <td class="py-2 tabular-nums">{{ Math.round(s.averageServiceMinutes) }} 分</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>
  </div>
</template>
