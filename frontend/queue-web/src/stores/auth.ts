import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { authApi } from '@/api'
import { setUnauthorizedHandler, tokenStorage } from '@/api/http'
import type { UserDto } from '@/types/api'

/** 系統角色（spec §4） */
export const ROLES = {
  admin: 'Admin',
  manager: 'Manager',
  counter: 'Counter',
  display: 'Display',
  kiosk: 'Kiosk',
  mobile: 'Mobile',
} as const

export const useAuthStore = defineStore('auth', () => {
  const user = ref<UserDto | null>(null)
  const token = ref<string | null>(tokenStorage.get())
  const loading = ref(false)

  const isAuthenticated = computed(() => Boolean(token.value))
  const roles = computed(() => user.value?.roles ?? [])
  const displayName = computed(() => user.value?.displayName ?? user.value?.userName ?? '')

  const hasRole = (...codes: string[]): boolean => codes.some((code) => roles.value.includes(code))
  const hasAnyRole = computed(() => roles.value.length > 0)

  function setSession(accessToken: string, profile: UserDto): void {
    tokenStorage.set(accessToken)
    token.value = accessToken
    user.value = profile
  }

  function clear(): void {
    tokenStorage.clear()
    token.value = null
    user.value = null
  }

  async function login(userName: string, password: string): Promise<void> {
    loading.value = true
    try {
      const result = await authApi.login(userName, password)
      setSession(result.accessToken, {
        id: Number(result.userId),
        userName: result.userName,
        displayName: result.displayName,
        email: null,
        counterId: result.counterId,
        counterNo: result.counterNo,
        isActive: true,
        roles: result.roles,
        lastLoginAt: null,
        createdAt: result.expiresAt,
      })
    } finally {
      loading.value = false
    }
  }

  /** 重新整理頁面後還原登入狀態 */
  async function restore(): Promise<void> {
    if (!token.value) return
    loading.value = true
    try {
      user.value = await authApi.me()
    } catch {
      clear()
    } finally {
      loading.value = false
    }
  }

  function logout(): void {
    clear()
  }

  // 401 時由 http 攔截器觸發，清掉本機狀態後交由 router 導回登入頁
  setUnauthorizedHandler(() => clear())

  return {
    user,
    token,
    loading,
    isAuthenticated,
    roles,
    displayName,
    hasAnyRole,
    hasRole,
    login,
    logout,
    restore,
    setSession,
    clear,
  }
})
