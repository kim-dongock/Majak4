import { useEffect, useMemo, useState } from 'react'

const IMG = '/assets/images/game'

type EffectPreview = {
  id: string
  title: string
  trigger: string
  source: string
  intervalMs: number
  frames: string[]
}

function numberedFrames(prefix: string, count: number, start = 1) {
  return Array.from({ length: count }, (_, index) => `${IMG}/${prefix}${String(start + index).padStart(2, '0')}.png`)
}

const EFFECTS: EffectPreview[] = [
  {
    id: 'ron',
    title: 'ロン和了',
    trigger: 'MJPID_ENDKYO / pinType=0',
    source: 'GameScene.playKyoResultHoraEffect',
    intervalMs: 30,
    frames: numberedFrames('eff_roneff_', 11),
  },
  {
    id: 'skill-fire',
    title: '火属性 Lv.1 技',
    trigger: 'MJPID_INIKYO / プレイヤー属性=火',
    source: 'GameScene.playLegacyLevel1Skills',
    intervalMs: 70,
    frames: numberedFrames('mj_ef_L1fire_', 13),
  },
  {
    id: 'skill-water',
    title: '水属性 Lv.1 技',
    trigger: 'MJPID_INIKYO / プレイヤー属性=水',
    source: 'GameScene.playLegacyLevel1Skills',
    intervalMs: 70,
    frames: numberedFrames('mj_ef_L1water_', 11),
  },
  {
    id: 'skill-wind',
    title: '風属性 Lv.1 技',
    trigger: 'MJPID_INIKYO / プレイヤー属性=風',
    source: 'GameScene.playLegacyLevel1Skills',
    intervalMs: 70,
    frames: numberedFrames('mj_ef_L1wind_', 12),
  },
  {
    id: 'skill-earth',
    title: '地属性 Lv.1 技',
    trigger: 'MJPID_INIKYO / プレイヤー属性=地',
    source: 'GameScene.playLegacyLevel1Skills',
    intervalMs: 70,
    frames: numberedFrames('mj_ef_L1earth_', 12),
  },
  {
    id: 'yakuman',
    title: '役満演出',
    trigger: 'MJPID_ENDKYO / 役満成立',
    source: 'GameScene.playKyoResultHoraEffect',
    intervalMs: 150,
    frames: numberedFrames('mj_ef_yakuman', 13, 0),
  },
]

export default function GameSpritePreviewDlg({ onClose }: { onClose: () => void }) {
  const [selectedId, setSelectedId] = useState(EFFECTS[0].id)
  const [frameIndex, setFrameIndex] = useState(0)
  const [playing, setPlaying] = useState(true)
  const selected = useMemo(() => EFFECTS.find(effect => effect.id === selectedId) ?? EFFECTS[0], [selectedId])

  useEffect(() => {
    setFrameIndex(0)
    setPlaying(true)
  }, [selectedId])

  useEffect(() => {
    if (!playing) return
    const timer = window.setInterval(() => {
      setFrameIndex(index => (index + 1) % selected.frames.length)
    }, selected.intervalMs)
    return () => window.clearInterval(timer)
  }, [playing, selected])

  return (
    <div className="majak-game-effect-preview" role="dialog" aria-modal="true" aria-label="ゲームスプライトプレビュー">
      <header>
        <div>
          <small>Phaser gameplay resources</small>
          <h2>ゲームスプライト</h2>
        </div>
        <button type="button" onClick={onClose} aria-label="閉じる">×</button>
      </header>
      <div className="majak-game-effect-preview__body">
        <nav aria-label="ゲームエフェクト一覧">
          {EFFECTS.map(effect => (
            <button key={effect.id} type="button" className={effect.id === selected.id ? 'is-selected' : ''} onClick={() => setSelectedId(effect.id)}>
              {effect.title}
            </button>
          ))}
        </nav>
        <section>
          <div className="majak-game-effect-preview__stage">
            <img src={selected.frames[frameIndex]} alt="" draggable={false} />
          </div>
          <div className="majak-game-effect-preview__controls">
            <button type="button" onClick={() => setPlaying(value => !value)}>{playing ? '停止' : '再生'}</button>
            <button type="button" onClick={() => { setPlaying(false); setFrameIndex(index => (index + selected.frames.length - 1) % selected.frames.length) }}>前</button>
            <button type="button" onClick={() => { setPlaying(false); setFrameIndex(index => (index + 1) % selected.frames.length) }}>次</button>
            <strong>{frameIndex + 1} / {selected.frames.length}</strong>
          </div>
          <dl>
            <div><dt>発生条件</dt><dd>{selected.trigger}</dd></div>
            <div><dt>実行箇所</dt><dd>{selected.source}</dd></div>
            <div><dt>フレーム間隔</dt><dd>{selected.intervalMs}ms</dd></div>
          </dl>
        </section>
      </div>
    </div>
  )
}
