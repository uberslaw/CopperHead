import { format, parseISO } from 'date-fns'

export function formatCurrency(amount: number, currency: string): string {
  return new Intl.NumberFormat('en-AU', {
    style: 'currency',
    currency,
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(amount)
}

export function formatDateTime(iso: string): string {
  return format(parseISO(iso), "EEE d/M/yy 'at' h:mma")
}

export function formatDate(iso: string): string {
  return format(parseISO(iso), 'EEE d/M/yy')
}

export function formatTime(iso: string): string {
  return format(parseISO(iso), 'h:mma')
}

export function formatDateHeader(iso: string): string {
  return format(parseISO(iso), 'EEEE, d MMMM yyyy')
}

export function toDatetimeLocal(iso: string): string {
  const d = parseISO(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function fromDatetimeLocal(value: string): string {
  return new Date(value).toISOString()
}
