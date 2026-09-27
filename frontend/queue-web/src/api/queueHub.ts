import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { computed, reactive, readonly } from 'vue'

import { tokenStorage } from '@/api/http'
import type { DisplaySnapshot, TicketEventNotification } from '@/types/api'

export type QueueEventName =
  | 'QueueCreated'
  | 'QueueCalled'
  | 'QueueRecalled'
  | 'QueueStarted'
  | 'QueueCompleted'
  | 'QueueNoShow'
  | 'QueueCancelled'
  | 'QueueTransferred'
  | 'QueueUpdated'
  | 'DisplayUpdated'

type Handler = (payload: unknown) => void

interface QueueHubState {
  connection: HubConnection | null
  state: HubConnectionState
  lastError: string | null
  snapshot: DisplaySnapshot | null
  lastCall: TicketEventNotification | null
}

const state = reactive<QueueHubState>({
  connection: null,
  state: HubConnectionState.Disconnected,
  lastError: null,
  snapshot: null,
  lastCall: null,
})
const handlers = new Map<string, Set<Handler>>()

const EVENT_NAMES: QueueEventName[] = [
  'QueueCreated',
  'QueueCalled',
  'QueueRecalled',
  'QueueStarted',
  'QueueCompleted',
  'QueueNoShow',
  'QueueCancelled',
  'QueueTransferred',
  'QueueUpdated',
  'DisplayUpdated',
]

const hubUrl = (): string => {
  const base = import.meta.env.VITE_API_BASE_URL ?? ''
  if (base) return `${base.replace(/\/$/, '')}/hubs/queue`
  return '/hubs/queue'
}

const notify = (event: string, payload: unknown): void => {
  handlers.get(event)?.forEach((handler) => handler(payload))
}

let starting: Promise<void> | null = null

export function useQueueHub() {
  async function start(): Promise<void> {
    if (state.connection && state.connection.state === HubConnectionState.Connected) return
    if (starting) return starting

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl(), {
        accessTokenFactory: () => tokenStorage.get() ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build()

    for (const name of EVENT_NAMES) {
      connection.on(name, (payload: unknown) => {
        if (name === 'DisplayUpdated') {
          state.snapshot = payload as DisplaySnapshot
        }
        if (name === 'QueueCalled' || name === 'QueueRecalled') {
          state.lastCall = payload as TicketEventNotification
        }
        notify(name, payload)
      })
    }

    connection.onreconnecting(() => {
      state.state = HubConnectionState.Reconnecting
    })
    connection.onreconnected(() => {
      state.state = HubConnectionState.Connected
    })
    connection.onclose((error) => {
      state.state = HubConnectionState.Disconnected
      state.lastError = error?.message ?? null
    })

    state.connection = connection

    starting = connection
      .start()
      .then(() => {
        state.state = HubConnectionState.Connected
        state.lastError = null
      })
      .catch((error: unknown) => {
        state.state = HubConnectionState.Disconnected
        state.lastError = error instanceof Error ? error.message : '無法連線至即時推播服務'
      })
      .finally(() => {
        starting = null
      })

    return starting
  }

  async function stop(): Promise<void> {
    const connection = state.connection
    state.connection = null
    state.state = HubConnectionState.Disconnected
    if (connection) {
      await connection.stop().catch(() => undefined)
    }
  }

  /** 訂閱事件；回傳取消訂閱函式 */
  function on(event: QueueEventName, handler: Handler): () => void {
    const set = handlers.get(event) ?? new Set<Handler>()
    set.add(handler)
    handlers.set(event, set)
    return () => set.delete(handler)
  }

  /**
   * 訂閱指定服務類型／櫃台群組。
   * 連線建立時後端已自動加入 display 群組，顯示頁不需額外呼叫。
   */
  async function subscribeService(serviceCode: string): Promise<void> {
    await state.connection?.invoke('SubscribeService', serviceCode)
  }

  async function subscribeCounter(counterNo: string): Promise<void> {
    await state.connection?.invoke('SubscribeCounter', counterNo)
  }

  return {
    state: readonly(state),
    isConnected: computed(() => state.state === HubConnectionState.Connected),
    start,
    stop,
    on,
    subscribeService,
    subscribeCounter,
  }
}
