/**
 * Rating ranking dialog — ShowRankingDialog 相当の入口。
 * レガシー: CMajakChannelWnd::OnBtnRankingClicked → ShowRankingDialog(...)
 * サーバー: mjkc25e RatingRankInfo → gradeRankList / gradeRankSelf
 */
export type RankingItem = {
  pix: string
  rank: number
  rating: number
  grade: number
  lastDate?: string
  isSelf?: number
}

export type RankingData = {
  rankDate?: number | string
  rankId?: number | string
  gradeRankList: RankingItem[]
  gradeRankSelf?: RankingItem & { szIndex?: string }
}

type Props = {
  data: RankingData
  onClose: () => void
  memberNameByPix?: Map<string, string>
}

export default function RankingDlg({ data, onClose, memberNameByPix = new Map<string, string>() }: Props) {
  const displayName = (pix: string) => memberNameByPix.get(pix) || pix
  const rankDate = String(data.rankDate ?? '')
  const rankDateLabel = /^\d{6}$/.test(rankDate)
    ? `${rankDate.slice(0, 4)}年${rankDate.slice(4)}月`
    : rankDate || '-'

  return (
    <div className="majak-popup-overlay majak-ranking-dialog-overlay" onContextMenu={event => event.preventDefault()}>
      <section className="majak-popup-panel majak-ranking-dialog" role="dialog" aria-modal="true" aria-labelledby="majak-ranking-dialog-title">
        <header className="majak-popup-titlebar majak-ranking-dialog__header">
          <h2 id="majak-ranking-dialog-title">ランキング</h2>
          <button type="button" className="majak-popup-titlebar__close majak-ranking-dialog__close" onClick={onClose} aria-label="閉じる">×</button>
        </header>

        <div className="majak-popup-body majak-ranking-dialog__body">
          <p className="majak-ranking-dialog__meta">対象年月 {rankDateLabel}<span aria-hidden="true"> / </span>種別 {data.rankId ?? '-'}</p>
          {data.gradeRankSelf && (
            <section className="majak-ranking-dialog__self" aria-label="自分の順位">
              <span>自分の順位</span>
              <strong>{data.gradeRankSelf.rank || '-'}<small>位</small></strong>
              <div>
                <b>{displayName(data.gradeRankSelf.pix)}</b>
                <span>R {data.gradeRankSelf.rating} / 段位 {data.gradeRankSelf.grade}</span>
              </div>
              {data.gradeRankSelf.szIndex && <em>{data.gradeRankSelf.szIndex}</em>}
            </section>
          )}

          <section className="majak-ranking-dialog__list" aria-label="ランキング一覧">
            <div className="majak-ranking-dialog__columns" aria-hidden="true">
              <span>順位</span><span>ニックネーム</span><span>レーティング</span><span>段位</span>
            </div>
            <div className="majak-ranking-dialog__rows">
              {data.gradeRankList.length === 0 ? (
                <p className="majak-ranking-dialog__empty">ランキング情報がありません。</p>
              ) : data.gradeRankList.map(item => (
                <div className={`majak-ranking-dialog__row${item.isSelf ? ' is-self' : ''}`} key={`${item.rank}-${item.pix}`}>
                  <strong className={`majak-ranking-dialog__place rank-${Math.min(Math.max(item.rank, 1), 4)}`}>{item.rank}<small>位</small></strong>
                  <b className="majak-ranking-dialog__name">{displayName(item.pix)}</b>
                  <span className="majak-ranking-dialog__rating">R {item.rating}</span>
                  <span className="majak-ranking-dialog__grade">段位 {item.grade}</span>
                </div>
              ))}
            </div>
          </section>
        </div>
        <footer className="majak-popup-actions majak-ranking-dialog__footer"><button type="button" onClick={onClose}>閉じる</button></footer>
      </section>
    </div>
  )
}