import { useCallback, useEffect, useState } from 'react'
import { v4 as uuid } from 'uuid'
import type {
  Trip,
  ItineraryItem,
  Stop,
  TripSettings,
  NewItineraryItem,
  Attachment,
} from '../types'
import { loadTrip, saveTrip } from '../utils/storage'
import { createSampleTrip, createEmptyTrip } from '../utils/sampleData'

export function useTrip() {
  const [trip, setTrip] = useState<Trip>(() => loadTrip() ?? createSampleTrip())

  useEffect(() => {
    saveTrip(trip)
  }, [trip])

  const updateTrip = useCallback((updates: Partial<Trip>) => {
    setTrip((prev) => ({ ...prev, ...updates }))
  }, [])

  const updateSettings = useCallback((settings: Partial<TripSettings>) => {
    setTrip((prev) => ({
      ...prev,
      settings: { ...prev.settings, ...settings },
    }))
  }, [])

  const addItem = useCallback((item: NewItineraryItem) => {
    const { attachments = [], ...rest } = item
    setTrip((prev) => ({
      ...prev,
      items: [
        ...prev.items,
        {
          ...rest,
          id: uuid(),
          attachments: attachments.map((a) => ({ ...a, id: uuid() })),
        } as ItineraryItem,
      ],
    }))
  }, [])

  const updateItem = useCallback((id: string, updates: Partial<ItineraryItem>) => {
    setTrip((prev) => ({
      ...prev,
      items: prev.items.map((item) =>
        item.id === id ? ({ ...item, ...updates } as ItineraryItem) : item,
      ),
    }))
  }, [])

  const removeItem = useCallback((id: string) => {
    setTrip((prev) => ({
      ...prev,
      items: prev.items.filter((item) => item.id !== id),
    }))
  }, [])

  const addStop = useCallback((stop: Omit<Stop, 'id'>) => {
    setTrip((prev) => ({
      ...prev,
      stops: [...prev.stops, { ...stop, id: uuid() }],
    }))
  }, [])

  const updateStop = useCallback((id: string, updates: Partial<Stop>) => {
    setTrip((prev) => ({
      ...prev,
      stops: prev.stops.map((s) => (s.id === id ? { ...s, ...updates } : s)),
    }))
  }, [])

  const removeStop = useCallback((id: string) => {
    setTrip((prev) => ({
      ...prev,
      stops: prev.stops.filter((s) => s.id !== id),
    }))
  }, [])

  const addAttachment = useCallback(
    (itemId: string, attachment: Omit<Attachment, 'id'>) => {
      setTrip((prev) => ({
        ...prev,
        items: prev.items.map((item) =>
          item.id === itemId
            ? {
                ...item,
                attachments: [
                  ...item.attachments,
                  { ...attachment, id: uuid() },
                ],
              }
            : item,
        ),
      }))
    },
    [],
  )

  const removeAttachment = useCallback((itemId: string, attachmentId: string) => {
    setTrip((prev) => ({
      ...prev,
      items: prev.items.map((item) =>
        item.id === itemId
          ? {
              ...item,
              attachments: item.attachments.filter((a) => a.id !== attachmentId),
            }
          : item,
      ),
    }))
  }, [])

  const resetToSample = useCallback(() => {
    setTrip(createSampleTrip())
  }, [])

  const resetToEmpty = useCallback(() => {
    setTrip(createEmptyTrip())
  }, [])

  return {
    trip,
    updateTrip,
    updateSettings,
    addItem,
    updateItem,
    removeItem,
    addStop,
    updateStop,
    removeStop,
    addAttachment,
    removeAttachment,
    resetToSample,
    resetToEmpty,
  }
}

export type UseTripReturn = ReturnType<typeof useTrip>
