/** 數字與時間格式化（顯示一律使用 Asia/Taipei） */

const dtf = (options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat =>
  new Intl.DateTimeFormat('zh-TW', { timeZone: 'Asia/Taipei', ...options })

const timeFmt = dtf({ hour: '2-digit', minute: '2-digit', hour12: false })
const dateTimeFmt = dtf({ month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hour12: false })
const fullFmt = dtf({ year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false })

export const formatTime = (value?: string | null): string =>
  value ? timeFmt.format(new Date(value)) : '--:--'

export const formatDateTime = (value?: string | null): string =>
  value ? dateTimeFmt.format(new Date(value)) : '--'

export const formatFull = (value?: string | null): string => (value ? fullFmt.format(new Date(value)) : '--')

/** 分鐘數顯示：不足 1 分鐘顯示「<1 分」 */
export const formatMinutes = (value?: number | null): string => {
  if (value === null || value === undefined) return '--'
  if (value < 1) return '<1 分'
  return `${Math.round(value)} 分`
}

/** 台北時區的今天 (YYYY-MM-DD) */
export const todayInTaipei = (): string => {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Taipei',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date())
  return parts
}
