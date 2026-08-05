import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface AuthState {
  token: string | null
  email: string | null
  role: string | null
  setAuth: (token: string, email: string, role: string) => void
  clearAuth: () => void
  isSuperAdmin: () => boolean
  canManageGem: () => boolean
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      token: null,
      email: null,
      role: null,
      setAuth: (token, email, role) => set({ token, email, role }),
      clearAuth: () => set({ token: null, email: null, role: null }),
      isSuperAdmin: () => get().role === 'super_admin',
      canManageGem: () => get().role === 'super_admin' || get().role === 'operator',
    }),
    { name: 'majak2-admin-auth' },
  ),
)
