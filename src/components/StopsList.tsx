import type { Stop } from '../types'
import { formatDate } from '../utils/format'

interface StopsListProps {
  stops: Stop[]
  onRemove: (id: string) => void
}

export function StopsList({ stops, onRemove }: StopsListProps) {
  if (stops.length === 0) return null

  return (
    <div className="bg-white rounded-xl border border-sand-200 p-4 shadow-sm">
      <h3 className="text-sm font-semibold text-slate-500 uppercase tracking-wide mb-3">
        Stops
      </h3>
      <ul className="space-y-2">
        {stops.map((stop) => (
          <li
            key={stop.id}
            className="flex items-start justify-between gap-2 text-sm"
          >
            <div>
              <span className="font-medium text-slate-800">{stop.name}</span>
              <span className="text-slate-500"> — {stop.location}</span>
              <div className="text-xs text-slate-400">
                {formatDate(stop.arrivalDate)} → {formatDate(stop.departureDate)}
              </div>
            </div>
            <button
              type="button"
              onClick={() => onRemove(stop.id)}
              className="text-slate-300 hover:text-red-500 shrink-0"
              aria-label={`Remove ${stop.name}`}
            >
              ×
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}
