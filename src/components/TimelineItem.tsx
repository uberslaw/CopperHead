import { useState } from 'react'
import type { ItineraryItem, Trip } from '../types'
import {
  getItemDetails,
  getItemCost,
  getItemIcon,
  type DetailRow,
} from '../utils/itinerary'
import { formatCurrency } from '../utils/format'
import { AttachmentList } from './FileUpload'

interface TimelineItemProps {
  entry: { item: ItineraryItem; summary: string }
  trip: Trip
  onRemove: (id: string) => void
  onAddAttachment: (
    itemId: string,
    attachment: { name: string; mimeType: string; dataUrl: string },
  ) => void
  onRemoveAttachment: (itemId: string, attachmentId: string) => void
}

export function TimelineItem({
  entry,
  trip,
  onRemove,
  onAddAttachment,
  onRemoveAttachment,
}: TimelineItemProps) {
  const [expanded, setExpanded] = useState(false)
  const { item, summary } = entry
  const details = getItemDetails(item, trip) as DetailRow[]
  const cost = getItemCost(item)
  const icon = getItemIcon(item.type)

  return (
    <div className="group">
      <button
        type="button"
        onClick={() => setExpanded(!expanded)}
        className={`w-full text-left flex items-center gap-3 px-4 py-3 rounded-xl transition-all ${
          expanded
            ? 'bg-ocean-50 border border-ocean-200 shadow-sm'
            : 'bg-white border border-sand-200 hover:border-ocean-200 hover:shadow-sm'
        }`}
      >
        <span className="text-xl shrink-0" aria-hidden>
          {icon}
        </span>
        <span className="flex-1 text-sm font-medium text-slate-800">
          {summary}
        </span>
        {cost > 0 && (
          <span className="text-sm text-slate-500 shrink-0">
            {formatCurrency(cost, trip.currency)}
          </span>
        )}
        <span
          className={`text-slate-400 transition-transform shrink-0 ${expanded ? 'rotate-180' : ''}`}
        >
          ▾
        </span>
      </button>

      {expanded && (
        <div className="mt-2 ml-4 pl-6 border-l-2 border-ocean-200 pb-2">
          <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-2 text-sm">
            {details.map((row) => (
              <div key={row.label}>
                <dt className="text-slate-400 text-xs uppercase tracking-wide">
                  {row.label}
                </dt>
                <dd className="text-slate-800 font-medium">{row.value}</dd>
              </div>
            ))}
          </dl>

          {item.bookingReference && (
            <div className="mt-3 text-sm">
              <span className="text-slate-400 text-xs uppercase tracking-wide">
                Booking ref
              </span>
              <p className="font-mono text-slate-800">{item.bookingReference}</p>
            </div>
          )}

          {item.notes && (
            <div className="mt-3 text-sm">
              <span className="text-slate-400 text-xs uppercase tracking-wide">
                Notes
              </span>
              <p className="text-slate-600">{item.notes}</p>
            </div>
          )}

          <AttachmentList
            attachments={item.attachments}
            onRemove={(id) => onRemoveAttachment(item.id, id)}
          />

          <div className="flex gap-3 mt-4">
            <label className="text-sm text-ocean-600 hover:text-ocean-800 font-medium cursor-pointer">
              + Attach file
              <input
                type="file"
                accept=".pdf,.png,.jpg,.jpeg,.webp,.gif"
                className="hidden"
                onChange={(e) => {
                  const file = e.target.files?.[0]
                  if (!file) return
                  const reader = new FileReader()
                  reader.onload = () => {
                    onAddAttachment(item.id, {
                      name: file.name,
                      mimeType: file.type,
                      dataUrl: reader.result as string,
                    })
                  }
                  reader.readAsDataURL(file)
                  e.target.value = ''
                }}
              />
            </label>
            <button
              type="button"
              onClick={() => onRemove(item.id)}
              className="text-sm text-red-500 hover:text-red-700 font-medium"
            >
              Remove
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
