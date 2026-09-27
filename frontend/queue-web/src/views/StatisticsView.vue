<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { queryApi } from '@/api'
import AlertMessage from '@/components/AlertMessage.vue'
import LoadingState from '@/components/LoadingState.vue'
import type { DailyTrendDto, PeakHourDto, TodayStatisticsDto } from '@/types/api'
import { todayInTaipei } from '@/utils/format'

const date = ref(todayInTaipei())
const stats = ref<TodayStatisticsDto | null>(null)
const peak = ref<PeakHourDto | null>(null)
const trend = ref<DailyTrendDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const maxHourly = computed(() => Math.max(1, ...(stats.value?.hourly.map((h) => h.total) ?? [1])))
const maxTrend = computed(() => Math.max(1, ...trend.value.map((t) => t.total)))

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [s, p, t] = await Promise.all([
      queryApi.statistics(date.value || undefined),
      queryApi.peak(date.value || undefined),
      queryApi.trend(7),
    ])
    stats.value = s
    peak.value = p
    trend.value = t
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法載入統計資料'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <header class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 class="text-xl font-bold text-slate-800">統計報表</h1>
        <p class="text-sm text-slate-500">每日/hourly 分布、各服務與各櫃台效率</p>
      </div>
      <div class="flex items-end gap-2">
        <div>
          <label class="label" for="stat-date">統計日期</label>
          <input id="stat-date" v-model="date" class="input w-40" type="date" @change="load" />
        </div>
        <button class="btn-primary" type="button" @click="load">重新整理</button>
      </div>
    </header>

    <LoadingState v-if="loading" label="統計中" />
    <AlertMessage v-else-if="error" variant="error">{{ error }}</AlertMessage>

    <template v-else-if="stats">
      <section class="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div class="card p-4">
          <p class="text-sm text-slate-500">總取號數</p>
          <p class="mt-1 text-3xl font-bold tabular-nums">{{ stats.totalTickets }}</p>
        </div>
        <div class="card p-4">
          <p class="text-sm text-slate-500">完成率</p>
          <p class="mt-1 text-3xl font-bold tabular-nums text-emerald-600">
            {{ stats.totalTickets ? Math.round((stats.completedCount / stats.totalTickets) * 100) : 0 }}%
          </p>
        </div>
        <div class="card p-4">
          <p class="text-sm text-slate-500">平均等待時間</p>
          <p class="mt-1 text-3xl font-bold tabular-nums">{{ Math.round(stats.averageWaitingMinutes) }} 分</p>
        </div>
        <div class="card p-4">
          <p class="text-sm text-slate-500">尖峰時段</p>
          <p class="mt-1 text-3xl font-bold tabular-nums">
            {{ peak ? `${String(peak.hour).padStart(2, '0')}:00` : '--' }}
          </p>
          <p v-if="peak" class="text-xs text-slate-500">{{ peak.total }} 筆（{{ Math.round(peak.ratio) }}%）</p>
        </div>
      </section>

      <section class="card p-5">
        <h2 class="mb-4 font-semibold text-slate-700">每小時分布</h2>
        <div class="flex h-48 items-end gap-1">
          <div
            v-for="h in stats.hourly"
            :key="h.hour"
            class="group relative flex-1 rounded-t bg-brand-200"
            :style="{ height: `${(h.total / maxHourly) * 100}%` }"
          >
            <div
              class="absolute inset-x-0 bottom-0 rounded-t bg-brand-500"
              :style="{ height: `${(h.completed / maxHourly) * 100}%` }"
            />
            <span
              class="pointer-events-none absolute -top-6 left-1/2 hidden -translate-x-1/2 whitespace-nowrap rounded bg-slate-800 px-1.5 py-0.5 text-[10px] text-white group-hover:block"
            >
              {{ String(h.hour).padStart(2, '0') }}時 共{{ h.total }}/完{{ h.completed }}
            </span>
          </div>
        </div>
        <div class="mt-2 flex justify-between text-[10px] text-slate-400">
          <span>00:00</span><span>06:00</span><span>12:00</span><span>18:00</span><span>23:00</span>
        </div>
      </section>

      <div class="grid gap-5 lg:grid-cols-2">
        <section class="card p-5">
          <h2 class="mb-4 font-semibold text-slate-700">各服務統計</h2>
          <table class="w-full text-sm">
            <thead class="border-b border-slate-200 text-left text-slate-500">
              <tr>
                <th class="py-2 pr-3 font-medium">服務</th>
                <th class="py-2 pr-3 font-medium">總數</th>
                <th class="py-2 pr-3 font-medium">完成</th>
                <th class="py-2 pr-3 font-medium">過號</th>
                <th class="py-2 font-medium">平均服務</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="s in stats.services" :key="s.serviceId" class="border-b border-slate-100 last:border-0">
                <td class="py-2 pr-3">{{ s.serviceName }}</td>
                <td class="py-2 pr-3 tabular-nums">{{ s.total }}</td>
                <td class="py-2 pr-3 tabular-nums">{{ s.completed }}</td>
                <td class="py-2 pr-3 tabular-nums">{{ s.noShow }}</td>
                <td class="py-2 tabular-nums">{{ Math.round(s.averageServiceMinutes) }} 分</td>
              </tr>
            </tbody>
          </table>
        </section>

        <section class="card p-5">
          <h2 class="mb-4 font-semibold text-slate-700">各櫃台效率</h2>
          <table class="w-full text-sm">
            <thead class="border-b border-slate-200 text-left text-slate-500">
              <tr>
                <th class="py-2 pr-3 font-medium">櫃台</th>
                <th class="py-2 pr-3 font-medium">叫號</th>
                <th class="py-2 pr-3 font-medium">完成</th>
                <th class="py-2 pr-3 font-medium">過號</th>
                <th class="py-2 font-medium">平均服務</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="c in stats.counters" :key="c.counterId" class="border-b border-slate-100 last:border-0">
                <td class="py-2 pr-3">{{ c.counterNo }}・{{ c.counterName }}</td>
                <td class="py-2 pr-3 tabular-nums">{{ c.calledCount }}</td>
                <td class="py-2 pr-3 tabular-nums">{{ c.completedCount }}</td>
                <td class="py-2 pr-3 tabular-nums">{{ c.noShowCount }}</td>
                <td class="py-2 tabular-nums">{{ Math.round(c.averageServiceMinutes) }} 分</td>
              </tr>
            </tbody>
          </table>
        </section>
      </div>

      <section class="card p-5">
        <h2 class="mb-4 font-semibold text-slate-700">近 7 日趨勢</h2>
        <div class="flex h-40 items-end gap-3">
          <div
            v-for="t in trend"
            :key="t.date"
            class="group flex flex-1 flex-col items-center gap-1"
          >
            <div
              class="w-full rounded-t bg-brand-400"
              :style="{ height: `${(t.total / maxTrend) * 100}%` }"
            />
            <span class="text-[10px] text-slate-500">{{ t.date.slice(5) }}</span>
            <span class="text-[10px] text-slate-400">{{ t.total }}</span>
          </div>
        </div>
      </section>
    </template>
  </div>
</template>
