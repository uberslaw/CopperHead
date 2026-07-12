import { useState } from 'react'
import type { AddItemType, Attachment } from '../types'
import type { UseTripReturn } from '../hooks/useTrip'
import { fromDatetimeLocal } from '../utils/format'
import { FileUpload } from './FileUpload'

interface AddItemPanelProps {
  tripActions: UseTripReturn
}

const TABS: { type: AddItemType; label: string; icon: string }[] = [
  { type: 'flight', label: 'Flight', icon: '✈️' },
  { type: 'accommodation', label: 'Stay', icon: '🏨' },
  { type: 'rental_car', label: 'Car', icon: '🚗' },
  { type: 'sightseeing', label: 'Sightseeing', icon: '📍' },
  { type: 'transport', label: 'Transport', icon: '🚌' },
]

export function AddItemPanel({ tripActions }: AddItemPanelProps) {
  const [activeTab, setActiveTab] = useState<AddItemType>('flight')
  const [showStopForm, setShowStopForm] = useState(false)
  const { trip, addItem, addStop } = tripActions

  const [pendingAttachments, setPendingAttachments] = useState<
    Omit<Attachment, 'id'>[]
  >([])

  const handleAttachment = (att: Omit<Attachment, 'id'>) => {
    setPendingAttachments((prev) => [...prev, att])
  }

  const resetAttachments = () => setPendingAttachments([])

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const form = new FormData(e.currentTarget)
    const base = {
      cost: form.get('cost') ? Number(form.get('cost')) : undefined,
      notes: (form.get('notes') as string) || undefined,
      bookingReference: (form.get('bookingReference') as string) || undefined,
      attachments: pendingAttachments,
    }

    switch (activeTab) {
      case 'flight':
        addItem({
          type: 'flight',
          ...base,
          departureCity: form.get('departureCity') as string,
          arrivalCity: form.get('arrivalCity') as string,
          departureAirport: form.get('departureAirport') as string,
          arrivalAirport: form.get('arrivalAirport') as string,
          departureDateTime: fromDatetimeLocal(form.get('departureDateTime') as string),
          arrivalDateTime: fromDatetimeLocal(form.get('arrivalDateTime') as string),
          durationMinutes: Number(form.get('durationMinutes')),
          airline: (form.get('airline') as string) || undefined,
          flightNumber: (form.get('flightNumber') as string) || undefined,
          connections: [],
          departureLocation: form.get('departureLocation') as string,
          departureDistanceKm: Number(form.get('departureDistanceKm')),
        })
        break
      case 'accommodation':
        addItem({
          type: 'accommodation',
          ...base,
          name: form.get('name') as string,
          address: form.get('address') as string,
          checkIn: fromDatetimeLocal(form.get('checkIn') as string),
          checkOut: fromDatetimeLocal(form.get('checkOut') as string),
          nightlyRate: form.get('nightlyRate')
            ? Number(form.get('nightlyRate'))
            : undefined,
        })
        break
      case 'rental_car':
        addItem({
          type: 'rental_car',
          ...base,
          company: form.get('company') as string,
          vehicleType: (form.get('vehicleType') as string) || undefined,
          pickupLocation: form.get('pickupLocation') as string,
          dropoffLocation: form.get('dropoffLocation') as string,
          pickupDateTime: fromDatetimeLocal(form.get('pickupDateTime') as string),
          dropoffDateTime: fromDatetimeLocal(form.get('dropoffDateTime') as string),
          dailyRate: form.get('dailyRate')
            ? Number(form.get('dailyRate'))
            : undefined,
        })
        break
      case 'sightseeing':
        addItem({
          type: 'sightseeing',
          ...base,
          name: form.get('name') as string,
          location: form.get('location') as string,
          startDateTime: fromDatetimeLocal(form.get('startDateTime') as string),
          endDateTime: form.get('endDateTime')
            ? fromDatetimeLocal(form.get('endDateTime') as string)
            : undefined,
          ticketPrice: form.get('ticketPrice')
            ? Number(form.get('ticketPrice'))
            : undefined,
          openingHours: (form.get('openingHours') as string) || undefined,
        })
        break
      case 'transport':
        addItem({
          type: 'transport',
          ...base,
          mode: form.get('mode') as 'taxi' | 'train' | 'bus' | 'walk' | 'metro' | 'other',
          from: form.get('from') as string,
          to: form.get('to') as string,
          dateTime: fromDatetimeLocal(form.get('dateTime') as string),
          distanceKm: form.get('distanceKm')
            ? Number(form.get('distanceKm'))
            : undefined,
          durationMinutes: form.get('durationMinutes')
            ? Number(form.get('durationMinutes'))
            : undefined,
        })
        break
    }

    e.currentTarget.reset()
    resetAttachments()
  }

  const handleStopSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const form = new FormData(e.currentTarget)
    addStop({
      name: form.get('name') as string,
      location: form.get('location') as string,
      arrivalDate: form.get('arrivalDate') as string,
      departureDate: form.get('departureDate') as string,
      notes: (form.get('notes') as string) || undefined,
    })
    e.currentTarget.reset()
    setShowStopForm(false)
  }

  const inputClass =
    'w-full rounded-lg border border-sand-200 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ocean-300 focus:border-ocean-400'
  const labelClass = 'block text-xs font-medium text-slate-600 mb-1'

  return (
    <div className="space-y-4">
      <div className="bg-white rounded-xl border border-sand-200 p-4 shadow-sm">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-semibold text-slate-500 uppercase tracking-wide">
            Trip Dates
          </h3>
        </div>
        <div className="grid grid-cols-2 gap-2">
          <div>
            <label className={labelClass}>Start</label>
            <input
              type="date"
              value={trip.startDate}
              onChange={(e) => tripActions.updateTrip({ startDate: e.target.value })}
              className={inputClass}
            />
          </div>
          <div>
            <label className={labelClass}>End</label>
            <input
              type="date"
              value={trip.endDate}
              onChange={(e) => tripActions.updateTrip({ endDate: e.target.value })}
              className={inputClass}
            />
          </div>
        </div>
      </div>

      <div className="bg-white rounded-xl border border-sand-200 p-4 shadow-sm">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-semibold text-slate-500 uppercase tracking-wide">
            Stops
          </h3>
          <button
            type="button"
            onClick={() => setShowStopForm(!showStopForm)}
            className="text-xs text-ocean-600 hover:text-ocean-800 font-medium"
          >
            {showStopForm ? 'Cancel' : '+ Add stop'}
          </button>
        </div>
        {showStopForm && (
          <form onSubmit={handleStopSubmit} className="space-y-2 mb-3">
            <input name="name" placeholder="City name" required className={inputClass} />
            <input name="location" placeholder="Country / region" required className={inputClass} />
            <div className="grid grid-cols-2 gap-2">
              <input name="arrivalDate" type="date" required className={inputClass} />
              <input name="departureDate" type="date" required className={inputClass} />
            </div>
            <button
              type="submit"
              className="w-full bg-ocean-600 text-white rounded-lg py-2 text-sm font-medium hover:bg-ocean-700"
            >
              Save stop
            </button>
          </form>
        )}
      </div>

      <div className="bg-white rounded-xl border border-sand-200 p-4 shadow-sm">
        <h3 className="text-sm font-semibold text-slate-500 uppercase tracking-wide mb-3">
          Add to Itinerary
        </h3>

        <div className="flex flex-wrap gap-1 mb-4">
          {TABS.map((tab) => (
            <button
              key={tab.type}
              type="button"
              onClick={() => setActiveTab(tab.type)}
              className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-colors ${
                activeTab === tab.type
                  ? 'bg-ocean-600 text-white'
                  : 'bg-sand-100 text-slate-600 hover:bg-sand-200'
              }`}
            >
              {tab.icon} {tab.label}
            </button>
          ))}
        </div>

        <form onSubmit={handleSubmit} className="space-y-2">
          {activeTab === 'flight' && (
            <>
              <div className="grid grid-cols-2 gap-2">
                <input name="departureCity" placeholder="From city" required className={inputClass} />
                <input name="arrivalCity" placeholder="To city" required className={inputClass} />
              </div>
              <input name="departureAirport" placeholder="Departure airport" required className={inputClass} />
              <input name="arrivalAirport" placeholder="Arrival airport" required className={inputClass} />
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className={labelClass}>Departure</label>
                  <input name="departureDateTime" type="datetime-local" required className={inputClass} />
                </div>
                <div>
                  <label className={labelClass}>Arrival</label>
                  <input name="arrivalDateTime" type="datetime-local" required className={inputClass} />
                </div>
              </div>
              <input name="durationMinutes" type="number" placeholder="Duration (minutes)" required className={inputClass} />
              <div className="grid grid-cols-2 gap-2">
                <input name="airline" placeholder="Airline" className={inputClass} />
                <input name="flightNumber" placeholder="Flight number" className={inputClass} />
              </div>
              <input name="departureLocation" placeholder="Leaving from (e.g. home address)" required className={inputClass} />
              <input name="departureDistanceKm" type="number" step="0.1" placeholder="Distance to airport (km)" required className={inputClass} />
            </>
          )}

          {activeTab === 'accommodation' && (
            <>
              <input name="name" placeholder="Hotel / property name" required className={inputClass} />
              <input name="address" placeholder="Address" required className={inputClass} />
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className={labelClass}>Check-in</label>
                  <input name="checkIn" type="datetime-local" required className={inputClass} />
                </div>
                <div>
                  <label className={labelClass}>Check-out</label>
                  <input name="checkOut" type="datetime-local" required className={inputClass} />
                </div>
              </div>
              <input name="nightlyRate" type="number" placeholder="Nightly rate (optional)" className={inputClass} />
            </>
          )}

          {activeTab === 'rental_car' && (
            <>
              <input name="company" placeholder="Rental company" required className={inputClass} />
              <input name="vehicleType" placeholder="Vehicle type" className={inputClass} />
              <input name="pickupLocation" placeholder="Pick up location" required className={inputClass} />
              <input name="dropoffLocation" placeholder="Drop off location" required className={inputClass} />
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className={labelClass}>Pick up</label>
                  <input name="pickupDateTime" type="datetime-local" required className={inputClass} />
                </div>
                <div>
                  <label className={labelClass}>Drop off</label>
                  <input name="dropoffDateTime" type="datetime-local" required className={inputClass} />
                </div>
              </div>
              <input name="dailyRate" type="number" placeholder="Daily rate (optional)" className={inputClass} />
            </>
          )}

          {activeTab === 'sightseeing' && (
            <>
              <input name="name" placeholder="Activity name" required className={inputClass} />
              <input name="location" placeholder="Location" required className={inputClass} />
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className={labelClass}>Start</label>
                  <input name="startDateTime" type="datetime-local" required className={inputClass} />
                </div>
                <div>
                  <label className={labelClass}>End</label>
                  <input name="endDateTime" type="datetime-local" className={inputClass} />
                </div>
              </div>
              <input name="ticketPrice" type="number" step="0.01" placeholder="Ticket price" className={inputClass} />
              <input name="openingHours" placeholder="Opening hours" className={inputClass} />
            </>
          )}

          {activeTab === 'transport' && (
            <>
              <select name="mode" required className={inputClass}>
                <option value="train">Train</option>
                <option value="taxi">Taxi</option>
                <option value="bus">Bus</option>
                <option value="metro">Metro</option>
                <option value="walk">Walk</option>
                <option value="other">Other</option>
              </select>
              <input name="from" placeholder="From" required className={inputClass} />
              <input name="to" placeholder="To" required className={inputClass} />
              <div>
                <label className={labelClass}>Date & time</label>
                <input name="dateTime" type="datetime-local" required className={inputClass} />
              </div>
              <div className="grid grid-cols-2 gap-2">
                <input name="distanceKm" type="number" step="0.1" placeholder="Distance (km)" className={inputClass} />
                <input name="durationMinutes" type="number" placeholder="Duration (min)" className={inputClass} />
              </div>
            </>
          )}

          <input name="cost" type="number" step="0.01" placeholder="Total cost (optional)" className={inputClass} />
          <input name="bookingReference" placeholder="Booking reference" className={inputClass} />
          <textarea name="notes" placeholder="Notes" rows={2} className={inputClass} />

          <FileUpload onUpload={handleAttachment} />
          {pendingAttachments.length > 0 && (
            <p className="text-xs text-slate-500">
              {pendingAttachments.length} file(s) attached
            </p>
          )}

          <button
            type="submit"
            className="w-full bg-ocean-600 text-white rounded-lg py-2.5 text-sm font-semibold hover:bg-ocean-700 transition-colors"
          >
            Add to itinerary
          </button>
        </form>
      </div>
    </div>
  )
}
