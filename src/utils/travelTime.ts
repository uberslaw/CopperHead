import type { TripSettings } from '../types'

export function calculateTravelMinutes(
  distanceKm: number,
  settings: TripSettings,
): number {
  if (distanceKm <= 0) return 0
  const baseMinutes = (distanceKm / settings.averageSpeedKmh) * 60
  return Math.ceil(baseMinutes * settings.trafficMultiplier + settings.generalBufferMinutes)
}

export function calculateRecommendedDeparture(
  eventDateTime: string,
  travelMinutes: number,
  bufferMinutes: number,
): Date {
  const event = new Date(eventDateTime)
  const totalMinutes = travelMinutes + bufferMinutes
  return new Date(event.getTime() - totalMinutes * 60_000)
}

export function formatDuration(minutes: number): string {
  if (minutes < 60) return `${minutes} min`
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  if (mins === 0) return `${hours}h`
  return `${hours}h ${mins}m`
}

export function estimateTrafficLabel(multiplier: number): string {
  if (multiplier <= 1.0) return 'Light traffic'
  if (multiplier <= 1.2) return 'Moderate traffic'
  if (multiplier <= 1.4) return 'Heavy traffic'
  return 'Very heavy traffic'
}
