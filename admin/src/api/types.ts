export interface DashboardStats {
  totalPlayers: number
  activePlayersToday: number
  pendingApproval: number
  gemChargedToday: number
  revenueJpyToday: number
  gemChargedThisMonth: number
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
}

export interface PlayerDetail extends PlayerSummary {
  sexCode: string
  firstLoginAt: string
  commonRating: number
  experience: number
  weeklyPoint: number
  lastPlayedAt: string | null
}

export interface GemProduct {
  productId: string
  displayName: string
  gemAmount: number
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
  totalGem: number
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
