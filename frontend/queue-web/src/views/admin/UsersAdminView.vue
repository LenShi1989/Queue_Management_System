<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'

import { authApi, counterApi } from '@/api'
import AlertMessage from '@/components/AlertMessage.vue'
import { ROLES, useAuthStore } from '@/stores/auth'
import type { CounterDto, UserDto } from '@/types/api'
import { formatDateTime } from '@/utils/format'

const auth = useAuthStore()

const rows = ref<UserDto[]>([])
const counters = ref<CounterDto[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const showForm = ref(false)
const editing = ref<UserDto | null>(null)

const ROLE_OPTIONS = [
  { code: ROLES.admin, label: '系統管理員' },
  { code: ROLES.manager, label: '管理者' },
  { code: ROLES.counter, label: '櫃台服務人員' },
  { code: ROLES.display, label: '叫號顯示器' },
  { code: ROLES.kiosk, label: '自助取號設備' },
  { code: ROLES.mobile, label: '手機查詢使用者' },
]

const form = reactive({
  userName: '',
  password: '',
  displayName: '',
  email: '',
  counterId: null as number | null,
  roles: [ROLES.counter] as string[],
  isActive: true,
})

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [u, c] = await Promise.all([authApi.users(), counterApi.list(true)])
    rows.value = u
    counters.value = c
  } catch (err) {
    error.value = err instanceof Error ? err.message : '無法載入使用者'
  } finally {
    loading.value = false
  }
}

function openCreate(): void {
  editing.value = null
  Object.assign(form, {
    userName: '',
    password: '',
    displayName: '',
    email: '',
    counterId: null,
    roles: [ROLES.counter],
    isActive: true,
  })
  showForm.value = true
}

function openEdit(row: UserDto): void {
  editing.value = row
  Object.assign(form, {
    userName: row.userName,
    password: '',
    displayName: row.displayName,
    email: row.email ?? '',
    counterId: row.counterId,
    roles: [...row.roles],
    isActive: row.isActive,
  })
  showForm.value = true
}

async function save(): Promise<void> {
  error.value = null
  notice.value = null

  if (!form.userName.trim() || !form.displayName.trim()) {
    error.value = '帳號與顯示名稱為必填'
    return
  }
  if (!editing.value && form.password.length < 8) {
    error.value = '初始密碼至少需 8 個字元'
    return
  }
  if (form.roles.length === 0) {
    error.value = '請至少指派一個角色'
    return
  }

  busy.value = true
  try {
    if (editing.value) {
      await authApi.updateUser(editing.value.id, {
        displayName: form.displayName,
        email: form.email || null,
        counterId: form.counterId,
        isActive: form.isActive,
        roles: form.roles,
      })
      notice.value = '使用者已更新'
    } else {
      await authApi.createUser({
        userName: form.userName,
        password: form.password,
        displayName: form.displayName,
        email: form.email || null,
        counterId: form.counterId,
        roles: form.roles,
      })
      notice.value = '使用者已建立'
    }
    showForm.value = false
    await load()
  } catch (err) {
    error.value = err instanceof Error ? err.message : '儲存失敗'
  } finally {
    busy.value = false
  }
}

async function remove(row: UserDto): Promise<void> {
  if (row.id === auth.user?.id) {
    error.value = '不可刪除自己的帳號'
    return
  }
  if (!window.confirm(`確定要刪除使用者「${row.displayName}」嗎？`)) return

  busy.value = true
  error.value = null
  try {
    await authApi.deleteUser(row.id)
    notice.value = '使用者已刪除'
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
        <h1 class="text-xl font-bold text-slate-800">使用者管理</h1>
        <p class="text-sm text-slate-500">帳號、角色與綁定櫃台</p>
      </div>
      <button class="btn-primary" type="button" :disabled="loading" @click="openCreate">新增使用者</button>
    </header>

    <AlertMessage v-if="error" variant="error">{{ error }}</AlertMessage>
    <AlertMessage v-if="notice" variant="success">{{ notice }}</AlertMessage>

    <section class="card overflow-x-auto p-1">
      <table class="w-full min-w-[900px] text-sm">
        <thead class="border-b border-slate-200 text-left text-slate-500">
          <tr>
            <th class="px-3 py-2 font-medium">帳號</th>
            <th class="px-3 py-2 font-medium">顯示名稱</th>
            <th class="px-3 py-2 font-medium">角色</th>
            <th class="px-3 py-2 font-medium">綁定櫃台</th>
            <th class="px-3 py-2 font-medium">最後登入</th>
            <th class="px-3 py-2 font-medium">狀態</th>
            <th class="px-3 py-2 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in rows" :key="row.id" class="border-b border-slate-100 last:border-0">
            <td class="px-3 py-2 font-mono text-xs">{{ row.userName }}</td>
            <td class="px-3 py-2">{{ row.displayName }}</td>
            <td class="px-3 py-2">
              <div class="flex flex-wrap gap-1">
                <span v-for="role in row.roles" :key="role" class="badge bg-slate-100 text-slate-700">
                  {{ role }}
                </span>
              </div>
            </td>
            <td class="px-3 py-2">{{ row.counterNo ?? '-' }}</td>
            <td class="px-3 py-2 text-xs text-slate-500">
              {{ row.lastLoginAt ? formatDateTime(row.lastLoginAt) : '未曾登入' }}
            </td>
            <td class="px-3 py-2">
              <span class="badge" :class="row.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-600'">
                {{ row.isActive ? '啟用' : '停用' }}
              </span>
            </td>
            <td class="px-3 py-2">
              <div class="flex gap-2">
                <button class="btn-secondary px-3 py-1 text-xs" type="button" @click="openEdit(row)">編輯</button>
                <button
                  class="btn-danger px-3 py-1 text-xs"
                  type="button"
                  :disabled="busy || row.id === auth.user?.id"
                  @click="remove(row)"
                >
                  刪除
                </button>
              </div>
            </td>
          </tr>
          <tr v-if="!loading && rows.length === 0">
            <td colspan="7" class="px-3 py-10 text-center text-slate-400">尚無使用者</td>
          </tr>
        </tbody>
      </table>
    </section>

    <div
      v-if="showForm"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      @click.self="showForm = false"
    >
      <form class="card max-h-[90vh] w-full max-w-lg space-y-4 overflow-y-auto p-6" @submit.prevent="save">
        <h2 class="text-lg font-semibold">{{ editing ? '編輯使用者' : '新增使用者' }}</h2>

        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label class="label" for="uusername">帳號</label>
            <input id="uusername" v-model="form.userName" class="input" :disabled="Boolean(editing)" />
          </div>
          <div>
            <label class="label" for="uname">顯示名稱</label>
            <input id="uname" v-model="form.displayName" class="input" />
          </div>
          <div v-if="!editing">
            <label class="label" for="upass">初始密碼（至少 8 碼）</label>
            <input id="upass" v-model="form.password" class="input" type="password" autocomplete="new-password" />
          </div>
          <div>
            <label class="label" for="uemail">Email</label>
            <input id="uemail" v-model="form.email" class="input" type="email" />
          </div>
          <div>
            <label class="label" for="ucounter">綁定櫃台</label>
            <select id="ucounter" v-model.number="form.counterId" class="input">
              <option :value="null">未綁定</option>
              <option v-for="c in counters" :key="c.id" :value="c.id">{{ c.code }}・{{ c.name }}</option>
            </select>
          </div>
        </div>

        <div>
          <p class="label">角色</p>
          <div class="grid gap-2 sm:grid-cols-2">
            <label
              v-for="option in ROLE_OPTIONS"
              :key="option.code"
              class="flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm"
            >
              <input v-model="form.roles" type="checkbox" :value="option.code" />
              {{ option.label }}
              <span class="text-xs text-slate-400">{{ option.code }}</span>
            </label>
          </div>
        </div>

        <label class="flex items-center gap-2 text-sm">
          <input v-model="form.isActive" type="checkbox" /> 帳號啟用
        </label>

        <div class="flex justify-end gap-2">
          <button class="btn-secondary" type="button" @click="showForm = false">取消</button>
          <button class="btn-primary" type="submit" :disabled="busy">{{ busy ? '儲存中…' : '儲存' }}</button>
        </div>
      </form>
    </div>
  </div>
</template>
