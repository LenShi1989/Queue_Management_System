<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'

import { counterApi, queryApi } from '@/api'
import { useQueueHub, type QueueEventName } from '@/api/queueHub'
import { ApiError } from '@/api/http'
import AlertMessage from '@/components/AlertMessage.vue'
import LoadingState from '@/components/LoadingState.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { useAuthStore } from '@/stores/auth'
import type { CallNextResultDto, CounterDto, TicketEventNotification, TicketStatus, TicketStatusDto } from '@/types/api'

interface CurrentTicketView {
  ticketId: number
  ticketNo: string
  status: TicketStatus
  calledAt: string | null
}

const auth = useAuthStore()
const hub = useQueueHub()

const counters = ref<CounterDto[]>([])
const currentCounterId = ref<number | null>(null)
const current = ref<CurrentTicketView | null>(null)
const waitingCount = ref(0)
const loading = ref(true)
const busy = ref(false)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const transferTarget = ref<number | null>(null)
const showTransfer = ref(false)

const unsubscribers: Array<() => void> = []

const currentCounter = computed(
  () => counters.value.find((c) => c.id === currentCounterId.value) ?? null,
)

const otherCounters = computed(() =>
  counters.value.filter((c) => c.id !== currentCounterId.value && c.status !== 'Offline'),
)

const canAct = computed(
  () => Boolean(currentCounterId.value) && !busy.value && currentCounter.value?.status !== 'Paused',
)

function clearMessages(): void {
  error.value = null
  notice.value = null
}

async function loadCounters(): Promise<void> {
  loading.value = true
  try {
    const list = await counterApi.list(false)
    counters.value = list
    if (currentCounterId.value === null) {
      // 櫃台帳號優先綁定自己的櫃台
      currentCounterId.value = auth.user?.counterId ?? list.find((c) => c.status === 'Idle')?.id ?? list[0]?.id ?? null
    }
    if (currentCounterId.value && currentCounterId.value !== auth.user?.counterId) {
      currentCounterId.value = auth.user?.counterId ?? currentCounterId.value
    }
    await refreshCurrent()
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法載入櫃台資料'
  } finally {
    loading.value = false
  }
}

async function refreshCurrent(): Promise<void> {
  if (!currentCounterId.value) {
    current.value = null
    waitingCount.value = 0
    return
  }

  try {
    const status = await counterApi.status(currentCounterId.value)
    current.value = status.currentTicketId
      ? {
          ticketId: status.currentTicketId,
          ticketNo: status.currentTicketNo ?? '',
          status: (status.currentTicketStatus ?? 'Calling') as TicketStatus,
          calledAt: null,
        }
      : null

    const waiting = await queryApi.waiting({ serviceId: currentCounter.value?.serviceId ?? undefined })
    waitingCount.value = waiting.length
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法取得目前服務狀態'
  }
}

async function act(
  label: string,
  task: () => Promise<TicketStatusDto | CallNextResultDto>,
  successMessage: string,
): Promise<void> {
  if (!currentCounterId.value || busy.value) return

  busy.value = true
  clearMessages()
  try {
    const result = await task()
    notice.value = `${successMessage}：${result.ticketNo}`
    current.value = null
    await refreshCurrent()
    await reloadCountersOnly()
  } catch (err) {
    // QUEUE_EMPTY 屬於正常回饋，不當成錯誤
    if (err instanceof ApiError && err.code === 'QUEUE_EMPTY') {
      notice.value = '目前沒有等待中的號碼'
    } else {
      error.value = err instanceof Error ? err.message : `${label}失敗`
    }
  } finally {
    busy.value = false
  }
}

const callNext = (): Promise<void> =>
  act('叫號', () => counterApi.callNext(currentCounterId.value!), '已叫號')

const recall = (): Promise<void> =>
  act('再叫', () => counterApi.recall(currentCounterId.value!), '已重新叫號')

const start = (): Promise<void> =>
  act('開始服務', () => counterApi.start(currentCounterId.value!), '已開始服務')

const complete = (): Promise<void> =>
  act('完成服務', () => counterApi.complete(currentCounterId.value!), '已完成服務')

const noShow = (): Promise<void> =>
  act('過號', () => counterApi.noShow(currentCounterId.value!), '已標記為過號')

async function transfer(): Promise<void> {
  if (!transferTarget.value) return
  const target = transferTarget.value
  showTransfer.value = false
  await act('轉移', () => counterApi.transfer(currentCounterId.value!, { targetCounterId: target }), '已轉移')
}

async function togglePause(): Promise<void> {
  if (!currentCounterId.value || busy.value) return
  busy.value = true
  clearMessages()
  try {
    const pausing = currentCounter.value?.status !== 'Paused'
    const result = await counterApi.pause(currentCounterId.value, pausing)
    notice.value = pausing ? '櫃台已暫停叫號' : '櫃台已恢復叫號'
    Object.assign(currentCounter.value!, { status: result.status })
  } catch (err) {
    error.value = err instanceof Error ? err.message : '操作失敗'
  } finally {
    busy.value = false
  }
}

async function reloadCountersOnly(): Promise<void> {
  try {
    counters.value = await counterApi.list(false)
  } catch {
    // 背景更新失敗不中斷操作流程
  }
}

function onEvent(event: QueueEventName, payload: unknown): void {
  const data = payload as TicketEventNotification
  if (data.counterId !== currentCounterId.value && event !== 'QueueCreated') return
  void refreshCurrent()
  void reloadCountersOnly()
}

onMounted(async () => {
  await loadCounters()
  await hub.start()
  for (const event of [
    'QueueCalled',
    'QueueRecalled',
    'QueueStarted',
    'QueueCompleted',
    'QueueNoShow',
    'QueueTransferred',
    'QueueCancelled',
    'QueueCreated',
  ] as QueueEventName[]) {
    unsubscribers.push(hub.on(event, (payload) => onEvent(event, payload)))
  }
})

onBeforeUnmount(() => unsubscribers.forEach((fn) => fn()))

watch(currentCounterId, async () => {
  clearMessages()
  current.value = null
  await refreshCurrent()
})
</script>

<template>
  <div class="space-y-5">
    <header class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h1 class="text-xl font-bold text-slate-800">櫃台叫號</h1>
        <p class="text-sm text-slate-500">下一號、再叫、開始、完成、過號、轉移</p>
      </div>
      <label class="flex items-center gap-2 text-sm">
        <span class="font-medium text-slate-600">選擇櫃台</span>
        <select v-model.number="currentCounterId" class="input w-48">
          <option v-for="counter in counters" :key="counter.id" :value="counter.id">
            {{ counter.code }}・{{ counter.name }}（{{ counter.serviceName ?? '未指定服務' }}）
          </option>
        </select>
      </label>
    </header>

    <LoadingState v-if="loading" label="載入櫃台" />

    <template v-else>
      <AlertMessage v-if="error" variant="error">{{ error }}</AlertMessage>
      <AlertMessage v-if="notice" variant="info">{{ notice }}</AlertMessage>
      <AlertMessage v-if="currentCounter?.status === 'Paused'" variant="warning">
        此櫃台已暫停叫號，按「恢復叫號」後才能繼續操作。
      </AlertMessage>

      <!-- 目前服務中 -->
      <section class="card p-6">
        <div class="flex items-center justify-between">
          <h2 class="font-semibold text-slate-700">目前服務</h2>
          <StatusBadge v-if="currentCounter" :status="currentCounter.status" />
        </div>

        <div v-if="current" class="mt-5 text-center">
          <p class="text-6xl font-bold tracking-tight text-brand-700">{{ current.ticketNo }}</p>
          <div class="mt-3 flex justify-center">
            <StatusBadge :status="current.status" />
          </div>
          <p class="mt-3 flex justify-center gap-2">
            <span v-if="currentCounter" class="text-sm text-slate-500">
              {{ currentCounter.serviceName ?? '未指定服務' }}
            </span>
          </p>
        </div>

        <p v-else class="mt-8 text-center text-slate-400">目前沒有服務中的票據</p>
      </section>

      <!-- 操作區 -->
      <section class="card p-6">
        <h2 class="mb-4 font-semibold text-slate-700">叫號操作</h2>

        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <button class="btn-primary btn-lg" type="button" :disabled="!canAct" @click="callNext">
            下一號
          </button>
          <button
            class="btn-secondary btn-lg"
            type="button"
            :disabled="!canAct || current?.status !== 'Calling'"
            @click="recall"
          >
            再叫一次
          </button>
          <button
            class="btn-success btn-lg"
            type="button"
            :disabled="!canAct || current?.status !== 'Calling'"
            @click="start"
          >
            開始服務
          </button>
          <button
            class="btn-success btn-lg"
            type="button"
            :disabled="!canAct || current?.status !== 'Serving'"
            @click="complete"
          >
            完成服務
          </button>
          <button
            class="btn-warning btn-lg"
            type="button"
            :disabled="!canAct || current?.status !== 'Calling'"
            @click="noShow"
          >
            過號
          </button>
          <button
            class="btn-secondary btn-lg"
            type="button"
            :disabled="!canAct || current?.status !== 'Serving'"
            @click="showTransfer = true"
          >
            轉移
          </button>
        </div>

        <div class="mt-4 border-t border-slate-100 pt-4">
          <button class="btn-secondary" type="button" :disabled="!currentCounterId || busy" @click="togglePause">
            {{ currentCounter?.status === 'Paused' ? '恢復叫號' : '暫停叫號' }}
          </button>
          <p class="mt-3 text-sm text-slate-500">
            等待中：
            <span class="font-semibold text-slate-700">{{ waitingCount }}</span> 筆
          </p>
        </div>
      </section>

      <!-- 轉移對話框 -->
      <div
        v-if="showTransfer"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
        @click.self="showTransfer = false"
      >
        <div class="card w-full max-w-md p-6">
          <h3 class="text-lg font-semibold">轉移至其他櫃台</h3>
          <p class="mt-1 text-sm text-slate-500">
            轉移後票號將回到目標櫃台的等待佇列。
          </p>
          <select v-model.number="transferTarget" class="input mt-4">
            <option :value="null" disabled>請選擇目標櫃台</option>
            <option v-for="counter in otherCounters" :key="counter.id" :value="counter.id">
              {{ counter.code }}・{{ counter.name }}（{{ counter.serviceName ?? '未指定服務' }}）
            </option>
          </select>
          <div class="mt-5 flex justify-end gap-2">
            <button class="btn-secondary" type="button" @click="showTransfer = false">取消</button>
            <button class="btn-primary" type="button" :disabled="!transferTarget" @click="transfer">
              確認轉移
            </button>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
