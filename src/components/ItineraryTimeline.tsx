import type { Trip } from '../types'
import { buildTimeline } from '../utils/itinerary'
import { formatDateHeader } from '../utils/format'
import { TimelineItem } from './TimelineItem'

interface ItineraryTimelineProps {
  trip: Trip
  onRemoveItem: (id: string) => void
  onAddAttachment: (
    itemId: string,
    attachment: { name: string; mimeType: string; dataUrl: string },
  ) => void
  onRemoveAttachment: (itemId: string, attachmentId: string) => void
}

export function ItineraryTimeline({
  trip,
  onRemoveItem,
  onAddAttachment,
  onRemoveAttachment,
}: ItineraryTimelineProps) {
  const timeline = buildTimeline(trip)
  const dates = Array.from(timeline.keys()).sort()

  if (dates.length === 0) {
    return (
      <div className="text-center py-16">
        <div className="text-5xl mb-4">🗺️</div>
        <h2 className="text-xl font-semibold text-slate-700 mb-2">
          Your itinerary is empty
        </h2>
        <p className="text-slate-500 max-w-md mx-auto">
          Add flights, accommodation, rental cars, sightseeing, and transport to
          build your chronological trip plan.
        </p>
      </div>
    )
  }

  return (
    <div className="space-y-8">
      {dates.map((dateKey) => {
        const entries = timeline.get(dateKey)!
        const firstEntry = entries[0]
        return (
          <section key={dateKey}>
            <h2 className="text-lg font-semibold text-ocean-800 mb-4 sticky top-0 bg-sand-50/90 backdrop-blur py-2 z-10">
              {formatDateHeader(firstEntry.sortKey)}
            </h2>
            <div className="space-y-2">
              {entries.map((entry) => (
                <TimelineItem
                  key={entry.item.id}
                  entry={entry}
                  trip={trip}
                  onRemove={onRemoveItem}
                  onAddAttachment={onAddAttachment}
                  onRemoveAttachment={onRemoveAttachment}
                />
              ))}
            </div>
          </section>
        )
      })}
    </div>
  )
}
