<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

import { useQueueHub, type QueueEventName } from '@/api/queueHub'
import { queryApi } from '@/api'
import type { DisplaySnapshot, TicketEventNotification } from '@/types/api'
import { formatTime } from '@/utils/format'

const snapshot = ref<DisplaySnapshot | null>(null)
const highlight = ref<TicketEventNotification | null>(null)
const lastUpdate = ref<string | null>(null)
const now = ref(formatTime(new Date().toISOString()))
const error = ref<string | null>(null)

const hub = useQueueHub()
const connected = hub.isConnected
const unsubscribers: Array<() => void> = []
let highlightTimer: number | null = null
let clockTimer: number | null = null

const counters = computed(() => snapshot.value?.counters ?? [])
const currentCall = computed(() => highlight.value)

async function refresh(): Promise<void> {
  try {
    snapshot.value = await queryApi.display()
    lastUpdate.value = snapshot.value.timestamp
    error.value = null
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法取得顯示資料'
  }
}

function speak(text: string): void {
  if (!('speechSynthesis' in window)) return
  window.speechSynthesis.cancel()
  const utterance = new SpeechSynthesisUtterance(text)
  utterance.lang = 'zh-TW'
  window.speechSynthesis.speak(utterance)
}

function onCall(payload: unknown): void {
  const data = payload as TicketEventNotification
  highlight.value = data
  lastUpdate.value = data.timestamp
  if (data.message) speak(data.message)

  if (highlightTimer !== null) window.clearTimeout(highlightTimer)
  highlightTimer = window.setTimeout(() => {
    highlight.value = null
  }, 15_000)
}

onMounted(async () => {
  await refresh()

  unsubscribers.push(hub.on('DisplayUpdated', (payload) => {
    snapshot.value = payload as DisplaySnapshot
    lastUpdate.value = snapshot.value.timestamp
  }))

  for (const event of ['QueueCalled', 'QueueRecalled'] as QueueEventName[]) {
    unsubscribers.push(hub.on(event, onCall))
  }

  await hub.start()

  // 後端連線建立時會自動加入 display 群組，此處僅更新時鐘
  clockTimer = window.setInterval(() => {
    now.value = formatTime(new Date().toISOString())
  }, 1000)
})

onBeforeUnmount(() => {
  unsubscribers.forEach((fn) => fn())
  if (highlightTimer !== null) window.clearTimeout(highlightTimer)
  if (clockTimer !== null) window.clearInterval(clockTimer)
  window.speechSynthesis?.cancel()
})
</script>

<template>
  <main class="min-h-screen bg-slate-950 text-white">
    <!-- 頁首 -->
    <header class="flex items-center justify-between border-b border-slate-800 px-6 py-4">
      <div>
        <h1 class="text-2xl font-bold tracking-wide">叫號顯示器</h1>
        <p class="text-sm text-slate-400">Queue Management System・即時叫號</p>
      </div>
      <div class="text-right">
        <p class="text-3xl font-bold tabular-nums">{{ now }}</p>
        <p class="mt-1 flex items-center justify-end gap-2 text-xs text-slate-400">
          <span
            class="size-2 rounded-full"
            :class="connected ? 'bg-emerald-400' : 'bg-rose-400'"
          />
          {{ connected ? '即時連線中' : '連線中斷' }}
          <span v-if="lastUpdate">・更新 {{ formatTime(lastUpdate) }}</span>
        </p>
      </div>
    </header>

    <p v-if="error" class="bg-rose-900 px-6 py-2 text-sm text-rose-100">{{ error }}</p>

    <!-- 最新叫號醒目區 -->
    <section
      class="border-b border-slate-800 bg-gradient-to-r from-brand-800 to-brand-950 px-6 py-10 text-center"
    >
      <template v-if="currentCall">
        <p class="text-lg text-brand-200">請 {{ currentCall.ticketNo }} 號前往</p>
        <p class="mt-3 text-8xl font-bold tracking-tight">{{ currentCall.ticketNo }}</p>
        <p class="mt-4 text-3xl text-brand-100">
          {{ currentCall.counterNo ? `${currentCall.counterNo} 號櫃台` : (currentCall.serviceName ?? '') }}
        </p>
      </template>
      <p v-else class="text-3xl text-slate-500">等待叫號</p>
    </section>

    <!-- 服務等待統計 -->
    <section class="grid gap-4 px-6 py-6 sm:grid-cols-2 lg:grid-cols-4">
      <div
        v-for="service in snapshot?.services ?? []"
        :key="service.serviceId"
        class="rounded-xl border border-slate-800 bg-slate-900 p-4"
      >
        <p class="text-sm text-slate-400">{{ service.serviceName }}</p>
        <p class="mt-2 text-4xl font-bold tabular-nums">{{ service.waitingCount }}</p>
        <p class="mt-1 text-xs text-slate-500">
          等待中 ・ 服務中 {{ service.servingCount }}
        </p>
      </div>
    </section>

    <!-- 櫃台狀態 -->
    <section class="px-6 pb-8">
      <h2 class="mb-3 text-lg font-semibold text-slate-300">櫃台狀態</h2>
      <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
        <div
          v-for="counter in counters"
          :key="counter.counterId"
          class="rounded-xl border border-slate-800 bg-slate-900 p-4"
          :class="counter.status === 'Idle' ? '' : 'border-brand-700'"
        >
          <div class="flex items-center justify-between">
            <p class="font-semibold">{{ counter.counterNo }} 號櫃台</p>
            <span
              class="badge"
              :class="{
                'bg-slate-700 text-slate-300': counter.status === 'Idle',
                'bg-brand-600 text-white': counter.status === 'Busy',
                'bg-amber-500 text-white': counter.status === 'Paused',
              }"
            >
              {{ counter.status === 'Idle' ? '空閒' : counter.status === 'Busy' ? '服務中' : counter.status === 'Paused' ? '暫停' : '離線' }}
            </span>
          </div>
          <p class="mt-3 text-4xl font-bold tabular-nums">
            {{ counter.current?.ticketNo ?? '--' }}
          </p>
          <p v-if="counter.current?.calledAt" class="mt-1 text-xs text-slate-500">
            {{ formatTime(counter.current.calledAt) }} 叫號
          </p>
        </div>
      </div>
      <p v-if="counters.length === 0" class="py-10 text-center text-slate-500">尚無櫃台資料</p>
    </section>

    <!-- 最近叫號 -->
    <section v-if="snapshot?.recentCalls?.length" class="px-6 pb-10">
      <h2 class="mb-3 text-lg font-semibold text-slate-300">最近叫號</h2>
      <div class="flex flex-wrap gap-3">
        <div
          v-for="call in snapshot.recentCalls.slice(0, 12)"
          :key="`${call.ticketId}-${call.calledAt}`"
          class="rounded-lg border border-slate-800 bg-slate-900 px-4 py-2"
        >
          <span class="text-2xl font-bold tabular-nums">{{ call.ticketNo }}</span>
          <span class="ml-2 text-xs text-slate-500">{{ formatTime(call.calledAt) }}</span>
        </div>
      </div>
    </section>
  </main>
</template>
