<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

import { serviceApi, ticketApi } from '@/api'
import AlertMessage from '@/components/AlertMessage.vue'
import LoadingState from '@/components/LoadingState.vue'
import QrCode from '@/components/QrCode.vue'
import { useQueueHub } from '@/api/queueHub'
import type { ServiceDto, TicketDto } from '@/types/api'
import { formatMinutes } from '@/utils/format'

const services = ref<ServiceDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const submitting = ref(false)
const selected = ref<ServiceDto | null>(null)
const result = ref<TicketDto | null>(null)

const RESET_SECONDS = 15
const secondsLeft = ref(RESET_SECONDS)
let countdown: number | null = null
let resetTimer: number | null = null

const hub = useQueueHub()

const canSubmit = computed(() => Boolean(selected.value) && !submitting.value)

async function loadServices(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const list = await serviceApi.list(false)
    services.value = list.filter((s) => s.isActive)
    if (!selected.value && services.value.length > 0) {
      selected.value = services.value[0]
    }
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法載入服務類型'
  } finally {
    loading.value = false
  }
}

async function takeTicket(): Promise<void> {
  if (!selected.value || submitting.value) return

  submitting.value = true
  error.value = null
  try {
    result.value = await ticketApi.create({ serviceId: selected.value.id })
    startResetCountdown()
  } catch (err) {
    error.value = err instanceof Error ? err.message : '取號失敗，請稍後再試'
  } finally {
    submitting.value = false
  }
}

function startResetCountdown(): void {
  secondsLeft.value = RESET_SECONDS
  stopTimers()

  countdown = window.setInterval(() => {
    secondsLeft.value -= 1
    if (secondsLeft.value <= 0) {
      stopTimers()
      result.value = null
    }
  }, 1000)

  // 逾時自動重置畫面
  resetTimer = window.setTimeout(() => {
    stopTimers()
    result.value = null
  }, RESET_SECONDS * 1000)
}

function stopTimers(): void {
  if (countdown !== null) {
    window.clearInterval(countdown)
    countdown = null
  }
  if (resetTimer !== null) {
    window.clearTimeout(resetTimer)
    resetTimer = null
  }
}

function reset(): void {
  stopTimers()
  result.value = null
  secondsLeft.value = RESET_SECONDS
  void loadServices()
}

onMounted(async () => {
  await loadServices()
  void hub.start()
})

onBeforeUnmount(stopTimers)
</script>

<template>
  <main class="min-h-screen bg-gradient-to-b from-brand-700 to-brand-900 px-4 py-8 text-white">
    <div class="mx-auto max-w-3xl">
      <header class="mb-8 text-center">
        <h1 class="text-3xl font-bold">自助取號</h1>
        <p class="mt-2 text-brand-200">請選擇服務類型後取號</p>
      </header>

      <LoadingState v-if="loading" label="載入服務類型" class="text-brand-100" />

      <template v-else>
        <!-- 取號結果 -->
        <div v-if="result" class="card p-8 text-center text-slate-800">
          <p class="text-sm text-slate-500">您的號碼</p>
          <p class="my-4 text-7xl font-bold tracking-tight text-brand-700">{{ result.ticketNo }}</p>
          <p class="text-lg text-slate-700">{{ result.serviceName }}</p>

          <dl class="mx-auto mt-6 grid max-w-md grid-cols-3 gap-3 text-center">
            <div class="rounded-lg bg-slate-50 p-3">
              <dt class="text-xs text-slate-500">目前順位</dt>
              <dd class="mt-1 text-xl font-semibold">{{ result.position ?? '-' }}</dd>
            </div>
            <div class="rounded-lg bg-slate-50 p-3">
              <dt class="text-xs text-slate-500">前面人數</dt>
              <dd class="mt-1 text-xl font-semibold">{{ result.peopleAhead ?? '-' }}</dd>
            </div>
            <div class="rounded-lg bg-slate-50 p-3">
              <dt class="text-xs text-slate-500">預估等待</dt>
              <dd class="mt-1 text-xl font-semibold">{{ formatMinutes(result.estimatedMinutes) }}</dd>
            </div>
          </dl>

          <div v-if="result.qrUrl" class="mt-6 flex flex-col items-center gap-2">
            <QrCode :url="result.qrUrl" :size="200" />
            <p class="text-xs text-slate-500">掃碼即時查詢叫號進度</p>
          </div>

          <p class="mt-6 text-sm text-slate-500">{{ secondsLeft }} 秒後自動回到首頁</p>
          <button class="btn-primary btn-lg mt-4 w-full sm:w-auto" type="button" @click="reset">
            立即重新取號
          </button>
        </div>

        <!-- 服務選擇 -->
        <div v-else class="card p-6 text-slate-800">
          <AlertMessage v-if="error" variant="error" class="mb-5">{{ error }}</AlertMessage>

          <h2 class="mb-4 text-lg font-semibold">選擇服務類型</h2>

          <div class="grid gap-3 sm:grid-cols-2">
            <button
              v-for="service in services"
              :key="service.id"
              class="rounded-xl border-2 p-5 text-left transition"
              :class="
                selected?.id === service.id
                  ? 'border-brand-600 bg-brand-50 ring-2 ring-brand-100'
                  : 'border-slate-200 hover:border-brand-300 hover:bg-slate-50'
              "
              type="button"
              @click="selected = service"
            >
              <p class="text-lg font-semibold">{{ service.name }}</p>
              <p class="mt-1 text-sm text-slate-500">
                號碼前綴 {{ service.prefix }} ・ 每人約 {{ service.estimatedServiceMinutes }} 分鐘
              </p>
              <p v-if="service.priority > 0" class="mt-2">
                <span class="badge bg-amber-100 text-amber-800">優先插隊</span>
              </p>
            </button>
          </div>

          <p v-if="services.length === 0" class="py-8 text-center text-slate-500">
            目前沒有可用的服務類型
          </p>

          <button
            class="btn-primary btn-lg mt-6 w-full"
            type="button"
            :disabled="!canSubmit"
            @click="takeTicket"
          >
            {{ submitting ? '取號中…' : selected ? `取號（${selected.name}）` : '請先選擇服務' }}
          </button>
        </div>
      </template>

      <p class="mt-8 text-center text-xs text-brand-300">
        今日取號 {{ services.reduce((sum, s) => sum + (s.waitingCount ?? 0), 0) }} 筆等待中
      </p>
    </div>
  </main>
</template>
