import { useTrip } from './hooks/useTrip'
import { ItineraryTimeline } from './components/ItineraryTimeline'
import { AddItemPanel } from './components/AddItemPanel'
import { BudgetSummary } from './components/BudgetSummary'
import { SettingsPanel } from './components/SettingsPanel'
import { StopsList } from './components/StopsList'
import { formatDate } from './utils/format'

function App() {
  const tripActions = useTrip()
  const { trip } = tripActions

  return (
    <div className="min-h-screen">
      <header className="bg-gradient-to-r from-ocean-800 to-ocean-600 text-white">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-6">
          <div className="flex items-center justify-between flex-wrap gap-4">
            <div>
              <h1 className="text-2xl sm:text-3xl font-bold tracking-tight">
                Voyage
              </h1>
              <p className="text-ocean-100 text-sm mt-1">
                Holiday planning &amp; itinerary
              </p>
            </div>
            <div className="flex items-center gap-3">
              <input
                type="text"
                value={trip.name}
                onChange={(e) => tripActions.updateTrip({ name: e.target.value })}
                className="bg-white/10 border border-white/20 rounded-lg px-3 py-1.5 text-sm font-medium placeholder:text-ocean-200 focus:outline-none focus:ring-2 focus:ring-white/40 w-48 sm:w-64"
              />
              <select
                value={trip.currency}
                onChange={(e) => tripActions.updateTrip({ currency: e.target.value })}
                className="bg-white/10 border border-white/20 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-white/40"
              >
                <option value="AUD">AUD</option>
                <option value="USD">USD</option>
                <option value="EUR">EUR</option>
                <option value="GBP">GBP</option>
                <option value="CHF">CHF</option>
              </select>
            </div>
          </div>
          <div className="mt-4 text-ocean-100 text-sm">
            {formatDate(trip.startDate)} — {formatDate(trip.endDate)}
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 sm:px-6 py-8">
        <div className="grid grid-cols-1 lg:grid-cols-[340px_1fr] gap-8">
          <aside className="space-y-4 lg:sticky lg:top-4 lg:self-start">
            <AddItemPanel tripActions={tripActions} />
            <StopsList stops={trip.stops} onRemove={tripActions.removeStop} />
            <BudgetSummary trip={trip} />
            <SettingsPanel
              settings={trip.settings}
              onChange={tripActions.updateSettings}
            />
            <div className="flex gap-2">
              <button
                type="button"
                onClick={tripActions.resetToSample}
                className="flex-1 text-xs text-slate-500 hover:text-ocean-600 py-2"
              >
                Load sample trip
              </button>
              <button
                type="button"
                onClick={tripActions.resetToEmpty}
                className="flex-1 text-xs text-slate-500 hover:text-red-500 py-2"
              >
                Clear all
              </button>
            </div>
          </aside>

          <section>
            <div className="mb-6">
              <h2 className="text-xl font-bold text-slate-800">Itinerary</h2>
              <p className="text-sm text-slate-500 mt-1">
                Tap any line item to see full details, travel times, and booking
                info
              </p>
            </div>
            <ItineraryTimeline
              trip={trip}
              onRemoveItem={tripActions.removeItem}
              onAddAttachment={tripActions.addAttachment}
              onRemoveAttachment={tripActions.removeAttachment}
            />
          </section>
        </div>
      </main>
    </div>
  )
}

export default App
