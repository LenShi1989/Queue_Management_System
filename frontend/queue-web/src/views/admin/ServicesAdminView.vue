<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'

import { serviceApi } from '@/api'
import AlertMessage from '@/components/AlertMessage.vue'
import type { ServiceDto, UpsertServiceRequest } from '@/types/api'

const rows = ref<ServiceDto[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const editing = ref<ServiceDto | null>(null)
const showForm = ref(false)

const form = reactive<UpsertServiceRequest>({
  code: '',
  name: '',
  prefix: 'A',
  numberLength: 3,
  priority: 0,
  estimatedServiceMinutes: 5,
  skipLineEnabled: false,
  displayOrder: 0,
  description: null,
  isActive: true,
})

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    rows.value = await serviceApi.list(true)
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法載入服務類型'
  } finally {
    loading.value = false
  }
}

function openCreate(): void {
  editing.value = null
  Object.assign(form, {
    code: '',
    name: '',
    prefix: 'A',
    numberLength: 3,
    priority: 0,
    estimatedServiceMinutes: 5,
    skipLineEnabled: false,
    displayOrder: rows.value.length + 1,
    description: null,
    isActive: true,
  })
  showForm.value = true
}

function openEdit(row: ServiceDto): void {
  editing.value = row
  Object.assign(form, {
    code: row.code,
    name: row.name,
    prefix: row.prefix,
    numberLength: row.numberLength,
    priority: row.priority,
    estimatedServiceMinutes: row.estimatedServiceMinutes,
    skipLineEnabled: row.skipLineEnabled,
    displayOrder: row.displayOrder,
    description: row.description,
    isActive: row.isActive,
  })
  showForm.value = true
}

async function save(): Promise<void> {
  if (!form.code.trim() || !form.name.trim() || !form.prefix.trim()) {
    error.value = '代碼、名稱與號碼前綴為必填'
    return
  }

  busy.value = true
  error.value = null
  notice.value = null
  try {
    if (editing.value) {
      await serviceApi.update(editing.value.id, form)
      notice.value = '服務類型已更新'
    } else {
      await serviceApi.create(form)
      notice.value = '服務類型已建立'
    }
    showForm.value = false
    await load()
  } catch (err) {
    error.value = err instanceof Error ? err.message : '儲存失敗'
  } finally {
    busy.value = false
  }
}

async function remove(row: ServiceDto): Promise<void> {
  if (!window.confirm(`確定要刪除服務類型「${row.name}」嗎？`)) return
  busy.value = true
  error.value = null
  try {
    await serviceApi.delete(row.id)
    notice.value = '服務類型已刪除'
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
        <h1 class="text-xl font-bold text-slate-800">服務類型管理</h1>
        <p class="text-sm text-slate-500">號碼前綴、優先權與平均服務時間</p>
      </div>
      <button class="btn-primary" type="button" :disabled="loading" @click="openCreate">新增服務</button>
    </header>

    <AlertMessage v-if="error" variant="error">{{ error }}</AlertMessage>
    <AlertMessage v-if="notice" variant="success">{{ notice }}</AlertMessage>

    <section class="card overflow-x-auto p-1">
      <table class="w-full min-w-[900px] text-sm">
        <thead class="border-b border-slate-200 text-left text-slate-500">
          <tr>
            <th class="px-3 py-2 font-medium">代碼</th>
            <th class="px-3 py-2 font-medium">名稱</th>
            <th class="px-3 py-2 font-medium">前綴</th>
            <th class="px-3 py-2 font-medium">號碼長度</th>
            <th class="px-3 py-2 font-medium">優先權</th>
            <th class="px-3 py-2 font-medium">平均服務</th>
            <th class="px-3 py-2 font-medium">等待中</th>
            <th class="px-3 py-2 font-medium">狀態</th>
            <th class="px-3 py-2 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in rows" :key="row.id" class="border-b border-slate-100 last:border-0">
            <td class="px-3 py-2 font-mono text-xs">{{ row.code }}</td>
            <td class="px-3 py-2">{{ row.name }}</td>
            <td class="px-3 py-2 font-semibold">{{ row.prefix }}</td>
            <td class="px-3 py-2 tabular-nums">{{ row.numberLength }}</td>
            <td class="px-3 py-2 tabular-nums">{{ row.priority }}</td>
            <td class="px-3 py-2 tabular-nums">{{ row.estimatedServiceMinutes }} 分</td>
            <td class="px-3 py-2 tabular-nums">{{ row.waitingCount ?? 0 }}</td>
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
            <td colspan="9" class="px-3 py-10 text-center text-slate-400">尚無服務類型</td>
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
        <h2 class="text-lg font-semibold">{{ editing ? '編輯服務類型' : '新增服務類型' }}</h2>

        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label class="label" for="code">代碼</label>
            <input id="code" v-model="form.code" class="input" placeholder="GENERAL" />
          </div>
          <div>
            <label class="label" for="name">名稱</label>
            <input id="name" v-model="form.name" class="input" placeholder="一般服務" />
          </div>
          <div>
            <label class="label" for="prefix">號碼前綴</label>
            <input id="prefix" v-model="form.prefix" class="input" maxlength="5" placeholder="A" />
          </div>
          <div>
            <label class="label" for="numberLength">號碼長度</label>
            <input id="numberLength" v-model.number="form.numberLength" class="input" type="number" min="1" max="10" />
          </div>
          <div>
            <label class="label" for="priority">優先權</label>
            <input id="priority" v-model.number="form.priority" class="input" type="number" />
          </div>
          <div>
            <label class="label" for="est">平均服務分鐘</label>
            <input id="est" v-model.number="form.estimatedServiceMinutes" class="input" type="number" min="1" />
          </div>
          <div>
            <label class="label" for="order">顯示順序</label>
            <input id="order" v-model.number="form.displayOrder" class="input" type="number" />
          </div>
          <div class="flex items-end gap-4 pb-2">
            <label class="flex items-center gap-2 text-sm">
              <input v-model="form.isActive" type="checkbox" /> 啟用
            </label>
            <label class="flex items-center gap-2 text-sm">
              <input v-model="form.skipLineEnabled" type="checkbox" /> 允許插隊
            </label>
          </div>
        </div>

        <div>
          <label class="label" for="desc">說明</label>
          <input id="desc" v-model="form.description" class="input" />
        </div>

        <div class="flex justify-end gap-2">
          <button class="btn-secondary" type="button" @click="showForm = false">取消</button>
          <button class="btn-primary" type="submit" :disabled="busy">{{ busy ? '儲存中…' : '儲存' }}</button>
        </div>
      </form>
    </div>
  </div>
</template>
