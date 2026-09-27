<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { settingApi } from '@/api'
import AlertMessage from '@/components/AlertMessage.vue'
import type { SettingDto } from '@/types/api'

const rows = ref<SettingDto[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const category = ref('全部')
const editing = ref<SettingDto | null>(null)
const draft = ref('')

const CATEGORY_LABELS: Record<string, string> = {
  Queue: '排隊規則',
  Display: '顯示器與語音',
  Kiosk: '自助機',
  Security: '安全',
  Service: '服務',
}

const categories = computed(() => ['全部', ...new Set(rows.value.map((r) => r.category ?? 'Queue'))])

const filtered = computed(() =>
  category.value === '全部' ? rows.value : rows.value.filter((r) => (r.category ?? 'Queue') === category.value),
)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    rows.value = await settingApi.list()
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法載入設定'
  } finally {
    loading.value = false
  }
}

function startEdit(row: SettingDto): void {
  editing.value = row
  draft.value = row.value ?? ''
}

function cancelEdit(): void {
  editing.value = null
  draft.value = ''
}

async function save(): Promise<void> {
  if (!editing.value) return
  busy.value = true
  error.value = null
  notice.value = null
  try {
    await settingApi.update({
      key: editing.value.key,
      value: draft.value,
      valueType: editing.value.valueType ?? 'string',
      category: editing.value.category ?? 'Queue',
      description: editing.value.description ?? undefined,
    })
    notice.value = '設定已更新'
    cancelEdit()
    await load()
  } catch (err) {
    error.value = err instanceof Error ? err.message : '儲存失敗'
  } finally {
    busy.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <header class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h1 class="text-xl font-bold text-slate-800">系統設定</h1>
        <p class="text-sm text-slate-500">排隊規則、顯示器語音、自助機與安全設定</p>
      </div>
      <div class="flex items-center gap-2">
        <select v-model="category" class="input w-40">
          <option v-for="c in categories" :key="c" :value="c">
            {{ c === '全部' ? '全部分類' : (CATEGORY_LABELS[c] ?? c) }}
          </option>
        </select>
        <button class="btn-secondary" type="button" @click="load">重新整理</button>
      </div>
    </header>

    <AlertMessage v-if="error" variant="error">{{ error }}</AlertMessage>
    <AlertMessage v-if="notice" variant="success">{{ notice }}</AlertMessage>

    <section class="card overflow-x-auto p-1">
      <table class="w-full min-w-[820px] text-sm">
        <thead class="border-b border-slate-200 text-left text-slate-500">
          <tr>
            <th class="px-3 py-2 font-medium">設定鍵</th>
            <th class="px-3 py-2 font-medium">說明</th>
            <th class="px-3 py-2 font-medium">類型</th>
            <th class="px-3 py-2 font-medium">值</th>
            <th class="px-3 py-2 font-medium">系統</th>
            <th class="px-3 py-2 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in filtered" :key="row.id" class="border-b border-slate-100 last:border-0 align-middle">
            <td class="px-3 py-2 font-mono text-xs">{{ row.key }}</td>
            <td class="px-3 py-2 text-slate-600">{{ row.description }}</td>
            <td class="px-3 py-2 text-xs text-slate-500">{{ row.valueType }}</td>
            <td class="px-3 py-2">
              <input
                v-if="editing?.id === row.id"
                v-model="draft"
                class="input w-40"
                :type="row.valueType === 'bool' ? 'checkbox' : 'text'"
              />
              <span v-else class="font-medium tabular-nums">{{ row.value }}</span>
            </td>
            <td class="px-3 py-2">
              <span v-if="row.isSystem" class="badge bg-slate-100 text-slate-600">系統</span>
              <span v-else class="badge bg-brand-100 text-brand-800">自訂</span>
            </td>
            <td class="px-3 py-2">
              <div class="flex gap-2">
                <template v-if="editing?.id === row.id">
                  <button class="btn-primary px-3 py-1 text-xs" type="button" :disabled="busy" @click="save">
                    儲存
                  </button>
                  <button class="btn-secondary px-3 py-1 text-xs" type="button" @click="cancelEdit">取消</button>
                </template>
                <button v-else class="btn-secondary px-3 py-1 text-xs" type="button" @click="startEdit(row)">
                  編輯
                </button>
              </div>
            </td>
          </tr>
          <tr v-if="!loading && filtered.length === 0">
            <td colspan="6" class="px-3 py-10 text-center text-slate-400">此分類沒有設定</td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</template>
