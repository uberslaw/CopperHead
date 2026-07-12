import type { Trip } from '../types'
import { getTotalBudget, getBudgetByCategory } from '../utils/itinerary'
import { formatCurrency } from '../utils/format'

interface BudgetSummaryProps {
  trip: Trip
}

export function BudgetSummary({ trip }: BudgetSummaryProps) {
  const total = getTotalBudget(trip)
  const byCategory = getBudgetByCategory(trip)
  const categories = Object.entries(byCategory).filter(([, v]) => v > 0)

  return (
    <div className="bg-white rounded-xl border border-sand-200 p-4 shadow-sm">
      <h3 className="text-sm font-semibold text-slate-500 uppercase tracking-wide mb-3">
        Budget
      </h3>
      <div className="text-2xl font-bold text-ocean-800 mb-4">
        {formatCurrency(total, trip.currency)}
      </div>
      {categories.length > 0 && (
        <ul className="space-y-2">
          {categories.map(([name, amount]) => (
            <li key={name} className="flex justify-between text-sm">
              <span className="text-slate-600">{name}</span>
              <span className="font-medium text-slate-800">
                {formatCurrency(amount, trip.currency)}
              </span>
            </li>
          ))}
        </ul>
      )}
      {trip.items.length === 0 && (
        <p className="text-sm text-slate-400">Add items to see your budget</p>
      )}
    </div>
  )
}
