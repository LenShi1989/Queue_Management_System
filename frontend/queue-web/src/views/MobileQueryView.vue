<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

import { publicApi } from '@/api'
import { useQueueHub, type QueueEventName } from '@/api/queueHub'
import AlertMessage from '@/components/AlertMessage.vue'
import LoadingState from '@/components/LoadingState.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import type { TicketDto } from '@/types/api'
import { formatFull, formatMinutes } from '@/utils/format'

const route = useRoute()
const hub = useQueueHub()

const ticket = ref<TicketDto | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const history = ref<Array<{ at: string; text: string }>>([])

const unsubscribers: Array<() => void> = []

const token = (): string => (typeof route.params.token === 'string' ? route.params.token : '')

async function load(): Promise<void> {
  const value = token()
  if (!value) {
    error.value = '查詢連結不完整'
    loading.value = false
    return
  }

  loading.value = true
  error.value = null
  try {
    ticket.value = await publicApi.detail(value)
  } catch (err) {
    error.value = err instanceof Error ? err.message : '查詢失敗'
    ticket.value = null
  } finally {
    loading.value = false
  }
}

const STEPS = [
  { status: 'Waiting', text: '已取號，等待叫號' },
  { status: 'Calling', text: '正在叫號，請前往櫃台' },
  { status: 'Serving', text: '服務中' },
  { status: 'Completed', text: '服務完成' },
] as const

const stepIndex = (): number => {
  const status = ticket.value?.status
  if (status === 'NoShow') return 1
  if (status === 'Cancelled' || status === 'Transferred') return -1
  return STEPS.findIndex((s) => s.status === status)
}

onMounted(async () => {
  await load()
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
    unsubscribers.push(
      hub.on(event, (payload) => {
        const data = payload as { ticketId: number; ticketNo: string; status: string }
        if (!ticket.value || data.ticketId !== ticket.value.ticketId) return
        void load()
        history.value.unshift({ at: new Date().toISOString(), text: data.status })
      }),
    )
  }
})
</script>

<template>
  <main class="min-h-screen bg-slate-100 px-4 py-6">
    <div class="mx-auto max-w-md">
      <header class="mb-5 text-center">
        <h1 class="text-xl font-bold text-slate-800">叫號進度查詢</h1>
      </header>

      <LoadingState v-if="loading" label="查詢中" />

      <AlertMessage v-else-if="error" variant="error">{{ error }}</AlertMessage>

      <template v-else-if="ticket">
        <section class="card p-6 text-center">
          <p class="text-sm text-slate-500">{{ ticket.serviceName }}</p>
          <p class="my-3 text-6xl font-bold tracking-tight text-brand-700">{{ ticket.ticketNo }}</p>
          <div class="flex justify-center">
            <StatusBadge :status="ticket.status" />
          </div>

          <!-- 進度條 -->
          <ol v-if="stepIndex() >= 0" class="mt-8 space-y-3 text-left">
            <li
              v-for="(step, i) in STEPS"
              :key="step.status"
              class="flex items-center gap-3"
              :class="i <= stepIndex() ? 'text-slate-800' : 'text-slate-400'"
            >
              <span
                class="flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                :class="
                  i < stepIndex()
                    ? 'bg-emerald-500 text-white'
                    : i === stepIndex()
                      ? 'bg-brand-600 text-white'
                      : 'bg-slate-200 text-slate-500'
                "
              >
                {{ i < stepIndex() ? '✓' : i + 1 }}
              </span>
              <span class="text-sm">{{ step.text }}</span>
            </li>
          </ol>

          <AlertMessage v-else class="mt-6" variant="warning">
            此票號目前狀態為 {{ ticket.status }}，如需 assistance 請洽服務人員。
          </AlertMessage>

          <dl class="mt-6 grid grid-cols-2 gap-3 text-left text-sm">
            <div class="rounded-lg bg-slate-50 p-3">
              <dt class="text-xs text-slate-500">目前順位</dt>
              <dd class="mt-1 text-lg font-semibold">{{ ticket.position ?? '-' }}</dd>
            </div>
            <div class="rounded-lg bg-slate-50 p-3">
              <dt class="text-xs text-slate-500">前面人數</dt>
              <dd class="mt-1 text-lg font-semibold">{{ ticket.peopleAhead ?? '-' }}</dd>
            </div>
            <div class="rounded-lg bg-slate-50 p-3">
              <dt class="text-xs text-slate-500">預估等待</dt>
              <dd class="mt-1 text-lg font-semibold">{{ formatMinutes(ticket.estimatedMinutes) }}</dd>
            </div>
            <div class="rounded-lg bg-slate-50 p-3">
              <dt class="text-xs text-slate-500">取號時間</dt>
              <dd class="mt-1 text-sm font-medium">{{ formatFull(ticket.createdAt) }}</dd>
            </div>
          </dl>

          <p v-if="ticket.counterNo" class="mt-4 text-sm text-slate-600">
            請前往 <span class="font-semibold text-brand-700">{{ ticket.counterNo }}</span> 號櫃台
          </p>
        </section>

        <section v-if="history.length" class="card mt-4 p-5">
          <h2 class="mb-3 text-sm font-semibold text-slate-700">即時動態</h2>
          <ul class="space-y-2 text-sm">
            <li v-for="(item, i) in history" :key="`${item.at}-${i}`" class="flex justify-between">
              <span class="text-slate-600">{{ item.text }}</span>
              <span class="text-xs text-slate-400">{{ formatFull(item.at) }}</span>
            </li>
          </ul>
        </section>

        <button class="btn-secondary mt-4 w-full" type="button" @click="load">重新整理</button>
      </template>
    </div>
  </main>
</template>
