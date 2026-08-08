export type MemberFilterSex = 'all' | 'male' | 'female'
export type MemberFilterAge = 'all' | '10s' | '20s' | '30s' | '40plus'

export interface MemberFilterValue {
  sex: MemberFilterSex
  age: MemberFilterAge
  level: number | null
}

export interface FilterableMember {
  sex: 'male' | 'female'
  age: number
  nlevel: number
}

export const DEFAULT_MEMBER_FILTER: MemberFilterValue = {
  sex: 'all',
  age: 'all',
  level: null,
}

export function isMemberFilterActive(filter: MemberFilterValue): boolean {
  return filter.sex !== 'all' || filter.age !== 'all' || filter.level !== null
}

export function matchesMemberFilter(member: FilterableMember, filter: MemberFilterValue): boolean {
  if (filter.sex !== 'all' && member.sex !== filter.sex) return false
  if (filter.level !== null && member.nlevel !== filter.level) return false
  if (filter.age === 'all') return true
  if (member.age <= 0) return false

  if (filter.age === '10s') return member.age >= 10 && member.age < 20
  if (filter.age === '20s') return member.age >= 20 && member.age < 30
  if (filter.age === '30s') return member.age >= 30 && member.age < 40
  return member.age >= 40
}