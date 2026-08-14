import { useState } from 'react'
import type { GameAnnouncement } from '../../api/announcements'
import AccuseDlg from './dialogs/AccuseDlg'
import AskEndDlg from './dialogs/AskEndDlg'
import CfgDlg, { DEFAULT_CONFIG } from './dialogs/CfgDlg'
import CollectionDlg from './dialogs/CollectionDlg'
import CustomReceiptDlg from './dialogs/CustomReceiptDlg'
import EndingPopupWnd from './dialogs/EndingPopupWnd'
import ExchangeItemReceiptDlg from './dialogs/ExchangeItemReceiptDlg'
import GetCoinDlg from './dialogs/GetCoinDlg'
import GetReqGameDialog from './dialogs/GetReqGameDialog'
import HanCoinReceiptDlg from './dialogs/HanCoinReceiptDlg'
import ItemPopupDlg, { POPUP_REASON } from './dialogs/ItemPopupDlg'
import LeadDlg from './dialogs/LeadDlg'
import LevelupDlg from './dialogs/LevelupDlg'
import LotResultDlg from './dialogs/LotResultDlg'
import LotSlotDlg from './dialogs/LotSlotDlg'
import MissionDlg from './dialogs/MissionDlg'
import MissionRewardGuideDlg from './dialogs/MissionRewardGuideDlg'
import OptDlg, { DEFAULT_OPTION } from './dialogs/OptDlg'
import PlayerInfoWnd from './dialogs/PlayerInfoWnd'
import RankingDlg, { type RankingData } from './dialogs/RankingDlg'
import RoomCreateDlg from './dialogs/RoomCreateDlg'
import ResponsiveItemShopDlg from './dialogs/ResponsiveItemShopDlg'
import StartPopupWnd from './dialogs/StartPopupWnd'
import TournamentRegistDlg from './dialogs/TournamentRegistDlg'
import WelcomeDlg from './dialogs/WelcomeDlg'

type PreviewId =
  | 'startNotice' | 'welcome' | 'roomCreate' | 'roomOptions' | 'settings'
  | 'ranking' | 'playerInfo' | 'inviteRequest' | 'accuse' | 'mission'
  | 'missionGuide' | 'tournamentRegist'
  | 'shop' | 'collection' | 'hanCoinReceipt' | 'exchangeReceipt' | 'customReceipt' | 'lotSlot' | 'lotResult'
  | 'levelup' | 'coin' | 'lead' | 'item' | 'ending' | 'askEnd'

type PreviewEntry = {
  id: PreviewId
  title: string
  group: string
  status: 'Complete'
  summary: string
}

const PREVIEWS: PreviewEntry[] = [
  { id: 'startNotice', title: 'StartPopupWnd', group: 'Startup and account notices', status: 'Complete', summary: 'Admin-selected startup announcement fixture.' },
  { id: 'welcome', title: 'WelcomeDlg', group: 'Startup and account notices', status: 'Complete', summary: 'Text-based welcome guidance and dismissal action.' },
  { id: 'roomCreate', title: 'RoomCreateDlg', group: 'Lobby and rooms', status: 'Complete', summary: 'Public room setup with spectator access fixture.' },
  { id: 'roomOptions', title: 'OptDlg', group: 'Lobby and rooms', status: 'Complete', summary: 'Room rule selection fixture.' },
  { id: 'settings', title: 'CfgDlg', group: 'Lobby and rooms', status: 'Complete', summary: 'Client configuration fixture.' },
  { id: 'ranking', title: 'RankingDlg', group: 'Lobby and players', status: 'Complete', summary: 'Rating ranking with self-position fixture.' },
  { id: 'playerInfo', title: 'PlayerInfoWnd', group: 'Lobby and players', status: 'Complete', summary: 'Player profile and match-record fallback fixture.' },
  { id: 'inviteRequest', title: 'GetReqGameDialog', group: 'Lobby and players', status: 'Complete', summary: 'Incoming game invitation with a visible countdown fixture.' },
  { id: 'accuse', title: 'AccuseDlg', group: 'Lobby and players', status: 'Complete', summary: 'Chat report target and reason selection fixture.' },
  { id: 'mission', title: 'MissionDlg', group: 'Missions and events', status: 'Complete', summary: 'Mission dialog shell; live mission data is loaded in the game.' },
  { id: 'levelup', title: 'LevelupDlg', group: 'Rewards', status: 'Complete', summary: 'Level 7 and 50,000 GP insurance fixture.' },
  { id: 'coin', title: 'GetCoinDlg', group: 'Rewards', status: 'Complete', summary: 'Free GP replenishment notice fixture.' },
  { id: 'missionGuide', title: 'MissionRewardGuideDlg', group: 'Rewards', status: 'Complete', summary: 'Explains how daily mission points become weekly GP rewards.' },
  { id: 'lead', title: 'LeadDlg', group: 'Rewards', status: 'Complete', summary: 'Rating challenge eligibility fixture.' },
  { id: 'item', title: 'ItemPopupDlg', group: 'Shop', status: 'Complete', summary: 'Three purchasable item fixture with recommendation state.' },
  { id: 'shop', title: 'ResponsiveItemShopDlg', group: 'Shop', status: 'Complete', summary: 'Shop shell with GP, MP, and Dragon Orb balance fixture.' },
  { id: 'collection', title: 'CollectionDlg', group: 'Shop', status: 'Complete', summary: 'Collection dialog loading state fixture.' },
  { id: 'hanCoinReceipt', title: 'HanCoinReceiptDlg', group: 'Shop', status: 'Complete', summary: 'MP purchase completion receipt fixture.' },
  { id: 'exchangeReceipt', title: 'ExchangeItemReceiptDlg', group: 'Shop', status: 'Complete', summary: 'Dragon Orb exchange completion receipt fixture.' },
  { id: 'customReceipt', title: 'CustomReceiptDlg', group: 'Shop', status: 'Complete', summary: 'Custom item purchase completion receipt fixture.' },
  { id: 'lotSlot', title: 'LotSlotDlg', group: 'Shop', status: 'Complete', summary: 'Lottery slot start-state fixture.' },
  { id: 'lotResult', title: 'LotResultDlg', group: 'Shop', status: 'Complete', summary: 'Lottery result summary fixture.' },
  { id: 'tournamentRegist', title: 'TournamentRegistDlg', group: 'Tournaments', status: 'Complete', summary: 'Tournament registration form fixture.' },
  { id: 'ending', title: 'EndingPopupWnd', group: 'Startup and account notices', status: 'Complete', summary: 'Logout confirmation fixture.' },
  { id: 'askEnd', title: 'AskEndDlg', group: 'In-game confirmation', status: 'Complete', summary: '10-second continuation confirmation fixture.' },
]

const PREVIEW_ANNOUNCEMENT: GameAnnouncement = {
  id: 1,
  title: 'メンテナンスのお知らせ',
  body: '8月20日 10:00から12:00までメンテナンスを実施します。\nご理解とご協力をお願いいたします。',
  publishedAt: '2026-08-14T00:00:00Z',
  isStartup: true,
}

const PREVIEW_RANKING: RankingData = {
  rankDate: '202608',
  rankId: '段位戦',
  gradeRankSelf: { pix: 'preview-user', rank: 12, rating: 1840, grade: 7, szIndex: '上位 5%' },
  gradeRankList: [
    { pix: 'rank-1', rank: 1, rating: 2430, grade: 12 },
    { pix: 'rank-2', rank: 2, rating: 2280, grade: 11 },
    { pix: 'preview-user', rank: 12, rating: 1840, grade: 7, isSelf: 1 },
  ],
}

function renderPreview(id: PreviewId, onClose: () => void) {
  switch (id) {
    case 'startNotice':
      return <StartPopupWnd announcement={PREVIEW_ANNOUNCEMENT} onClose={onClose} />
    case 'welcome':
      return <WelcomeDlg onClose={onClose} />
    case 'roomCreate':
      return <RoomCreateDlg initialTitle="気軽にどうぞ～" onOK={onClose} onCancel={onClose} />
    case 'roomOptions':
      return <OptDlg initial={DEFAULT_OPTION} onOK={onClose} onCancel={onClose} />
    case 'settings':
      return <CfgDlg initial={DEFAULT_CONFIG} onOK={onClose} onCancel={onClose} />
    case 'ranking':
      return <RankingDlg data={PREVIEW_RANKING} memberNameByPix={new Map([['rank-1', '雀王'], ['rank-2', '東風の神'], ['preview-user', 'プレビュー雀士']])} onClose={onClose} />
    case 'playerInfo':
      return <PlayerInfoWnd player={{ pix: 'preview-user', name: 'プレビュー雀士', sex: 'M', rating: 1840, slevel: '七段', location: '東京', winCount: 38, loseCount: 21, drawCount: 4 }} onClose={onClose} />
    case 'inviteRequest':
      return <GetReqGameDialog inviterId="preview-inviter" inviterName="対戦相手" roomId={12345} roomPwd="" roomName="気軽にどうぞ～" inviterSex="F" inviterRating={1720} inviterLevel="六段" onClose={onClose} />
    case 'accuse':
      return <AccuseDlg myPix="preview-user" myMemberName="プレビュー雀士" speakers={['player-a', 'player-b']} speakerNameById={new Map([['player-a', '困った雀士'], ['player-b', '対局相手']])} chatContent="プレビュー用のチャット履歴です。" onClose={onClose} />
    case 'mission':
      return <MissionDlg onClose={onClose} onMoneyUpdate={() => {}} onGemUpdate={() => {}} />
    case 'levelup':
      return <LevelupDlg level={7} lentMoney={50000} onClose={onClose} />
    case 'coin':
      return <GetCoinDlg storageKey="popup-preview-coin" onClose={onClose} />
    case 'missionGuide':
      return <MissionRewardGuideDlg onClose={onClose} />
    case 'lead':
      return <LeadDlg storageKey="popup-preview-lead" onClose={onClose} />
    case 'item':
      return <ItemPopupDlg reason={POPUP_REASON.USEDUP} gamMoney={1200} pix="preview-user" memberName="プレビュー雀士" hanCoin={500} onClose={onClose} />
    case 'shop':
      return <ResponsiveItemShopDlg cashCount={500} gemCount={24} gamMoney={1200} onClose={onClose} />
    case 'collection':
      return <CollectionDlg onClose={onClose} onEquipChange={() => {}} />
    case 'hanCoinReceipt':
      return <HanCoinReceiptDlg pix="preview-user" memberName="プレビュー雀士" itemName="龍珠2倍" sellCode="bonus" price={300} count={1} coinBefore={500} coinAfter={200} gameMoney={1000} onClose={onClose} />
    case 'exchangeReceipt':
      return <ExchangeItemReceiptDlg pix="preview-user" memberName="プレビュー雀士" itemName="特別称号" itemKind="麻雀称号" itemGuid1="限定称号を獲得しました。" itemGuid2="コレクションから装着できます。" costGem={10} costMoney={5000} userGem={14} userMoney={7000} limitDays={-1} quantity={0} onClose={onClose} />
    case 'customReceipt':
      return <CustomReceiptDlg pix="preview-user" memberName="プレビュー雀士" itemId={11} itemName="和風背景" price={200} gameMoney={500} coinBefore={500} coinAfter={300} onClose={onClose} />
    case 'lotSlot':
      return <LotSlotDlg itemName="龍珠くじ" lotteryCount={3} totalAmount={3000} lotValues={[1000, 1000, 1000]} nextLotteryCount={5} onResult={() => {}} onClose={onClose} />
    case 'lotResult':
      return <LotResultDlg itemName="龍珠くじ" lotteryCount={3} entries={[]} totalAmount={3000} nextLotteryCount={5} onBuyAgain={() => {}} onClose={onClose} />
    case 'tournamentRegist':
      return <TournamentRegistDlg onOK={onClose} onCancel={onClose} />
    case 'ending':
      return <EndingPopupWnd onOK={onClose} onCancel={onClose} />
    case 'askEnd':
      return <AskEndDlg onYes={onClose} onNo={onClose} />
  }
}

export default function PopupPreviewScreen() {
  const [selectedId, setSelectedId] = useState<PreviewId>('welcome')
  const [isOpen, setIsOpen] = useState(true)
  const selected = PREVIEWS.find(entry => entry.id === selectedId) ?? PREVIEWS[0]

  const choosePreview = (id: PreviewId) => {
    setSelectedId(id)
    setIsOpen(true)
  }

  return (
    <main className="majak-popup-preview majak-screen-surface">
      <aside className="majak-popup-preview__sidebar">
        <div>
          <p className="majak-popup-preview__eyebrow">Development only</p>
          <h1>Popup Preview</h1>
          <p className="majak-popup-preview__description">Select a popup to open it with deterministic fixture data.</p>
        </div>
        <nav className="majak-popup-preview__list" aria-label="Popup preview list">
          {PREVIEWS.map(entry => (
            <button
              key={entry.id}
              type="button"
              className={entry.id === selectedId ? 'is-selected' : ''}
              onClick={() => choosePreview(entry.id)}
            >
              <span>{entry.title}</span>
              <small>{entry.group}</small>
              <b>{entry.status}</b>
            </button>
          ))}
        </nav>
      </aside>
      <section className="majak-popup-preview__content" aria-live="polite">
        <p className="majak-popup-preview__eyebrow">{selected.group}</p>
        <h2>{selected.title}</h2>
        <p>{selected.summary}</p>
        <dl>
          <div><dt>Source status</dt><dd>Complete</dd></div>
          <div><dt>Browser status</dt><dd>Ready for manual check</dd></div>
        </dl>
        {!isOpen && <button type="button" className="majak-popup-preview__open" onClick={() => setIsOpen(true)}>Open {selected.title}</button>}
      </section>
      {isOpen && renderPreview(selectedId, () => setIsOpen(false))}
    </main>
  )
}
