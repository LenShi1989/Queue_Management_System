import { http, unwrap } from './http'
import type {
  CallNextResultDto,
  CounterDto,
  CurrentCallDto,
  DailyTrendDto,
  DisplaySnapshot,
  LoginResponse,
  PeakHourDto,
  ServiceDto,
  SettingDto,
  TicketDto,
  TicketHistoryDto,
  TicketStatusDto,
  TodayStatisticsDto,
  TransferRequest,
  UpsertCounterRequest,
  UpsertServiceRequest,
  UserDto,
  WaitingTicketDto,
} from '@/types/api'
// ─────────────── Auth ───────────────

export const authApi = {
  login: (userName: string, password: string) =>
    unwrap<LoginResponse>(http.post('/api/auth/login', { userName, password })),

  me: () => unwrap<UserDto>(http.get('/api/auth/me')),

  changePassword: (currentPassword: string, newPassword: string) =>
    unwrap(http.post('/api/auth/change-password', { currentPassword, newPassword })),

  users: () => unwrap<UserDto[]>(http.get('/api/auth/users')),

  createUser: (payload: {
    userName: string
    password: string
    displayName: string
    email?: string | null
    counterId?: number | null
    roles: string[]
  }) => unwrap<UserDto>(http.post('/api/auth/users', payload)),

  updateUser: (
    id: number,
    payload: {
      displayName?: string | null
      email?: string | null
      counterId?: number | null
      isActive?: boolean
      roles?: string[]
    },
  ) => unwrap<UserDto>(http.put(`/api/auth/users/${id}`, payload)),

  resetPassword: (id: number, newPassword: string) =>
    unwrap(http.post(`/api/auth/users/${id}/reset-password`, { newPassword })),

  deleteUser: (id: number) => unwrap(http.delete(`/api/auth/users/${id}`)),
}

// ─────────────── Ticket ───────────────

export const ticketApi = {
  create: (payload: { serviceId: number; customerName?: string | null; customerPhone?: string | null; remark?: string | null }) =>
    unwrap<TicketDto>(http.post('/api/queue/tickets', { priority: 0, ...payload })),

  get: (id: number) => unwrap<TicketDto>(http.get(`/api/queue/tickets/${id}`)),

  status: (id: number) => unwrap<TicketStatusDto>(http.get(`/api/queue/tickets/${id}/status`)),

  history: (id: number) => unwrap<TicketHistoryDto[]>(http.get(`/api/queue/tickets/${id}/history`)),

  cancel: (id: number, reason?: string) =>
    unwrap<TicketStatusDto>(http.post(`/api/queue/tickets/${id}/cancel`, { reason })),

  list: (params: {
    date?: string
    serviceId?: number
    counterId?: number
    status?: string
    ticketNo?: string
    page?: number
    pageSize?: number
  }) => unwrap<TicketDto[]>(http.get('/api/queue/tickets', { params })),
}

// ─────────────── Counter ───────────────

export const counterApi = {
  list: (includeInactive = false) =>
    unwrap<CounterDto[]>(http.get('/api/queue/counters', { params: { includeInactive } })),

  status: (counterId: number) => unwrap<CounterDto>(http.get(`/api/queue/counters/${counterId}/status`)),

  callNext: (counterId: number) =>
    unwrap<CallNextResultDto>(http.post(`/api/queue/counters/${counterId}/call-next`)),

  recall: (counterId: number) =>
    unwrap<TicketStatusDto>(http.post(`/api/queue/counters/${counterId}/recall`)),

  start: (counterId: number) =>
    unwrap<TicketStatusDto>(http.post(`/api/queue/counters/${counterId}/start`)),

  complete: (counterId: number, remark?: string) =>
    unwrap<TicketStatusDto>(http.post(`/api/queue/counters/${counterId}/complete`, { remark })),

  noShow: (counterId: number) =>
    unwrap<TicketStatusDto>(http.post(`/api/queue/counters/${counterId}/no-show`)),

  transfer: (counterId: number, payload: TransferRequest) =>
    unwrap<TicketStatusDto>(http.post(`/api/queue/counters/${counterId}/transfer`, payload)),

  pause: (counterId: number, paused: boolean) =>
    unwrap<CounterDto>(http.post(`/api/queue/counters/${counterId}/pause`, { paused })),

  create: (payload: UpsertCounterRequest) => unwrap<CounterDto>(http.post('/api/queue/counters', payload)),

  update: (id: number, payload: UpsertCounterRequest) =>
    unwrap<CounterDto>(http.put(`/api/queue/counters/${id}`, payload)),

  delete: (id: number) => unwrap(http.delete(`/api/queue/counters/${id}`)),
}

// ─────────────── 查詢（Display / 統計） ───────────────

export const queryApi = {
  current: () => unwrap<CurrentCallDto[]>(http.get('/api/queue/current')),

  waiting: (params?: { serviceId?: number; limit?: number }) =>
    unwrap<WaitingTicketDto[]>(http.get('/api/queue/waiting', { params })),

  display: () => unwrap<DisplaySnapshot>(http.get('/api/queue/display')),

  statistics: (date?: string) => unwrap<TodayStatisticsDto>(http.get('/api/queue/statistics', { params: { date } })),

  peak: (date?: string) => unwrap<PeakHourDto>(http.get('/api/queue/statistics/peak', { params: { date } })),

  trend: (days = 7) => unwrap<DailyTrendDto[]>(http.get('/api/queue/statistics/trend', { params: { days } })),
}

// ─────────────── 服務類型 / 設定 ───────────────

export const serviceApi = {
  list: (includeInactive = false) =>
    unwrap<ServiceDto[]>(http.get('/api/queue/services', { params: { includeInactive } })),

  get: (id: number) => unwrap<ServiceDto>(http.get(`/api/queue/services/${id}`)),

  create: (payload: UpsertServiceRequest) => unwrap<ServiceDto>(http.post('/api/queue/services', payload)),

  update: (id: number, payload: UpsertServiceRequest) =>
    unwrap<ServiceDto>(http.put(`/api/queue/services/${id}`, payload)),

  delete: (id: number) => unwrap(http.delete(`/api/queue/services/${id}`)),
}

export const settingApi = {
  list: () => unwrap<SettingDto[]>(http.get('/api/queue/settings')),

  update: (payload: { key: string; value: string; valueType?: string; category?: string; description?: string }) =>
    unwrap<SettingDto>(http.put('/api/queue/settings', payload)),

  delete: (key: string) => unwrap(http.delete(`/api/queue/settings/${encodeURIComponent(key)}`)),
}

// ─────────────── 手機查詢（免登入，QR Token） ───────────────

export const publicApi = {
  query: (token: string) => unwrap<TicketDto>(http.get(`/api/public/queue/${encodeURIComponent(token)}`)),

  detail: (token: string) => unwrap<TicketDto>(http.get(`/api/public/queue/${encodeURIComponent(token)}/detail`)),
}
