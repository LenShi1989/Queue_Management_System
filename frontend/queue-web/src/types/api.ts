/** 後端統一回應格式（Queue.Api/Common/ApiResponse.cs） */
export interface ApiResponse<T> {
  success: boolean
  data: T
  message?: string | null
}

export interface ApiErrorResponse {
  success: false
  code: string
  message: string
  traceId: string
  data: null
}

export type TicketStatus =
  | 'Waiting'
  | 'Calling'
  | 'Serving'
  | 'Completed'
  | 'NoShow'
  | 'Cancelled'
  | 'Transferred'
  | 'Suspended'

export type CounterStatus = 'Idle' | 'Busy' | 'Paused' | 'Offline'

export interface LoginResponse {
  accessToken: string
  tokenType: string
  expiresIn: number
  expiresAt: string
  userId: string
  userName: string
  displayName: string
  counterId?: number | null
  counterNo?: string | null
  roles: string[]
}

export interface ServiceDto {
  id: number
  code: string
  name: string
  prefix: string
  numberLength: number
  priority: number
  estimatedServiceMinutes: number
  skipLineEnabled: boolean
  displayOrder: number
  description?: string | null
  isActive: boolean
  waitingCount?: number | null
  servingCount?: number | null
  currentTicketNo?: string | null
}

export interface UpsertServiceRequest {
  code: string
  name: string
  prefix: string
  numberLength: number
  priority: number
  estimatedServiceMinutes: number
  skipLineEnabled: boolean
  displayOrder: number
  description?: string | null
  isActive: boolean
}

export interface CounterDto {
  id: number
  code: string
  name: string
  serviceId?: number | null
  serviceName?: string | null
  serviceCode?: string | null
  status: string
  currentTicketId?: number | null
  currentTicketNo?: string | null
  currentTicketStatus?: string | null
  weight: number
  displayOrder: number
  isActive: boolean
  waitingCount?: number | null
}

export interface UpsertCounterRequest {
  code: string
  name: string
  serviceId?: number | null
  weight: number
  displayOrder: number
  isActive: boolean
}

export interface TransferRequest {
  targetCounterId: number
  remark?: string | null
}

export interface CreateTicketRequest {
  serviceId: number
  priority: number
  customerName?: string | null
  customerPhone?: string | null
  remark?: string | null
}

export interface TicketDto {
  ticketId: number
  id: number
  ticketNo: string
  status: TicketStatus
  queueDate: string
  prefix: string
  sequenceNo: number
  serviceId: number
  serviceName: string
  counterId?: number | null
  counterNo?: string | null
  priority: number
  position?: number | null
  peopleAhead?: number | null
  estimatedMinutes?: number | null
  customerName?: string | null
  customerPhone?: string | null
  qrToken?: string | null
  qrUrl?: string | null
  createdAt: string
  calledAt?: string | null
  servingAt?: string | null
  completedAt?: string | null
  waitingMinutes?: number | null
  serviceMinutes?: number | null
  callCount: number
  remark?: string | null
}

export interface CallNextResultDto {
  ticketId: number
  ticketNo: string
  counterId: number
  counterNo: string
  status: TicketStatus
  position?: number | null
  peopleAhead?: number | null
  estimatedMinutes?: number | null
  serviceId?: number | null
  serviceName?: string | null
}

export interface TicketStatusDto {
  ticketId: number
  ticketNo: string
  status: TicketStatus
  currentCallingNo?: string | null
  counterNo?: string | null
  position?: number | null
  peopleAhead?: number | null
  estimatedMinutes?: number | null
  createdAt: string
  calledAt?: string | null
  servingAt?: string | null
  completedAt?: string | null
  updatedAt: string
}

export interface TicketHistoryDto {
  id: number
  ticketId: number
  ticketNo: string
  fromStatus?: string | null
  toStatus: string
  action: string
  counterId?: number | null
  counterNo?: string | null
  operatorName?: string | null
  createdAt: string
  ipAddress?: string | null
  remark?: string | null
}

export interface CurrentCallDto {
  ticketId: number
  ticketNo: string
  serviceName: string
  counterId?: number | null
  counterNo?: string | null
  calledAt?: string | null
  timestamp: string
}

export interface WaitingTicketDto {
  ticketId: number
  ticketNo: string
  serviceName: string
  priority: number
  position: number
  waitingMinutes: number
  createdAt: string
}

/** 大螢幕快照 */
export interface DisplayTicket {
  ticketId: number
  ticketNo: string
  status: string
  calledAt?: string | null
  timestamp: string
}

export interface DisplayCounterState {
  counterId: number
  counterNo: string
  counterName: string
  status: string
  current?: DisplayTicket | null
  waiting: DisplayTicket[]
}

export interface DisplayServiceState {
  serviceId: number
  serviceName: string
  prefix: string
  waitingCount: number
  servingCount: number
  currentTicketNo?: string | null
}

export interface DisplaySnapshot {
  timestamp: string
  waitingTotal: number
  waitingTotalWithPriority?: number | null
  counters: DisplayCounterState[]
  services: DisplayServiceState[]
  recentCalls: DisplayTicket[]
}

export interface ServiceStatisticDto {
  serviceId: number
  serviceName: string
  prefix: string
  total: number
  completed: number
  waiting: number
  noShow: number
  cancelled: number
  averageServiceMinutes: number
}

export interface CounterStatisticDto {
  counterId: number
  counterNo: string
  counterName: string
  calledCount: number
  completedCount: number
  noShowCount: number
  averageServiceMinutes: number
}

export interface HourlyStatisticDto {
  hour: number
  total: number
  completed: number
  noShow: number
  cancelled: number
}

export interface TodayStatisticsDto {
  date: string
  totalTickets: number
  completedCount: number
  waitingCount: number
  callingCount: number
  servingCount: number
  noShowCount: number
  cancelledCount: number
  transferredCount: number
  averageWaitingMinutes: number
  averageServiceMinutes: number
  averageTotalMinutes: number
  noShowRate: number
  cancelRate: number
  activeCounters: number
  idleCounters: number
  services: ServiceStatisticDto[]
  counters: CounterStatisticDto[]
  hourly: HourlyStatisticDto[]
}

export interface PeakHourDto {
  hour: number
  total: number
  ratio: number
}

export interface DailyTrendDto {
  date: string
  total: number
  completed: number
  noShow: number
  cancelled: number
  averageWaitingMinutes: number
}

export interface SettingDto {
  id: number
  key: string
  value?: string | null
  valueType?: string | null
  category?: string | null
  description?: string | null
  isSystem: boolean
}

export interface UserDto {
  id: number
  userName: string
  displayName: string
  email?: string | null
  counterId?: number | null
  counterNo?: string | null
  isActive: boolean
  roles: string[]
  lastLoginAt?: string | null
  createdAt: string
}

/** SignalR QueueHub 事件內容 */
export interface TicketEventNotification {
  ticketId: number
  ticketNo: string
  status: TicketStatus
  prefix?: string | null
  serviceId?: number | null
  serviceName?: string | null
  counterId?: number | null
  counterNo?: string | null
  position?: number | null
  peopleAhead?: number | null
  estimatedMinutes?: number | null
  priority?: number | null
  message?: string | null
  timestamp: string
}
