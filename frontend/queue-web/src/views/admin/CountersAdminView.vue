<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'

import { counterApi, serviceApi } from '@/api'
import AlertMessage from '@/components/AlertMessage.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import type { CounterDto, ServiceDto, UpsertCounterRequest } from '@/types/api'

const rows = ref<CounterDto[]>([])
const services = ref<ServiceDto[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const editing = ref<CounterDto | null>(null)
const showForm = ref(false)

const form = reactive<UpsertCounterRequest>({
  code: '',
  name: '',
  serviceId: null,
  weight: 1,
  displayOrder: 0,
  isActive: true,
})

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [c, s] = await Promise.all([counterApi.list(true), serviceApi.list(true)])
    rows.value = c
    services.value = s
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法載入櫃台資料'
  } finally {
    loading.value = false
  }
}

function openCreate(): void {
  editing.value = null
  Object.assign(form, {
    code: '',
    name: '',
    serviceId: services.value[0]?.id ?? null,
    weight: 1,
    displayOrder: rows.value.length + 1,
    isActive: true,
  })
  showForm.value = true
}

function openEdit(row: CounterDto): void {
  editing.value = row
  Object.assign(form, {
    code: row.code,
    name: row.name,
    serviceId: row.serviceId,
    weight: row.weight,
    displayOrder: row.displayOrder,
    isActive: row.isActive,
  })
  showForm.value = true
}

async function save(): Promise<void> {
  if (!form.code.trim() || !form.name.trim()) {
    error.value = '代碼與名稱為必填'
    return
  }

  busy.value = true
  error.value = null
  notice.value = null
  try {
    if (editing.value) {
      await counterApi.update(editing.value.id, form)
      notice.value = '櫃台已更新'
    } else {
      await counterApi.create(form)
      notice.value = '櫃台已建立'
    }
    showForm.value = false
    await load()
  } catch (err) {
    error.value = err instanceof Error ? err.message : '儲存失敗'
  } finally {
    busy.value = false
  }
}

async function remove(row: CounterDto): Promise<void> {
  if (!window.confirm(`確定要刪除櫃台「${row.name}」嗎？`)) return
  busy.value = true
  error.value = null
  try {
    await counterApi.delete(row.id)
    notice.value = '櫃台已刪除'
    await load()
  } catch (err) {
    error.value = err instanceof Error ? err.message : '刪除失敗'
  } finally {
    busy.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <header class="flex items-center justify-between">
      <div>
        <h1 class="text-xl font-bold text-slate-800">櫃台管理</h1>
        <p class="text-sm text-slate-500">櫃台與服務類型對應、權重與顯示順序</p>
      </div>
      <button class="btn-primary" type="button" :disabled="loading" @click="openCreate">新增櫃台</button>
    </header>

    <AlertMessage v-if="error" variant="error">{{ error }}</AlertMessage>
    <AlertMessage v-if="notice" variant="success">{{ notice }}</AlertMessage>

    <section class="card overflow-x-auto p-1">
      <table class="w-full min-w-[860px] text-sm">
        <thead class="border-b border-slate-200 text-left text-slate-500">
          <tr>
            <th class="px-3 py-2 font-medium">代碼</th>
            <th class="px-3 py-2 font-medium">名稱</th>
            <th class="px-3 py-2 font-medium">服務類型</th>
            <th class="px-3 py-2 font-medium">目前狀態</th>
            <th class="px-3 py-2 font-medium">目前票號</th>
            <th class="px-3 py-2 font-medium">權重</th>
            <th class="px-3 py-2 font-medium">順序</th>
            <th class="px-3 py-2 font-medium">啟用</th>
            <th class="px-3 py-2 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in rows" :key="row.id" class="border-b border-slate-100 last:border-0">
            <td class="px-3 py-2 font-mono text-xs">{{ row.code }}</td>
            <td class="px-3 py-2">{{ row.name }}</td>
            <td class="px-3 py-2">{{ row.serviceName ?? '未指定' }}</td>
            <td class="px-3 py-2"><StatusBadge :status="row.status" /></td>
            <td class="px-3 py-2 font-semibold tabular-nums">{{ row.currentTicketNo ?? '--' }}</td>
            <td class="px-3 py-2 tabular-nums">{{ row.weight }}</td>
            <td class="px-3 py-2 tabular-nums">{{ row.displayOrder }}</td>
            <td class="px-3 py-2">
              <span class="badge" :class="row.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-600'">
                {{ row.isActive ? '啟用' : '停用' }}
              </span>
            </td>
            <td class="px-3 py-2">
              <div class="flex gap-2">
                <button class="btn-secondary px-3 py-1 text-xs" type="button" @click="openEdit(row)">編輯</button>
                <button class="btn-danger px-3 py-1 text-xs" type="button" :disabled="busy" @click="remove(row)">
                  刪除
                </button>
              </div>
            </td>
          </tr>
          <tr v-if="!loading && rows.length === 0">
            <td colspan="9" class="px-3 py-10 text-center text-slate-400">尚無櫃台</td>
          </tr>
        </tbody>
      </table>
    </section>

    <div
      v-if="showForm"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      @click.self="showForm = false"
    >
      <form class="card w-full max-w-lg space-y-4 p-6" @submit.prevent="save">
        <h2 class="text-lg font-semibold">{{ editing ? '編輯櫃台' : '新增櫃台' }}</h2>

        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label class="label" for="ccode">代碼</label>
            <input id="ccode" v-model="form.code" class="input" placeholder="1" />
          </div>
          <div>
            <label class="label" for="cname">名稱</label>
            <input id="cname" v-model="form.name" class="input" placeholder="1 號櫃台" />
          </div>
          <div>
            <label class="label" for="cservice">服務類型</label>
            <select id="cservice" v-model.number="form.serviceId" class="input">
              <option :value="null">未指定</option>
              <option v-for="s in services" :key="s.id" :value="s.id">{{ s.name }}</option>
            </select>
          </div>
          <div>
            <label class="label" for="cweight">權重</label>
            <input id="cweight" v-model.number="form.weight" class="input" type="number" min="1" />
          </div>
          <div>
            <label class="label" for="corder">顯示順序</label>
            <input id="corder" v-model.number="form.displayOrder" class="input" type="number" />
          </div>
          <div class="flex items-end pb-2">
            <label class="flex items-center gap-2 text-sm">
              <input v-model="form.isActive" type="checkbox" /> 啟用
            </label>
          </div>
        </div>

        <div class="flex justify-end gap-2">
          <button class="btn-secondary" type="button" @click="showForm = false">取消</button>
          <button class="btn-primary" type="submit" :disabled="busy">{{ busy ? '儲存中…' : '儲存' }}</button>
        </div>
      </form>
    </div>
  </div>
</template>
