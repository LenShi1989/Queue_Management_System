import { onBeforeUnmount, ref } from 'vue'

import { ApiError } from '@/api/http'

/** 全域錯誤訊息（供頁面顯示後端回傳的繁中訊息） */
export function useAsyncState<T>() {
  const data = ref<T | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const toMessage = (err: unknown): string =>
    err instanceof ApiError ? err.message : '系統發生錯誤，請稍後再試'

  async function run(task: () => Promise<T>): Promise<T | null> {
    loading.value = true
    error.value = null
    try {
      data.value = await task()
      return data.value
    } catch (err) {
      error.value = toMessage(err)
      return null
    } finally {
      loading.value = false
    }
  }

  function reset(): void {
    data.value = null
    error.value = null
    loading.value = false
  }

  return { data, loading, error, run, reset, toMessage }
}

/** 定時輪詢（頁面隱藏時自動暫停，減少不必要的請求） */
export function usePolling(callback: () => void | Promise<void>, intervalMs: number) {
  let timer: number | null = null

  const start = (): void => {
    if (timer !== null) return
    timer = window.setInterval(() => {
      if (!document.hidden) void callback()
    }, intervalMs)
  }

  const stop = (): void => {
    if (timer === null) return
    window.clearInterval(timer)
    timer = null
  }

  const onVisibility = (): void => {
    if (document.hidden) stop()
    else {
      void callback()
      start()
    }
  }

  document.addEventListener('visibilitychange', onVisibility)
  onBeforeUnmount(() => {
    stop()
    document.removeEventListener('visibilitychange', onVisibility)
  })

  return { start, stop }
}
