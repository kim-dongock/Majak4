import { useState } from 'react'

const IMG = '/assets/images/game'

export type TournamentFormat = {
  maxPlayers: number
  playMode: number
  label: string
  imageName: string
  imageHeight: number
}

export const TOURNAMENT_FORMATS: TournamentFormat[] = [
  { maxPlayers: 4, playMode: 1, label: '4人/1人勝抜', imageName: 'mj_tournament_match_type_a.png', imageHeight: 396 },
  { maxPlayers: 16, playMode: 1, label: '16人/1人勝抜', imageName: 'mj_tournament_match_type_c.png', imageHeight: 396 },
  { maxPlayers: 64, playMode: 1, label: '64人/1人勝抜', imageName: 'mj_tournament_match_type_f.png', imageHeight: 1040 },
  { maxPlayers: 8, playMode: 2, label: '8人/2人勝抜', imageName: 'mj_tournament_match_type_b.png', imageHeight: 396 },
  { maxPlayers: 16, playMode: 2, label: '16人/2人勝抜', imageName: 'mj_tournament_match_type_d.png', imageHeight: 396 },
  { maxPlayers: 32, playMode: 2, label: '32人/2人勝抜', imageName: 'mj_tournament_match_type_e.png', imageHeight: 560 },
]

function getFirstRoundLabelPositions(format: TournamentFormat): Array<[number, number, number]> {
  if (format.maxPlayers === 4) {
    return [[49, 122, 99], [49, 342, 99], [484, 122, 99], [484, 342, 99]]
  }
  if (format.maxPlayers === 8) {
    return [[4, 102, 99], [4, 162, 99], [4, 302, 99], [4, 362, 99], [529, 102, 99], [529, 162, 99], [529, 302, 99], [529, 362, 99]]
  }

  const matchCount = format.maxPlayers / 4
  const leftMatchCount = matchCount / 2
  return Array.from({ length: matchCount }, (_, matchIndex) => {
    const isRightSide = matchIndex >= leftMatchCount
    const sideMatchIndex = matchIndex % leftMatchCount
    const baseY = 103 + sideMatchIndex * 120
    return Array.from({ length: 4 }, (_, playerIndex) => [isRightSide ? 529 : 4, baseY + playerIndex * 20, 99] as [number, number, number])
  }).flat()
}

export default function TournamentBracketPreviewDlg({
  format,
  onClose,
  selectable = false,
}: {
  format?: TournamentFormat
  onClose: () => void
  selectable?: boolean
}) {
  const [selectedIndex, setSelectedIndex] = useState(() => Math.max(0, format ? TOURNAMENT_FORMATS.indexOf(format) : 0))
  const selectedFormat = selectable ? TOURNAMENT_FORMATS[selectedIndex] : format ?? TOURNAMENT_FORMATS[0]
  const playerPositions = getFirstRoundLabelPositions(selectedFormat)

  return (
    <div className="majak-tournament-bracket-preview-overlay" role="presentation">
      <section className="majak-tournament-bracket-preview" role="dialog" aria-modal="true" aria-labelledby="tournament-bracket-preview-title">
        <header>
          <div>
            <h2 id="tournament-bracket-preview-title">対戦表プレビュー</h2>
            {selectable ? (
              <select className="majak-tournament-bracket-preview__format" value={selectedIndex} onChange={event => setSelectedIndex(Number(event.target.value))} aria-label="大会形式">
                {TOURNAMENT_FORMATS.map((item, index) => <option key={item.label} value={index}>{item.label}</option>)}
              </select>
            ) : <p>{selectedFormat.label}</p>}
          </div>
          <button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button>
        </header>
        <div className="majak-tournament-bracket-preview__image">
          <div className="majak-tournament-bracket-preview__canvas" style={{ aspectRatio: `633 / ${selectedFormat.imageHeight}` }}>
            <img src={`${IMG}/${selectedFormat.imageName}`} alt={`${selectedFormat.label}の対戦表`} draggable={false} />
            {playerPositions.map(([left, top, width], index) => (
              <span
                key={`${selectedFormat.label}-${index}`}
                className="majak-tournament-bracket-preview__player"
                style={{ left: `${left / 6.33}%`, top: `${top / selectedFormat.imageHeight * 100}%`, width: `${width / 6.33}%` }}
              >
                {`雀士${String(index + 1).padStart(2, '0')}`}
              </span>
            ))}
          </div>
        </div>
        <footer><button type="button" onClick={onClose}>閉じる</button></footer>
      </section>
    </div>
  )
}