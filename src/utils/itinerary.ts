import { parseISO, format } from 'date-fns'
import type {
  ItineraryItem,
  FlightItem,
  Trip,
  TripSettings,
} from '../types'
import { calculateTravelMinutes } from './travelTime'
import { formatTime, formatDate } from './format'

export interface TimelineEntry {
  item: ItineraryItem
  sortKey: string
  summary: string
  dateKey: string
}

export function getItemDateTime(item: ItineraryItem): string {
  switch (item.type) {
    case 'flight':
      return item.departureDateTime
    case 'accommodation':
      return item.checkIn
    case 'rental_car':
      return item.pickupDateTime
    case 'sightseeing':
      return item.startDateTime
    case 'transport':
      return item.dateTime
  }
}

export function getItemCost(item: ItineraryItem): number {
  switch (item.type) {
    case 'flight':
      return item.cost ?? 0
    case 'accommodation': {
      const nights = Math.max(
        1,
        Math.ceil(
          (parseISO(item.checkOut).getTime() - parseISO(item.checkIn).getTime()) /
            (1000 * 60 * 60 * 24),
        ),
      )
      return item.cost ?? (item.nightlyRate ?? 0) * nights
    }
    case 'rental_car': {
      const days = Math.max(
        1,
        Math.ceil(
          (parseISO(item.dropoffDateTime).getTime() -
            parseISO(item.pickupDateTime).getTime()) /
            (1000 * 60 * 60 * 24),
        ),
      )
      return item.cost ?? (item.dailyRate ?? 0) * days
    }
    case 'sightseeing':
      return item.cost ?? item.ticketPrice ?? 0
    case 'transport':
      return item.cost ?? 0
  }
}

export function buildSummary(item: ItineraryItem): string {
  switch (item.type) {
    case 'flight':
      return `Flight (${item.departureCity} to ${item.arrivalCity}) ${formatTime(item.departureDateTime)} ${formatDate(item.departureDateTime)}`
    case 'accommodation':
      return `Check in — ${item.name} ${formatTime(item.checkIn)} ${formatDate(item.checkIn)}`
    case 'rental_car':
      return `Pick up rental car (${item.company}) ${formatTime(item.pickupDateTime)} ${formatDate(item.pickupDateTime)}`
    case 'sightseeing':
      return `${item.name} ${formatTime(item.startDateTime)} ${formatDate(item.startDateTime)}`
    case 'transport':
      return `Transport (${item.from} → ${item.to}) ${formatTime(item.dateTime)} ${formatDate(item.dateTime)}`
  }
}

export function buildTimeline(trip: Trip): Map<string, TimelineEntry[]> {
  const entries: TimelineEntry[] = trip.items.map((item) => {
    const dt = getItemDateTime(item)
    return {
      item,
      sortKey: dt,
      summary: buildSummary(item),
      dateKey: format(parseISO(dt), 'yyyy-MM-dd'),
    }
  })

  entries.sort((a, b) => a.sortKey.localeCompare(b.sortKey))

  const grouped = new Map<string, TimelineEntry[]>()
  for (const entry of entries) {
    const list = grouped.get(entry.dateKey) ?? []
    list.push(entry)
    grouped.set(entry.dateKey, list)
  }
  return grouped
}

export function getTotalBudget(trip: Trip): number {
  return trip.items.reduce((sum, item) => sum + getItemCost(item), 0)
}

export function getBudgetByCategory(trip: Trip): Record<string, number> {
  const categories: Record<string, number> = {
    Flights: 0,
    Accommodation: 0,
    'Rental cars': 0,
    Sightseeing: 0,
    Transport: 0,
  }

  for (const item of trip.items) {
    const cost = getItemCost(item)
    switch (item.type) {
      case 'flight':
        categories.Flights += cost
        break
      case 'accommodation':
        categories.Accommodation += cost
        break
      case 'rental_car':
        categories['Rental cars'] += cost
        break
      case 'sightseeing':
        categories.Sightseeing += cost
        break
      case 'transport':
        categories.Transport += cost
        break
    }
  }
  return categories
}

export interface FlightTravelInfo {
  travelMinutes: number
  recommendedDeparture: Date
  airportBufferMinutes: number
}

export function getFlightTravelInfo(
  flight: FlightItem,
  settings: TripSettings,
): FlightTravelInfo {
  const travelMinutes = calculateTravelMinutes(flight.departureDistanceKm, settings)
  const recommendedDeparture = new Date(
    parseISO(flight.departureDateTime).getTime() -
      (travelMinutes + settings.airportBufferMinutes) * 60_000,
  )
  return {
    travelMinutes,
    recommendedDeparture,
    airportBufferMinutes: settings.airportBufferMinutes,
  }
}

export interface DetailRow {
  label: string
  value: string | number | undefined
}

export function getItemDetails(
  item: ItineraryItem,
  trip: Trip,
): DetailRow[] {
  switch (item.type) {
    case 'flight': {
      const info = getFlightTravelInfo(item, trip.settings)
      const details: DetailRow[] = [
        { label: 'Departure airport', value: item.departureAirport },
        { label: 'Arrival airport', value: item.arrivalAirport },
        { label: 'Departure', value: formatTime(item.departureDateTime) },
        { label: 'Arrival', value: formatTime(item.arrivalDateTime) },
        { label: 'Duration', value: `${item.durationMinutes} min` },
      ]
      if (item.airline) details.push({ label: 'Airline', value: item.airline })
      if (item.flightNumber)
        details.push({ label: 'Flight number', value: item.flightNumber })
      if (item.connections.length > 0) {
        details.push({
          label: 'Connections',
          value: item.connections
            .map((c) => `${c.airport} (${c.layoverMinutes} min layover)`)
            .join(', '),
        })
      }
      if (item.cost) details.push({ label: 'Ticket cost', value: item.cost })
      details.push(
        { label: 'Leave from', value: item.departureLocation },
        {
          label: 'Distance to airport',
          value: `${item.departureDistanceKm} km`,
        },
        { label: 'Est. travel time', value: `${info.travelMinutes} min` },
        {
          label: 'Airport buffer',
          value: `${info.airportBufferMinutes} min`,
        },
        {
          label: 'Recommended departure',
          value: format(info.recommendedDeparture, "h:mma 'on' EEE d/M/yy"),
        },
      )
      return details
    }
    case 'accommodation':
      return [
        { label: 'Name', value: item.name },
        { label: 'Address', value: item.address },
        { label: 'Check-in', value: formatTime(item.checkIn) },
        { label: 'Check-out', value: formatTime(item.checkOut) },
        ...(item.nightlyRate
          ? [{ label: 'Nightly rate', value: item.nightlyRate }]
          : []),
        ...(item.cost ? [{ label: 'Total cost', value: item.cost }] : []),
      ]
    case 'rental_car':
      return [
        { label: 'Company', value: item.company },
        ...(item.vehicleType
          ? [{ label: 'Vehicle', value: item.vehicleType }]
          : []),
        { label: 'Pick up', value: item.pickupLocation },
        { label: 'Drop off', value: item.dropoffLocation },
        { label: 'Pick up time', value: formatTime(item.pickupDateTime) },
        { label: 'Drop off time', value: formatTime(item.dropoffDateTime) },
        ...(item.dailyRate
          ? [{ label: 'Daily rate', value: item.dailyRate }]
          : []),
        ...(item.cost ? [{ label: 'Total cost', value: item.cost }] : []),
      ]
    case 'sightseeing':
      return [
        { label: 'Location', value: item.location },
        { label: 'Start', value: formatTime(item.startDateTime) },
        ...(item.endDateTime
          ? [{ label: 'End', value: formatTime(item.endDateTime) }]
          : []),
        ...(item.openingHours
          ? [{ label: 'Opening hours', value: item.openingHours }]
          : []),
        ...(item.ticketPrice
          ? [{ label: 'Ticket price', value: item.ticketPrice }]
          : []),
        ...(item.cost ? [{ label: 'Total cost', value: item.cost }] : []),
      ]
    case 'transport': {
      const details: DetailRow[] = [
        { label: 'Mode', value: item.mode },
        { label: 'From', value: item.from },
        { label: 'To', value: item.to },
      ]
      if (item.distanceKm) {
        const mins =
          item.durationMinutes ??
          calculateTravelMinutes(item.distanceKm, trip.settings)
        details.push(
          { label: 'Distance', value: `${item.distanceKm} km` },
          { label: 'Est. duration', value: `${mins} min` },
        )
      } else if (item.durationMinutes) {
        details.push({ label: 'Duration', value: `${item.durationMinutes} min` })
      }
      if (item.cost) details.push({ label: 'Cost', value: item.cost })
      return details
    }
  }
}

export function getItemIcon(type: ItineraryItem['type']): string {
  switch (type) {
    case 'flight':
      return '✈️'
    case 'accommodation':
      return '🏨'
    case 'rental_car':
      return '🚗'
    case 'sightseeing':
      return '📍'
    case 'transport':
      return '🚌'
  }
}
