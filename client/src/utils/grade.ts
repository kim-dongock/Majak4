const GRADE_NAMES = [
  '10級', '9級', '8級', '7級', '6級', '5級', '4級', '3級', '2級', '1級',
  '初段', '二段', '三段', '四段', '五段', '六段', '七段', '八段', '九段',
]

export function gradeLevelName(gradeLevel: number | undefined) {
  return gradeLevel != null ? GRADE_NAMES[gradeLevel] ?? '-' : '-'
}