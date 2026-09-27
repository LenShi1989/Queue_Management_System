<script setup lang="ts">
import { ref } from 'vue'

import { authApi } from '@/api'
import AlertMessage from '@/components/AlertMessage.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const busy = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)

async function submit(): Promise<void> {
  error.value = null
  success.value = null

  if (!currentPassword.value || !newPassword.value) {
    error.value = '請填寫目前密碼與新密碼'
    return
  }
  if (newPassword.value.length < 8) {
    error.value = '新密碼至少需 8 個字元'
    return
  }
  if (newPassword.value !== confirmPassword.value) {
    error.value = '兩次輸入的新密碼不一致'
    return
  }

  busy.value = true
  try {
    await authApi.changePassword(currentPassword.value, newPassword.value)
    success.value = '密碼已更新'
    currentPassword.value = ''
    newPassword.value = ''
    confirmPassword.value = ''
  } catch (err) {
    error.value = err instanceof Error ? err.message : '密碼修改失敗'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="max-w-xl space-y-5">
    <header>
      <h1 class="text-xl font-bold text-slate-800">我的帳號</h1>
      <p class="text-sm text-slate-500">帳號資訊與密碼修改</p>
    </header>

    <section class="card p-5">
      <dl class="grid gap-3 sm:grid-cols-2">
        <div>
          <dt class="text-sm text-slate-500">帳號</dt>
          <dd class="font-medium">{{ auth.user?.userName ?? '-' }}</dd>
        </div>
        <div>
          <dt class="text-sm text-slate-500">顯示名稱</dt>
          <dd class="font-medium">{{ auth.user?.displayName ?? '-' }}</dd>
        </div>
        <div>
          <dt class="text-sm text-slate-500">角色</dt>
          <dd class="font-medium">{{ auth.roles.join('、') || '-' }}</dd>
        </div>
        <div>
          <dt class="text-sm text-slate-500">綁定櫃台</dt>
          <dd class="font-medium">{{ auth.user?.counterNo ?? '未綁定' }}</dd>
        </div>
      </dl>
    </section>

    <section class="card p-5">
      <h2 class="mb-4 font-semibold text-slate-700">修改密碼</h2>

      <AlertMessage v-if="error" variant="error" class="mb-4">{{ error }}</AlertMessage>
      <AlertMessage v-if="success" variant="success" class="mb-4">{{ success }}</AlertMessage>

      <form class="space-y-4" @submit.prevent="submit">
        <div>
          <label class="label" for="current">目前密碼</label>
          <input id="current" v-model="currentPassword" class="input" type="password" autocomplete="current-password" />
        </div>
        <div>
          <label class="label" for="new">新密碼（至少 8 碼）</label>
          <input id="new" v-model="newPassword" class="input" type="password" autocomplete="new-password" />
        </div>
        <div>
          <label class="label" for="confirm">確認新密碼</label>
          <input id="confirm" v-model="confirmPassword" class="input" type="password" autocomplete="new-password" />
        </div>
        <button class="btn-primary" type="submit" :disabled="busy">
          {{ busy ? '更新中…' : '更新密碼' }}
        </button>
      </form>
    </section>
  </div>
</template>
