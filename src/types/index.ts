export type ItineraryItemType =
  | 'flight'
  | 'accommodation'
  | 'rental_car'
  | 'sightseeing'
  | 'transport'

export interface Attachment {
  id: string
  name: string
  mimeType: string
  dataUrl: string
}

export interface FlightConnection {
  airport: string
  layoverMinutes: number
}

export interface TripSettings {
  airportBufferMinutes: number
  generalBufferMinutes: number
  trafficMultiplier: number
  averageSpeedKmh: number
}

export interface Stop {
  id: string
  name: string
  location: string
  arrivalDate: string
  departureDate: string
  notes?: string
}

export interface BaseItem {
  id: string
  type: ItineraryItemType
  stopId?: string
  cost?: number
  notes?: string
  bookingReference?: string
  attachments: Attachment[]
}

export interface FlightItem extends BaseItem {
  type: 'flight'
  departureCity: string
  arrivalCity: string
  departureAirport: string
  arrivalAirport: string
  departureDateTime: string
  arrivalDateTime: string
  durationMinutes: number
  airline?: string
  flightNumber?: string
  connections: FlightConnection[]
  departureLocation: string
  departureDistanceKm: number
}

export interface AccommodationItem extends BaseItem {
  type: 'accommodation'
  name: string
  address: string
  checkIn: string
  checkOut: string
  nightlyRate?: number
}

export interface RentalCarItem extends BaseItem {
  type: 'rental_car'
  company: string
  vehicleType?: string
  pickupLocation: string
  dropoffLocation: string
  pickupDateTime: string
  dropoffDateTime: string
  dailyRate?: number
}

export interface SightseeingItem extends BaseItem {
  type: 'sightseeing'
  name: string
  location: string
  startDateTime: string
  endDateTime?: string
  ticketPrice?: number
  openingHours?: string
}

export interface TransportItem extends BaseItem {
  type: 'transport'
  mode: 'taxi' | 'train' | 'bus' | 'walk' | 'metro' | 'other'
  from: string
  to: string
  dateTime: string
  distanceKm?: number
  durationMinutes?: number
}

export type ItineraryItem =
  | FlightItem
  | AccommodationItem
  | RentalCarItem
  | SightseeingItem
  | TransportItem

type ItemInput<T extends ItineraryItem> = Omit<T, 'id' | 'attachments'> & {
  attachments?: Omit<Attachment, 'id'>[]
}

export type NewItineraryItem =
  | ItemInput<FlightItem>
  | ItemInput<AccommodationItem>
  | ItemInput<RentalCarItem>
  | ItemInput<SightseeingItem>
  | ItemInput<TransportItem>

export interface Trip {
  id: string
  name: string
  startDate: string
  endDate: string
  currency: string
  settings: TripSettings
  stops: Stop[]
  items: ItineraryItem[]
}

export type AddItemType = ItineraryItemType
