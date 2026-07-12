import type { Trip } from '../types'

const STORAGE_KEY = 'voyage-trip'

export function loadTrip(): Trip | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    return JSON.parse(raw) as Trip
  } catch {
    return null
  }
}

export function saveTrip(trip: Trip): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(trip))
}
