import axios, { type AxiosError, type AxiosInstance } from 'axios'
import type { ApiErrorResponse } from '@/types/api'

/** 後端錯誤（統一格式） */
export class ApiError extends Error {
  readonly code: string
  readonly status: number
  readonly traceId?: string

  constructor(code: string, message: string, status: number, traceId?: string) {
    super(message)
    this.name = 'ApiError'
    this.code = code
    this.status = status
    this.traceId = traceId
  }
}

const TOKEN_KEY = 'qms.token'

export const tokenStorage = {
  get: (): string | null => localStorage.getItem(TOKEN_KEY),
  set: (token: string): void => localStorage.setItem(TOKEN_KEY, token),
  clear: (): void => localStorage.removeItem(TOKEN_KEY),
}

/** 401 時由 auth store 注入，避免 axios 直接依賴 Pinia */
let onUnauthorized: (() => void) | null = null
export const setUnauthorizedHandler = (handler: () => void): void => {
  onUnauthorized = handler
}

export const http: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '',
  timeout: 30_000,
})

http.interceptors.request.use((config) => {
  const token = tokenStorage.get()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

http.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ApiErrorResponse>) => {
    const status = error.response?.status ?? 0
    const body = error.response?.data

    if (status === 401) {
      tokenStorage.clear()
      onUnauthorized?.()
    }

    // 沒有回應（逾時、網路中斷）也要轉成可顯示的錯誤
    const message =
      body?.message ??
      (status === 0 ? '無法連線至伺服器，請確認網路或稍後再試' : '系統發生錯誤，請稍後再試')

    return Promise.reject(new ApiError(body?.code ?? 'NETWORK_ERROR', message, status, body?.traceId))
  },
)

/** 取出 ApiResponse<T> 的 data，失敗時直接 throw ApiError */
export async function unwrap<T>(promise: Promise<{ data: { success: boolean; data: T } }>): Promise<T> {
  const response = await promise
  return response.data.data
}
