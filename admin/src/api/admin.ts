import { api } from './client'
import type {
  DashboardStats, PlayerSummary, PlayerDetail,
  CashProduct, DailyRevenue, AdminAccount,
} from './types'

// ── Auth ──────────────────────────────────────────────────────────────────
export const authApi = {
  loginWithGoogle: (idToken: string) =>
    api.post<{ token: string; email: string; role: string }>(
      '/api/admin/auth/google', { idToken }),
}

// ── Dashboard ─────────────────────────────────────────────────────────────
export const dashboardApi = {
  getStats: () => api.get<DashboardStats>('/api/admin/dashboard'),
}

// ── Users ─────────────────────────────────────────────────────────────────
export const userApi = {
  search: (keyword?: string, offset = 0, limit = 30) => {
    const q = new URLSearchParams()
    if (keyword) q.set('keyword', keyword)
    q.set('offset', String(offset))
    q.set('limit', String(limit))
    return api.get<PlayerSummary[]>(`/api/admin/users?${q}`)
  },
  getDetail: (memberNo: number | string) =>
    api.get<PlayerDetail>(`/api/admin/users/${encodeURIComponent(String(memberNo))}`),
  getPending: (offset = 0, limit = 50) =>
    api.get<{ total: number; offset: number; limit: number; items: import('./types').PendingPlayer[] }>(
      `/api/admin/users/pending?offset=${offset}&limit=${limit}`),
  approve: (memberNo: number) =>
    api.post<{ approved: boolean }>(`/api/admin/users/${memberNo}/approve`, {}),
  suspend: (memberNo: number, reason: string) =>
    api.post<{ suspended: boolean }>(`/api/admin/users/${memberNo}/suspend`, { reason }),
  unsuspend: (memberNo: number) =>
    api.post<{ unsuspended: boolean }>(`/api/admin/users/${memberNo}/unsuspend`, {}),
}

// ── キャッシュ ───────────────────────────────────────────────────────────
export const cashApi = {
  adjust: (memberNo: number, amount: number, memo: string) =>
    api.post<{
      memberNo: number
      balanceBefore: number
      balanceAfter: number
      paidCashBefore: number
      paidCashAfter: number
      freeCashBefore: number
      freeCashAfter: number
    }>(
      '/api/admin/cash/adjust', { memberNo, amount, memo }),

  getProducts: () => api.get<CashProduct[]>('/api/admin/cash/products'),

  updateProduct: (p: CashProduct) =>
    api.put<{ updated: boolean }>(`/api/admin/cash/products/${encodeURIComponent(p.productId)}`, p),

  getRevenue: (days = 30) =>
    api.get<DailyRevenue[]>(`/api/admin/cash/revenue?days=${days}`),
}

// ── Admin Accounts ────────────────────────────────────────────────────────
export const accountApi = {
  list: () => api.get<AdminAccount[]>('/api/admin/accounts'),
  upsert: (email: string, role: string) =>
    api.post<AdminAccount>('/api/admin/accounts', { email, role }),
  disable: (email: string) =>
    api.del<{ disabled: boolean }>(`/api/admin/accounts/${encodeURIComponent(email)}`),
}

// ── Notice ────────────────────────────────────────────────────────────────
export const noticeApi = {
  send: (message: string, color = 0) =>
    api.post<{ sent: boolean }>('/api/admin/notice', { message, color }),
}
