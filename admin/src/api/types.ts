export interface DashboardStats {
  totalPlayers: number
  activePlayersToday: number
  pendingApproval: number
  cashChargedToday: number
  revenueJpyToday: number
  cashChargedThisMonth: number
  revenueJpyThisMonth: number
}

export interface PlayerSummary {
  memberNo: number
  displayName: string
  sexCode: string
  avatarId: string
  accountStatus: number
  lastLoginAt: string
  gameMoney: number
  gemCount: number
  cashCount: number
  paidCashCount: number
  freeCashCount: number
}

export interface PlayerDetail extends PlayerSummary {
  sexCode: string
  firstLoginAt: string
  commonRating: number
  experience: number
  weeklyPoint: number
  lastPlayedAt: string | null
}

export interface CashProduct {
  productId: string
  displayName: string
  cashAmount: number
  priceJpy: number
  platform: 'web' | 'ios' | 'android' | 'all'
  storeProductId: string | null
  isActive: boolean
  sortOrder: number
}

export interface DailyRevenue {
  revenueDate: string
  platform: string
  orderCount: number
  totalCash: number
  totalJpy: number
}

export interface AdminAccount {
  adminNo: number
  email: string
  role: 'super_admin' | 'operator' | 'viewer'
  isActive: boolean
  createdAt: string
}

export interface PendingPlayer {
  memberNo: number
  displayName: string
  sexCode: string
  avatarId: string
  termsAgreedAt: string
  registeredAt: string
}
