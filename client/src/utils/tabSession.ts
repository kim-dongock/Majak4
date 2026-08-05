const TAB_ID_STORAGE_KEY = 'majak:tab-id'

function createTabId(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function')
    return crypto.randomUUID()

  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`
}

export function getTabSessionId(): string {
  try {
    const existing = window.sessionStorage.getItem(TAB_ID_STORAGE_KEY)
    if (existing) return existing

    const tabId = createTabId()
    window.sessionStorage.setItem(TAB_ID_STORAGE_KEY, tabId)
    return tabId
  } catch {
    return createTabId()
  }
}