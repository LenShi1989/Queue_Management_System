<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'

import { ticketApi } from '@/api'
import { useQueueHub, type QueueEventName } from '@/api/queueHub'
import AlertMessage from '@/components/AlertMessage.vue'
import LoadingState from '@/components/LoadingState.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import type { TicketDto, TicketHistoryDto } from '@/types/api'
import { formatDateTime, formatMinutes, todayInTaipei } from '@/utils/format'

const tickets = ref<TicketDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const date = ref(todayInTaipei())
const status = ref('')
const ticketNo = ref('')

const selected = ref<TicketDto | null>(null)
const history = ref<TicketHistoryDto[]>([])
const historyLoading = ref(false)

const hub = useQueueHub()
const unsubscribers: Array<() => void> = []

const STATUS_OPTIONS = [
  { value: '', label: '全部' },
  { value: 'Waiting', label: '等待中' },
  { value: 'Calling', label: '叫號中' },
  { value: 'Serving', label: '服務中' },
  { value: 'Completed', label: '已完成' },
  { value: 'NoShow', label: '已過號' },
  { value: 'Cancelled', label: '已取消' },
  { value: 'Transferred', label: '已轉移' },
]

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    tickets.value = await ticketApi.list({
      date: date.value || undefined,
      status: status.value || undefined,
      ticketNo: ticketNo.value.trim() || undefined,
      pageSize: 100,
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法載入票據'
  } finally {
    loading.value = false
  }
}

async function openDetail(ticket: TicketDto): Promise<void> {
  selected.value = ticket
  historyLoading.value = true
  try {
    history.value = await ticketApi.history(ticket.id)
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法載入歷程'
  } finally {
    historyLoading.value = false
  }
}

onMounted(async () => {
  await load()
  await hub.start()
  for (const event of [
    'QueueCalled',
    'QueueStarted',
    'QueueCompleted',
    'QueueNoShow',
    'QueueCancelled',
    'QueueTransferred',
  ] as QueueEventName[]) {
    unsubscribers.push(hub.on(event, () => void load()))
  }
})

watch([date, status], () => void load())
</script>

<template>
  <div class="space-y-5">
    <header>
      <h1 class="text-xl font-bold text-slate-800">票據查詢</h1>
      <p class="text-sm text-slate-500">依日期與狀態查詢票據，並查看完整操作歷程</p>
    </header>

    <section class="card flex flex-wrap items-end gap-3 p-4">
      <div>
        <label class="label" for="date">日期</label>
        <input id="date" v-model="date" class="input w-40" type="date" />
      </div>
      <div>
        <label class="label" for="status">狀態</label>
        <select id="status" v-model="status" class="input w-40">
          <option v-for="option in STATUS_OPTIONS" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>
      </div>
      <div>
        <label class="label" for="ticketNo">號碼</label>
        <input id="ticketNo" v-model="ticketNo" class="input w-32" placeholder="例如 A001" />
      </div>
      <button class="btn-primary" type="button" @click="load">查詢</button>
      <span class="ml-auto text-sm text-slate-500">共 {{ tickets.length }} 筆</span>
    </section>

    <AlertMessage v-if="error" variant="error">{{ error }}</AlertMessage>

    <section class="card overflow-x-auto p-1">
      <LoadingState v-if="loading" label="查詢中" />
      <table v-else class="w-full min-w-[820px] text-sm">
        <thead class="border-b border-slate-200 text-left text-slate-500">
          <tr>
            <th class="px-3 py-2 font-medium">號碼</th>
            <th class="px-3 py-2 font-medium">服務</th>
            <th class="px-3 py-2 font-medium">狀態</th>
            <th class="px-3 py-2 font-medium">櫃台</th>
            <th class="px-3 py-2 font-medium">順位</th>
            <th class="px-3 py-2 font-medium">等待</th>
            <th class="px-3 py-2 font-medium">服務時間</th>
            <th class="px-3 py-2 font-medium">取號時間</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="ticket in tickets"
            :key="ticket.id"
            class="cursor-pointer border-b border-slate-100 last:border-0 hover:bg-slate-50"
            @click="openDetail(ticket)"
          >
            <td class="px-3 py-2 font-semibold tabular-nums">{{ ticket.ticketNo }}</td>
            <td class="px-3 py-2">{{ ticket.serviceName }}</td>
            <td class="px-3 py-2"><StatusBadge :status="ticket.status" /></td>
            <td class="px-3 py-2">{{ ticket.counterNo ?? '-' }}</td>
            <td class="px-3 py-2 tabular-nums">{{ ticket.position ?? '-' }}</td>
            <td class="px-3 py-2 tabular-nums">{{ formatMinutes(ticket.waitingMinutes) }}</td>
            <td class="px-3 py-2 tabular-nums">{{ formatMinutes(ticket.serviceMinutes) }}</td>
            <td class="px-3 py-2">{{ formatDateTime(ticket.createdAt) }}</td>
          </tr>
          <tr v-if="tickets.length === 0">
            <td colspan="8" class="px-3 py-10 text-center text-slate-400">查無資料</td>
          </tr>
        </tbody>
      </table>
    </section>

    <!-- 歷程詳情 -->
    <div
      v-if="selected"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      @click.self="selected = null"
    >
      <div class="card max-h-[85vh] w-full max-w-2xl overflow-y-auto p-6">
        <div class="flex items-start justify-between">
          <div>
            <h2 class="text-2xl font-bold text-brand-700">{{ selected.ticketNo }}</h2>
            <p class="text-sm text-slate-500">
              {{ selected.serviceName }} ・ <StatusBadge :status="selected.status" />
            </p>
          </div>
          <button class="btn-secondary" type="button" @click="selected = null">關閉</button>
        </div>

        <h3 class="mt-6 mb-3 font-semibold text-slate-700">操作歷程</h3>
        <LoadingState v-if="historyLoading" :rows="2" label="載入歷程" />
        <ol v-else class="space-y-3">
          <li v-for="item in history" :key="item.id" class="flex gap-3">
            <span class="mt-1 size-2 shrink-0 rounded-full bg-brand-500" />
            <div class="text-sm">
              <p class="font-medium text-slate-800">
                {{ item.fromStatus ?? '建立' }} → {{ item.toStatus }}
                <span class="ml-1 text-xs text-slate-500">{{ item.action }}</span>
              </p>
              <p class="text-xs text-slate-500">
                {{ formatDateTime(item.createdAt) }}
                <span v-if="item.operatorName">・{{ item.operatorName }}</span>
                <span v-if="item.counterNo">・{{ item.counterNo }} 號櫃台</span>
                <span v-if="item.remark">・{{ item.remark }}</span>
              </p>
            </div>
          </li>
          <li v-if="history.length === 0" class="text-sm text-slate-400">尚無歷程紀錄</li>
        </ol>
      </div>
    </div>
  </div>
</template>
