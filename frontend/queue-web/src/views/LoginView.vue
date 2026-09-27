<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import AlertMessage from '@/components/AlertMessage.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const userName = ref('')
const password = ref('')
const error = ref<string | null>(null)

async function submit(): Promise<void> {
  error.value = null
  if (!userName.value.trim() || !password.value) {
    error.value = '請輸入帳號與密碼'
    return
  }

  try {
    await auth.login(userName.value.trim(), password.value)
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
    await router.replace(redirect)
  } catch (err) {
    error.value = err instanceof Error ? err.message : '登入失敗，請稍後再試'
  }
}
</script>

<template>
  <main class="flex min-h-screen items-center justify-center bg-slate-900 px-4 py-10">
    <div class="w-full max-w-sm">
      <div class="mb-8 text-center">
        <h1 class="text-2xl font-bold text-white">排隊叫號系統</h1>
        <p class="mt-2 text-sm text-slate-400">Queue Management System</p>
      </div>

      <form class="card p-6" @submit.prevent="submit">
        <h2 class="mb-5 text-lg font-semibold text-slate-800">登入</h2>

        <AlertMessage v-if="error" variant="error" class="mb-4">{{ error }}</AlertMessage>

        <div class="space-y-4">
          <div>
            <label class="label" for="userName">帳號</label>
            <input
              id="userName"
              v-model="userName"
              class="input"
              type="text"
              autocomplete="username"
              placeholder="請輸入帳號"
              :disabled="auth.loading"
            />
          </div>

          <div>
            <label class="label" for="password">密碼</label>
            <input
              id="password"
              v-model="password"
              class="input"
              type="password"
              autocomplete="current-password"
              placeholder="請輸入密碼"
              :disabled="auth.loading"
            />
          </div>
        </div>

        <button class="btn-primary btn-lg mt-6 w-full" type="submit" :disabled="auth.loading">
          {{ auth.loading ? '登入中…' : '登入' }}
        </button>

        <div class="mt-6 space-y-1 border-t border-slate-100 pt-4 text-xs text-slate-500">
          <p class="font-medium text-slate-600">測試帳號</p>
          <p>系統管理員：admin / a12345678</p>
          <p>櫃台人員：counter1 / counter123</p>
          <p>自助機：kiosk / kiosk123</p>
        </div>
      </form>

      <div class="mt-6 flex justify-center gap-4 text-sm">
        <RouterLink class="text-brand-300 hover:text-brand-200" :to="{ name: 'kiosk' }">
          自助取號
        </RouterLink>
        <span class="text-slate-600">|</span>
        <RouterLink class="text-brand-300 hover:text-brand-200" :to="{ name: 'display' }">
          叫號顯示器
        </RouterLink>
      </div>
    </div>
  </main>
</template>
