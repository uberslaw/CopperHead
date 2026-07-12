import type { TripSettings } from '../types'
import { estimateTrafficLabel } from '../utils/travelTime'

interface SettingsPanelProps {
  settings: TripSettings
  onChange: (settings: Partial<TripSettings>) => void
}

export function SettingsPanel({ settings, onChange }: SettingsPanelProps) {
  return (
    <div className="bg-white rounded-xl border border-sand-200 p-4 shadow-sm">
      <h3 className="text-sm font-semibold text-slate-500 uppercase tracking-wide mb-3">
        Time Buffers
      </h3>
      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">
            Airport arrival buffer
          </label>
          <div className="flex items-center gap-2">
            <input
              type="range"
              min={60}
              max={240}
              step={15}
              value={settings.airportBufferMinutes}
              onChange={(e) =>
                onChange({ airportBufferMinutes: Number(e.target.value) })
              }
              className="flex-1 accent-ocean-600"
            />
            <span className="text-sm font-medium text-slate-700 w-16 text-right">
              {settings.airportBufferMinutes} min
            </span>
          </div>
          <p className="text-xs text-slate-400 mt-1">
            How early to arrive at the airport before departure
          </p>
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">
            General comfort buffer
          </label>
          <div className="flex items-center gap-2">
            <input
              type="range"
              min={0}
              max={60}
              step={5}
              value={settings.generalBufferMinutes}
              onChange={(e) =>
                onChange({ generalBufferMinutes: Number(e.target.value) })
              }
              className="flex-1 accent-ocean-600"
            />
            <span className="text-sm font-medium text-slate-700 w-16 text-right">
              {settings.generalBufferMinutes} min
            </span>
          </div>
          <p className="text-xs text-slate-400 mt-1">
            Extra padding added to all travel estimates
          </p>
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">
            Traffic conditions
          </label>
          <div className="flex items-center gap-2">
            <input
              type="range"
              min={1}
              max={1.6}
              step={0.05}
              value={settings.trafficMultiplier}
              onChange={(e) =>
                onChange({ trafficMultiplier: Number(e.target.value) })
              }
              className="flex-1 accent-ocean-600"
            />
            <span className="text-xs font-medium text-slate-700 w-20 text-right">
              {estimateTrafficLabel(settings.trafficMultiplier)}
            </span>
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">
            Average travel speed
          </label>
          <div className="flex items-center gap-2">
            <input
              type="range"
              min={20}
              max={100}
              step={5}
              value={settings.averageSpeedKmh}
              onChange={(e) =>
                onChange({ averageSpeedKmh: Number(e.target.value) })
              }
              className="flex-1 accent-ocean-600"
            />
            <span className="text-sm font-medium text-slate-700 w-16 text-right">
              {settings.averageSpeedKmh} km/h
            </span>
          </div>
        </div>
      </div>
    </div>
  )
}
