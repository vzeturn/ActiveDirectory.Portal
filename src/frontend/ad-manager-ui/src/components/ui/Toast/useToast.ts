import { useCallback, useState } from 'react'

export interface ToastMessage {
  id: string
  text: string
  variant: 'success' | 'error'
}

export function useToast() {
  const [toasts, setToasts] = useState<ToastMessage[]>([])

  const dismiss = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id))
  }, [])

  const show = useCallback((text: string, variant: 'success' | 'error') => {
    const id = crypto.randomUUID()
    setToasts((prev) => [...prev, { id, text, variant }])
    setTimeout(() => dismiss(id), 5000)
  }, [dismiss])

  return { toasts, show, dismiss }
}
