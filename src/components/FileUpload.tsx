import { useRef } from 'react'
import type { Attachment } from '../types'

interface FileUploadProps {
  onUpload: (attachment: Omit<Attachment, 'id'>) => void
  label?: string
}

export function FileUpload({ onUpload, label = 'Attach booking file' }: FileUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null)

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return

    const reader = new FileReader()
    reader.onload = () => {
      onUpload({
        name: file.name,
        mimeType: file.type,
        dataUrl: reader.result as string,
      })
    }
    reader.readAsDataURL(file)
    e.target.value = ''
  }

  return (
    <div>
      <input
        ref={inputRef}
        type="file"
        accept=".pdf,.png,.jpg,.jpeg,.webp,.gif"
        className="hidden"
        onChange={handleChange}
      />
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        className="text-sm text-ocean-600 hover:text-ocean-800 font-medium"
      >
        + {label}
      </button>
    </div>
  )
}

interface AttachmentListProps {
  attachments: Attachment[]
  onRemove?: (id: string) => void
}

export function AttachmentList({ attachments, onRemove }: AttachmentListProps) {
  if (attachments.length === 0) return null

  return (
    <div className="flex flex-wrap gap-2 mt-2">
      {attachments.map((att) => (
        <div
          key={att.id}
          className="flex items-center gap-2 bg-sand-100 rounded-lg px-3 py-1.5 text-sm"
        >
          {att.mimeType.startsWith('image/') ? (
            <a href={att.dataUrl} target="_blank" rel="noreferrer">
              <img
                src={att.dataUrl}
                alt={att.name}
                className="h-8 w-8 object-cover rounded"
              />
            </a>
          ) : (
            <span>📄</span>
          )}
          <a
            href={att.dataUrl}
            download={att.name}
            target="_blank"
            rel="noreferrer"
            className="text-ocean-700 hover:underline truncate max-w-[140px]"
          >
            {att.name}
          </a>
          {onRemove && (
            <button
              type="button"
              onClick={() => onRemove(att.id)}
              className="text-slate-400 hover:text-red-500 ml-1"
              aria-label="Remove attachment"
            >
              ×
            </button>
          )}
        </div>
      ))}
    </div>
  )
}
