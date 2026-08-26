import { useState } from 'react'
import type { GameAnnouncement } from '../../api/announcements'
import AccuseDlg from './dialogs/AccuseDlg'
import AskEndDlg from './dialogs/AskEndDlg'
import BuyCustomItemDlg from './dialogs/BuyCustomItemDlg'
import BuyExchangeItemDlg from './dialogs/BuyExchangeItemDlg'
import BuyHanCoinItemDlg from './dialogs/BuyHanCoinItemDlg'
import CfgDlg, { DEFAULT_CONFIG } from './dialogs/CfgDlg'
import CircleOptDlg from './dialogs/CircleOptDlg'
import CollectionDlg from './dialogs/CollectionDlg'
import ConfirmItemDlg from './dialogs/ConfirmItemDlg'
import CustomDlg from './dialogs/CustomDlg'
import CustomReceiptDlg from './dialogs/CustomReceiptDlg'
import DebugLoginDlg from './dialogs/DebugLoginDlg'
import EndingPopupWnd from './dialogs/EndingPopupWnd'
import ExchangeItemReceiptDlg from './dialogs/ExchangeItemReceiptDlg'
import { Event200912PointDlg } from './dialogs/EventDialogs'
import GetCoinDlg from './dialogs/GetCoinDlg'
import GetReqGameDialog from './dialogs/GetReqGameDialog'
import HanCoinReceiptDlg from './dialogs/HanCoinReceiptDlg'
import ItemPopupDlg, { POPUP_REASON } from './dialogs/ItemPopupDlg'
import ItemShopDlg from './dialogs/ItemShopDlg'
import LeadDlg from './dialogs/LeadDlg'
import LevelupDlg from './dialogs/LevelupDlg'
import LotResultDlg from './dialogs/LotResultDlg'
import LotSlotDlg from './dialogs/LotSlotDlg'
import MissionDlg from './dialogs/MissionDlg'
import MissionRewardGuideDlg from './dialogs/MissionRewardGuideDlg'
import OptDlg, { DEFAULT_OPTION } from './dialogs/OptDlg'
import PlayerInfoWnd from './dialogs/PlayerInfoWnd'
import ProfileEditDlg from './dialogs/ProfileEditDlg'
import PaifuSaveDlg from './dialogs/PaifuSaveDlg'
import RankingDlg, { type RankingData } from './dialogs/RankingDlg'
import RoomCreateDlg from './dialogs/RoomCreateDlg'
import ResponsiveItemShopDlg from './dialogs/ResponsiveItemShopDlg'
import RegistrationDlg from './dialogs/RegistrationDlg'
import SerialCodeDlg from './dialogs/SerialCodeDlg'
import StartPopupWnd from './dialogs/StartPopupWnd'
import TournamentBracketPreviewDlg from './dialogs/TournamentBracketPreviewDlg'
import TournamentRegistDlg from './dialogs/TournamentRegistDlg'
import WelcomeDlg from './dialogs/WelcomeDlg'
import GameSpritePreviewDlg from './dialogs/GameSpritePreviewDlg'
import KyoRes from '../ingame/KyoRes'
import HanRes from '../ingame/HanRes'
import { FORCED_KYO_RESULT } from '../ingame/forcedKyoResult'
import { FORCED_HAN_RESULT } from '../ingame/forcedHanResult'

type PreviewId =
  | 'startNotice' | 'welcome' | 'roomCreate' | 'roomOptions' | 'settings'
  | 'ranking' | 'playerInfo' | 'inviteRequest' | 'accuse' | 'mission'
  | 'missionGuide' | 'tournamentRegist' | 'tournamentBrackets'
  | 'shop' | 'collection' | 'hanCoinReceipt' | 'exchangeReceipt' | 'customReceipt' | 'lotSlot' | 'lotResult'
  | 'levelup' | 'coin' | 'lead' | 'item' | 'ending' | 'askEnd'
  | 'buyHanCoinItem' | 'buyExchangeItem' | 'buyCustomItem' | 'circleOptions' | 'confirmItem'
  | 'customInventory' | 'debugLogin' | 'eventDialogs' | 'legacyItemShop' | 'registration'
  | 'profileEdit'
  | 'shopTransaction' | 'paifuSave' | 'serialCode'
  | 'gameSprites' | 'kyoResult' | 'hanResult'

type PreviewEntry = {
  id: PreviewId
  title: string
  group: string
  status: 'Complete' | 'Cataloged'
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
  { id: 'tournamentBrackets', title: 'Tournament Brackets', group: 'Tournaments', status: 'Complete', summary: 'Format-selectable 4, 8, 16, 32, and 64 player bracket preview.' },
  { id: 'ending', title: 'EndingPopupWnd', group: 'Startup and account notices', status: 'Complete', summary: 'Logout confirmation fixture.' },
  { id: 'askEnd', title: 'AskEndDlg', group: 'In-game confirmation', status: 'Complete', summary: '10-second continuation confirmation fixture.' },
  { id: 'buyHanCoinItem', title: 'BuyHanCoinItemDlg', group: 'Shop', status: 'Complete', summary: 'MP item purchase confirmation dialog.' },
  { id: 'buyExchangeItem', title: 'BuyExchangeItemDlg', group: 'Shop', status: 'Complete', summary: 'Dragon Orb exchange confirmation dialog.' },
  { id: 'buyCustomItem', title: 'BuyCustomItemDlg', group: 'Shop', status: 'Complete', summary: 'Custom item purchase confirmation dialog.' },
  { id: 'circleOptions', title: 'CircleOptDlg', group: 'Lobby and rooms', status: 'Complete', summary: 'Circle room option configuration dialog.' },
  { id: 'confirmItem', title: 'ConfirmItemDlg', group: 'Shop', status: 'Complete', summary: 'Owned item selection and confirmation dialog.' },
  { id: 'customInventory', title: 'CustomDlg', group: 'Shop', status: 'Complete', summary: 'Custom inventory and equipment dialog.' },
  { id: 'debugLogin', title: 'DebugLoginDlg', group: 'Development', status: 'Complete', summary: 'Development-only account login dialog.' },
  { id: 'eventDialogs', title: 'EventDialogs', group: 'Missions and events', status: 'Complete', summary: 'Event introduction, point, and close dialog set.' },
  { id: 'legacyItemShop', title: 'ItemShopDlg', group: 'Shop', status: 'Complete', summary: 'Legacy fixed-layout item shop dialog.' },
  { id: 'registration', title: 'RegistrationDlg', group: 'Startup and account notices', status: 'Complete', summary: 'New member registration dialog.' },
  { id: 'profileEdit', title: 'ProfileEditDlg', group: 'Lobby and players', status: 'Complete', summary: 'Birth year and avatar profile editor.' },
  { id: 'shopTransaction', title: 'ResponsiveShopTransactionDlg', group: 'Shop', status: 'Complete', summary: 'Responsive item purchase transaction dialog.' },
  { id: 'paifuSave', title: 'PaifuSaveDlg', group: 'Lobby and rooms', status: 'Complete', summary: 'Paifu save dialog.' },
  { id: 'serialCode', title: 'SerialCodeDlg', group: 'Startup and account notices', status: 'Complete', summary: 'Serial code entry dialog.' },
  { id: 'gameSprites', title: 'GameSpriteEffects', group: 'In-game effects', status: 'Complete', summary: 'Actual Phaser PNG sequences with their gameplay trigger and execution source.' },
  { id: 'kyoResult', title: 'KyoRes', group: 'In-game results', status: 'Complete', summary: 'The actual per-round result overlay with deterministic result data.' },
  { id: 'hanResult', title: 'HanRes', group: 'In-game results', status: 'Complete', summary: 'The actual final match result overlay with deterministic player totals.' },
]

const PREVIEW_ANNOUNCEMENT: GameAnnouncement = {
  announcementId: 1,
  title: 'メンテナンスのお知らせ',
  body: '8月20日 10:00から12:00までメンテナンスを実施します。\nご理解とご協力をお願いいたします。',
  isPublished: true,
  publishedAt: '2026-08-14T00:00:00Z',
  isStartup: true,
  createdAt: '2026-08-14T00:00:00Z',
  updatedAt: '2026-08-14T00:00:00Z',
}

const PREVIEW_RANKING: RankingData = {
  rankDate: '202608',
  rankId: '段位戦',
  gradeRankSelf: { pix: 'preview-user', nickname: 'プレビュー雀士', rank: 12, rating: 1840, grade: 7, szIndex: '上位 5%' },
  gradeRankList: [
    { pix: 'rank-1', nickname: '雀王', rank: 1, rating: 2430, grade: 12 },
    { pix: 'rank-2', nickname: '東風の神', rank: 2, rating: 2280, grade: 11 },
    { pix: 'preview-user', nickname: 'プレビュー雀士', rank: 12, rating: 1840, grade: 7, isSelf: 1 },
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
      return <RankingDlg data={PREVIEW_RANKING} onClose={onClose} />
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
    case 'tournamentBrackets':
      return <TournamentBracketPreviewDlg selectable onClose={onClose} />
    case 'ending':
      return <EndingPopupWnd onOK={onClose} onCancel={onClose} />
    case 'askEnd':
      return <AskEndDlg onYes={onClose} onNo={onClose} />
    case 'buyHanCoinItem':
      return <BuyHanCoinItemDlg item={{ itemCode: 'preview-ticket', sellCode: 'preview-ticket', itemName: '龍珠2倍', price: 300, gameMoney: 1000, description: ['対局終了時に獲得できる', '龍珠が2倍になります。'], imageUrl: '/assets/images/game/items/mj_item_01.png' }} pix="preview-user" memberName="プレビュー雀士" hanCoin={500} onClose={onClose} />
    case 'buyExchangeItem':
      return <BuyExchangeItemDlg item={{ sellCode: 'preview-title', itemName: '特別称号', itemKind: '麻雀称号', itemGuid1: '限定称号を獲得できます。', itemGuid2: 'コレクションから装着できます。', costGem: 10, costMoney: 5000, limitDays: -1, quantity: 0 }} pix="preview-user" memberName="プレビュー雀士" userGem={24} userMoney={12000} onClose={onClose} />
    case 'buyCustomItem':
      return <BuyCustomItemDlg item={{ itemId: 11, itemName: '和風背景', itemType: '背景', itemDesc: '対局ロビーの背景を変更できます。', price: 200, shopNo: 1, gameMoney: 0 }} pix="preview-user" memberName="プレビュー雀士" hanCoin={500} onClose={onClose} />
    case 'circleOptions':
      return <CircleOptDlg circles={[{ circleId: 'circle-1', circleName: '雀友会' }, { circleId: 'circle-2', circleName: '東風クラブ' }, { circleId: 'circle-3', circleName: '麻雀研究会' }]} onOK={() => onClose()} onCancel={onClose} />
    case 'confirmItem':
      return <ConfirmItemDlg majItems={[{ itemCode: 'MJ20', buyDt: 1767225600, endDt: 2147483647, qty: 3, useFlag: 0 }]} onClose={onClose} />
    case 'customInventory':
      return <CustomDlg hanCoin={500} hanCoupon={12} onEquipChange={() => {}} onRequestShop={() => {}} onClose={onClose} />
    case 'debugLogin':
      return <DebugLoginDlg servers={[{ label: 'Preview Server', serverId: 'preview', downloadUrl: 'https://example.invalid' }]} groups={[{ label: 'Preview Group', groupId: 'preview-group' }]} users={[{ id: 'preview-user', password: 'preview-password' }]} onOK={() => onClose()} onCancel={onClose} />
    case 'eventDialogs':
      return <Event200912PointDlg info={{ matchCount: 5, bestPoints: [120, 95, 80, 72, 61] }} onClose={onClose} onGoWeb={() => {}} />
    case 'legacyItemShop':
      return <ItemShopDlg cashCount={500} gemCount={24} gamMoney={1200} onClose={onClose} />
    case 'registration':
      return <RegistrationDlg idToken="popup-preview" googleInfo={{ pix: 'preview-user', name: 'プレビュー雀士', sex: 'M', birthYear: null, avatarId: '', password: '', isTestEnv: true, requiresRegistration: true }} onComplete={() => onClose()} onAuthExpired={onClose} />
    case 'profileEdit':
      return <ProfileEditDlg player={{ pix: 'preview-user', name: 'プレビュー雀士', sex: 'F', birthYear: 1994, avatarId: '/assets/images/characters/thumbnail_05f.png', password: '', isTestEnv: true, requiresRegistration: false }} onSave={async profile => profile} onComplete={onClose} onClose={onClose} />
    case 'shopTransaction':
      return <ResponsiveItemShopDlg cashCount={500} gemCount={24} gamMoney={1200} onClose={onClose} />
    case 'paifuSave':
      return <PaifuSaveDlg defaultFileName="preview-paifu.txt" initialComment="プレビュー用の牌譜です。" onSave={() => onClose()} onCancel={onClose} />
    case 'serialCode':
      return <SerialCodeDlg onOK={() => {}} onClose={onClose} />
    case 'gameSprites':
      return <GameSpritePreviewDlg onClose={onClose} />
    case 'kyoResult':
      return <KyoRes data={FORCED_KYO_RESULT} myOdr={0} canContinue onClose={onClose} />
    case 'hanResult':
      return <HanRes players={FORCED_HAN_RESULT} hasTip onClose={onClose} />
    default:
      return null
  }
}

export default function PopupPreviewScreen() {
  const [selectedId, setSelectedId] = useState<PreviewId>('welcome')
  const [isOpen, setIsOpen] = useState(true)
  const selected = PREVIEWS.find(entry => entry.id === selectedId) ?? PREVIEWS[0]
  const canPreview = selected.status === 'Complete'

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
          <p className="majak-popup-preview__description">{PREVIEWS.length} popup screens. Preview-ready screens open with deterministic fixture data.</p>
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
          <div><dt>Catalog status</dt><dd>{selected.status}</dd></div>
          <div><dt>Title</dt><dd>--majak-popup-font-title</dd></div>
          <div><dt>Emphasis</dt><dd>--majak-popup-font-emphasis</dd></div>
          <div><dt>Body</dt><dd>--majak-popup-font-body</dd></div>
        </dl>
        {canPreview && !isOpen && <button type="button" className="majak-popup-preview__open" onClick={() => setIsOpen(true)}>Open {selected.title}</button>}
      </section>
      {canPreview && isOpen && renderPreview(selectedId, () => setIsOpen(false))}
    </main>
  )
}
