/**
 * PreloadScene — ゲームリソースの事前ロード
 * CDownloadWnd 相当 (AP-09 §1-1)
 *
 * タイルスプライトシート (skin/1):
 *   mj_hai_omote_0_01.png  1369×63  37 frames × 37×63  → 手牌 (自分)
 *   mj_hai_omote_1_01.png  1665×43  37 frames × 45×43  → 右向き牌
 *   mj_hai_omote_2_01.png  1147×55  37 frames × 31×55  → 対面 / 捨て牌
 *   mj_hai_omote_3_01.png  1665×43  37 frames × 45×43  → 左向き牌
 *   mj_hai_sutehai_0_01.png 1147×55 37 frames × 31×55  → 捨て牌
 *   mj_hai_ura_0_01.png      37×63   1 frame  37×63   → 裏牌 (自分方向)
 *   mj_hai_ura_1_01.png      45×43   1 frame  45×43   → 裏牌 (右 / 左方向)
 *   mj_hai_ura_2_01.png      31×55   1 frame  31×55   → 裏牌 (対面方向)
 *   mj_hai_tachi_0_01.png  1369×63  37 frames × 37×63  → 自分方向手牌
 *   mj_hai_tachi_1_01.png    30×61   1 frame  30×61   → 右家手牌
 *   mj_hai_tachi_2_01.png    31×56   1 frame  31×56   → 対面手牌
 *   mj_hai_tachi_3_01.png    30×61   1 frame  30×61   → 左家手牌
 *   mj_hai_dora_01.png     1147×55  37 frames × 31×55  → ドラ表示牌
 *
 * タイルコード → フレームインデックス (PaiCode.h 準拠):
 *   MAN(kind=0): 0x01-0x09 → frame 0-8
 *   SOU(kind=1): 0x11-0x19 → frame 9-17
 *   PIN(kind=2): 0x21-0x29 → frame 18-26
 *   風牌(kind=3): 0x31-0x34 → frame 27-30
 *   三元牌(kind=3): 0x35-0x37 → frame 31-33
 */
import Phaser from 'phaser'
import { GAME_OPTIONS_REGISTRY_KEY, getGameOptions, type CreateGameOptions } from '../game/GameInstance'
import { emitGameLoadProgress } from '../game/gameLoadProgress'
import { getLegacyFullUiSkinId } from '../utils/legacySkinPalette'

const IMG  = '/assets/images/game'
const CUSTOM_BOARD_DEFAULT = 100000
const CUSTOM_BOARD_TENGOKU = 100002
const CUSTOM_HAI_DEFAULT = 100003

function customSkinSuffix(id: number): string {
  return String(id).padStart(2, '0')
}

function customSkinBase(id: number): string {
  return `${IMG}/skin/${id}`
}

export default class PreloadScene extends Phaser.Scene {
  private readonly initialOptions?: CreateGameOptions
  private readonly preloadOnly: boolean
  private readonly onPreloadComplete?: () => void
  private preloadStartedAt = 0

  constructor(config: { options?: CreateGameOptions; preloadOnly?: boolean; onComplete?: () => void } = {}) {
    super({ key: 'PreloadScene' })
    this.initialOptions = config.options
    this.preloadOnly = Boolean(config.preloadOnly)
    this.onPreloadComplete = config.onComplete
  }

  preload() {
    const options = this.initialOptions ?? getGameOptions()
    const customBgId = Number(options.customBgId ?? 0)
    const customBoardType = Number(options.customBoardType ?? 0)
    const customHaiId = Number(options.customHaiId ?? 0)
    this.preloadStartedAt = performance.now()
    if (!this.preloadOnly) emitGameLoadProgress('resources')
    console.info('[GameAssets] load start', {
      phase: this.preloadOnly ? 'app-startup-cache-warmup' : 'game-start',
      customBgId,
      customBoardType,
      customHaiId,
      startedAt: new Date().toISOString(),
    })
    const customBgSuffix = customSkinSuffix(customBgId)
    const customHaiSuffix = customSkinSuffix(customHaiId)
    const fullUiSkinId = getLegacyFullUiSkinId(customBgId, customBoardType)
    const fullUiSkinSuffix = customSkinSuffix(fullUiSkinId ?? 0)
    const hasCustomBg = customBgId > 0 && customBgId !== CUSTOM_BOARD_DEFAULT
    const hasFullCustomBg = fullUiSkinId != null
    const customBgBase = hasCustomBg ? customSkinBase(customBgId) : ''
    const fullUiSkinBase = hasFullCustomBg ? customSkinBase(fullUiSkinId) : ''
    const loadBgSkinImage = (key: string) => {
      if (hasFullCustomBg) this.load.image(`${key}_skin`, `${fullUiSkinBase}/${key}_${fullUiSkinSuffix}.png`)
    }
    const loadBgSkinSheet = (key: string, config: Phaser.Types.Loader.FileTypes.ImageFrameConfig) => {
      if (hasFullCustomBg) this.load.spritesheet(`${key}_skin`, `${fullUiSkinBase}/${key}_${fullUiSkinSuffix}.png`, config)
    }

    /* ── ゲームボード / サイドバー ── */
    this.load.image('mj_board',  `${IMG}/mj_board.png`)
    this.load.image('mj_sideBg', `${IMG}/mj_sideBg.png`)
    this.load.image('mj_h_bg',   `${IMG}/mj_h_bg.png`)
    if (hasCustomBg) {
      this.load.image('mj_board_skin', `${customBgBase}/mj_board_${customBgSuffix}.png`)
      this.load.image('mj_h_bg_skin', `${customBgBase}/mj_h_bg_${customBgSuffix}.png`)
    }
    loadBgSkinImage('mj_sideBg')
    if (fullUiSkinId === CUSTOM_BOARD_TENGOKU) {
      this.load.image('mj_taku_dragon_skin', `${fullUiSkinBase}/00mj_taku_dragon_${fullUiSkinSuffix}.png`)
    }

    /* ── 手牌 (縦立て, omote_0) 37 frames × 37×63 ── */
    this.load.spritesheet('hai_omote', `${IMG}/mj_hai_omote_0.png`,
      { frameWidth: 37, frameHeight: 63 })
    if (customHaiId > 0 && customHaiId !== CUSTOM_HAI_DEFAULT) {
      const base = customSkinBase(customHaiId)
      this.load.spritesheet('hai_omote_skin', `${base}/mj_hai_omote_0_${customHaiSuffix}.png`,
        { frameWidth: 37, frameHeight: 63 })
    }

    /* ── 方向別の表向き牌 (CMJObjPai::m_bmpOpen[1-3]) ── */
    this.load.spritesheet('hai_open_1', `${IMG}/mj_hai_omote_1.png`,
      { frameWidth: 45, frameHeight: 43 })
    this.load.spritesheet('hai_open_2', `${IMG}/mj_hai_omote_2.png`,
      { frameWidth: 31, frameHeight: 55 })
    this.load.spritesheet('hai_open_3', `${IMG}/mj_hai_omote_3.png`,
      { frameWidth: 45, frameHeight: 43 })
    if (customHaiId > 0 && customHaiId !== CUSTOM_HAI_DEFAULT) {
      const base = customSkinBase(customHaiId)
      this.load.spritesheet('hai_open_1_skin', `${base}/mj_hai_omote_1_${customHaiSuffix}.png`,
        { frameWidth: 45, frameHeight: 43 })
      this.load.spritesheet('hai_open_2_skin', `${base}/mj_hai_omote_2_${customHaiSuffix}.png`,
        { frameWidth: 31, frameHeight: 55 })
      this.load.spritesheet('hai_open_3_skin', `${base}/mj_hai_omote_3_${customHaiSuffix}.png`,
        { frameWidth: 45, frameHeight: 43 })
    }

    /* ── 捨て牌 (sutehai_0) 37 frames × 31×55 ── */
    this.load.spritesheet('hai_sute', `${IMG}/mj_hai_sutehai_0.png`,
      { frameWidth: 31, frameHeight: 55 })
    if (customHaiId > 0 && customHaiId !== CUSTOM_HAI_DEFAULT) {
      this.load.spritesheet('hai_sute_skin', `${customSkinBase(customHaiId)}/mj_hai_sutehai_0_${customHaiSuffix}.png`,
        { frameWidth: 31, frameHeight: 55 })
    }

    /* ── 裏牌 (CMJObjPai::m_bmpDown) ── */
    this.load.image('hai_ura_0', `${IMG}/mj_hai_ura_0.png`)
    this.load.image('hai_ura_1', `${IMG}/mj_hai_ura_1.png`)
    this.load.image('hai_ura_2', `${IMG}/mj_hai_ura_2.png`)
    if (customHaiId > 0 && customHaiId !== CUSTOM_HAI_DEFAULT) {
      const base = customSkinBase(customHaiId)
      this.load.image('hai_ura_0_skin', `${base}/mj_hai_ura_0_${customHaiSuffix}.png`)
      this.load.image('hai_ura_1_skin', `${base}/mj_hai_ura_1_${customHaiSuffix}.png`)
      this.load.image('hai_ura_2_skin', `${base}/mj_hai_ura_2_${customHaiSuffix}.png`)
    }

    /* ── 方向別の立ち牌 (CMJObjPai::m_bmpHand) ── */
    this.load.spritesheet('hai_tachi_0', `${IMG}/mj_hai_tachi_0.png`,
      { frameWidth: 37, frameHeight: 63 })
    this.load.image('hai_tachi_1', `${IMG}/mj_hai_tachi_1.png`)
    this.load.image('hai_tachi_2', `${IMG}/mj_hai_tachi_2.png`)
    this.load.image('hai_tachi_3', `${IMG}/mj_hai_tachi_3.png`)
    if (customHaiId > 0 && customHaiId !== CUSTOM_HAI_DEFAULT) {
      const base = customSkinBase(customHaiId)
      this.load.spritesheet('hai_tachi_0_skin', `${base}/mj_hai_tachi_0_${customHaiSuffix}.png`,
        { frameWidth: 37, frameHeight: 63 })
      this.load.image('hai_tachi_1_skin', `${base}/mj_hai_tachi_1_${customHaiSuffix}.png`)
      this.load.image('hai_tachi_2_skin', `${base}/mj_hai_tachi_2_${customHaiSuffix}.png`)
      this.load.image('hai_tachi_3_skin', `${base}/mj_hai_tachi_3_${customHaiSuffix}.png`)
    }

    /* ── ドラ表示牌 (dora) 37 frames × 31×55 — レガシー m_bmpHand[0] ── */
    this.load.spritesheet('hai_dora', `${IMG}/mj_hai_dora.png`,
      { frameWidth: 31, frameHeight: 55 })
    if (customHaiId > 0 && customHaiId !== CUSTOM_HAI_DEFAULT) {
      this.load.spritesheet('hai_dora_skin', `${customSkinBase(customHaiId)}/mj_hai_dora_${customHaiSuffix}.png`,
        { frameWidth: 31, frameHeight: 55 })
    }

    /* ── UI パーツ ── */
    this.load.image('mj_uiBoard',   `${IMG}/mj_uiBoard.png`)
    this.load.image('mj_resBtBoard',`${IMG}/mj_resBtBoard.png`)
    this.load.image('mj_watchBoard', `${IMG}/mj_watchBoard.png`)
    this.load.image('mj_myTurn',    `${IMG}/mj_myTurn.png`)
    this.load.image('mj_aiTurn',    `${IMG}/mj_aiTurn.png`)
    this.load.image('mj_hostmark',  `${IMG}/mj_hostmark.png`)
    this.load.image('mj_aiAvtrL',   `${IMG}/mj_aiAvtrL.png`)
    this.load.image('mj_aiAvtrW',   `${IMG}/mj_aiAvtrW.png`)
    this.load.image('mj_tenpaiicon',`${IMG}/mj_tenpaiicon.png`)
    this.load.spritesheet('mj_tonari_0', `${IMG}/mj_tonari_0.png`, { frameWidth: 31, frameHeight: 55 })
    this.load.spritesheet('mj_tonari_1', `${IMG}/mj_tonari_1.png`, { frameWidth: 45, frameHeight: 43 })
    this.load.spritesheet('mj_tapai_0', `${IMG}/mj_tapai_0.png`, { frameWidth: 31, frameHeight: 55 })
    this.load.spritesheet('mj_tapai_1', `${IMG}/mj_tapai_1.png`, { frameWidth: 45, frameHeight: 43 })
    this.load.spritesheet('mj_machihai_num', `${IMG}/mj_machihai_num.png`, { frameWidth: 9, frameHeight: 12 })
    this.load.image('mj_machihai_furiten', `${IMG}/mj_machihai_furiten.png`)
    this.load.image('mj_machihai_han', `${IMG}/mj_machihai_han.png`)
    this.load.image('mj_machihai_yakunashi', `${IMG}/mj_machihai_yakunashi.png`)
    this.load.image('mj_machihai_yakuman', `${IMG}/mj_machihai_yakuman.png`)
    this.load.image('mj_machihai_base01', `${IMG}/mj_machihai_base01.png`)
    this.load.image('mj_machihai_base02', `${IMG}/mj_machihai_base02.png`)
    this.load.image('mj_machihai_base03', `${IMG}/mj_machihai_base03.png`)
    this.load.image('mj_machihai_frame01', `${IMG}/mj_machihai_frame01.png`)
    this.load.image('mj_machihai_frame02', `${IMG}/mj_machihai_frame02.png`)
    this.load.image('mj_machihai_frame03', `${IMG}/mj_machihai_frame03.png`)
    this.load.image('mj_rkey',      `${IMG}/mj_rkey.png`)
    this.load.image('cursor_mouse', `${IMG}/mj_crsMouse_2(6).png`)
    loadBgSkinImage('mj_uiBoard')
    loadBgSkinImage('mj_resBtBoard')
    loadBgSkinImage('mj_watchBoard')
    loadBgSkinImage('mj_myTurn')
    loadBgSkinImage('mj_aiTurn')
    loadBgSkinImage('mj_aiAvtrL')
    loadBgSkinImage('mj_aiAvtrW')
    loadBgSkinImage('mj_machihai_han')
    loadBgSkinImage('mj_machihai_yakunashi')
    loadBgSkinImage('mj_machihai_yakuman')
    loadBgSkinImage('mj_machihai_base01')
    loadBgSkinImage('mj_machihai_base02')
    loadBgSkinImage('mj_machihai_base03')
    loadBgSkinImage('mj_machihai_frame01')
    loadBgSkinImage('mj_machihai_frame02')
    loadBgSkinImage('mj_machihai_frame03')
    this.load.spritesheet('mj_num_game00', `${IMG}/mj_num_game00.png`, { frameWidth: 9, frameHeight: 17 })
    this.load.spritesheet('mj_num_restpae', `${IMG}/mj_num_restpae.png`, { frameWidth: 10, frameHeight: 14 })
    this.load.spritesheet('mj_kyoku', `${IMG}/mj_kyoku.png`, { frameWidth: 33, frameHeight: 29 })
    this.load.spritesheet('mj_kyokuNum', `${IMG}/mj_kyokuNum.png`, { frameWidth: 62, frameHeight: 29 })
    loadBgSkinSheet('mj_num_game00', { frameWidth: 9, frameHeight: 17 })
    loadBgSkinSheet('mj_kyoku', { frameWidth: 33, frameHeight: 29 })
    loadBgSkinSheet('mj_kyokuNum', { frameWidth: 62, frameHeight: 29 })
    for (let loc = 0; loc < 4; loc++) {
      this.load.spritesheet(`mj_myfan_${loc}`, `${IMG}/mj_myfan_${loc}.png`, { frameWidth: 33, frameHeight: 33 })
      this.load.spritesheet(`mj_oyahuda_${loc}`, `${IMG}/mj_oyahuda_${loc}.png`, { frameWidth: 55, frameHeight: 53 })
      loadBgSkinSheet(`mj_myfan_${loc}`, { frameWidth: 33, frameHeight: 33 })
      loadBgSkinSheet(`mj_oyahuda_${loc}`, { frameWidth: 55, frameHeight: 53 })
    }
    this.load.spritesheet('mj_dice', `${IMG}/mj_dice.png`, { frameWidth: 17, frameHeight: 21 })
    loadBgSkinSheet('mj_dice', { frameWidth: 17, frameHeight: 21 })
    this.load.image('mj_wareme00', `${IMG}/mj_wareme00.png`)
    this.load.image('mj_wareme01', `${IMG}/mj_wareme01.png`)
    this.load.image('mj_wareme02', `${IMG}/mj_wareme02.png`)
    this.load.image('mj_wareme03', `${IMG}/mj_wareme03.png`)
    this.load.image('mj_richbar_0', `${IMG}/mj_richbar_0.png`)
    this.load.image('mj_richbar_1', `${IMG}/mj_richbar_1.png`)
    this.load.image('mj_richbar_0_Festa', `${IMG}/mj_richbar_0_Festa.png`)
    this.load.image('mj_richbar_1_Festa', `${IMG}/mj_richbar_1_Festa.png`)
    this.load.image('mj_ryu_richbar_side_b', `${IMG}/mj_ryu_richbar_side_b.png`)
    this.load.image('mj_ryu_richbar_length_b', `${IMG}/mj_ryu_richbar_length_b.png`)
    this.load.image('mj_ryu_richbar_side_y', `${IMG}/mj_ryu_richbar_side_y.png`)
    this.load.image('mj_ryu_richbar_length_y', `${IMG}/mj_ryu_richbar_length_y.png`)
    for (const key of ['mj_wareme00', 'mj_wareme01', 'mj_wareme02', 'mj_wareme03']) {
      loadBgSkinImage(key)
    }
    this.load.spritesheet('mj_baloon_0', `${IMG}/mj_baloon00.png`, { frameWidth: 236, frameHeight: 202 })
    this.load.spritesheet('mj_baloon_1', `${IMG}/mj_baloon01.png`, { frameWidth: 267, frameHeight: 167 })
    this.load.spritesheet('mj_baloon_2', `${IMG}/mj_baloon02.png`, { frameWidth: 236, frameHeight: 202 })
    this.load.spritesheet('mj_baloon_3', `${IMG}/mj_baloon03.png`, { frameWidth: 267, frameHeight: 167 })

    /* ── 牌譜グラフウィンドウ (CMJPaifWnd::m_Screen) ── */
    this.load.image('mj_recBg', `${IMG}/mj_recBg.png`)
    this.load.spritesheet('mj_recPaeFt', `${IMG}/mj_recPaeFt.png`, { frameWidth: 20, frameHeight: 29 })
    this.load.spritesheet('mj_recPaeSd', `${IMG}/mj_recPaeSd.png`, { frameWidth: 28, frameHeight: 22 })
    this.load.spritesheet('mj_recPaeSm', `${IMG}/mj_recPaeSm.png`, { frameWidth: 13, frameHeight: 19 })
    this.load.image('mj_recPaeBk', `${IMG}/mj_recPaeBk.png`)
    this.load.spritesheet('mj_recNotesIcn', `${IMG}/mj_recNotesIcn.png`, { frameWidth: 20, frameHeight: 14 })
    this.load.image('mj_recReachBar', `${IMG}/mj_recReachBar.png`)
    this.load.spritesheet('mj_num_rh', `${IMG}/mj_num_rh.png`, { frameWidth: 10, frameHeight: 14 })
    this.load.spritesheet('mj_opt_0', `${IMG}/mj_opt_0.png`, { frameWidth: 17, frameHeight: 17 })
    this.load.spritesheet('mj_opt_1', `${IMG}/mj_opt_1.png`, { frameWidth: 17, frameHeight: 17 })
    this.load.spritesheet('mj_opt_2', `${IMG}/mj_opt_2.png`, { frameWidth: 17, frameHeight: 17 })
    this.load.spritesheet('mj_opt_3', `${IMG}/mj_opt_3.png`, { frameWidth: 17, frameHeight: 17 })
    this.load.spritesheet('mj_opt_4', `${IMG}/mj_opt_4.png`, { frameWidth: 17, frameHeight: 17 })
    this.load.spritesheet('mj_opt_5', `${IMG}/mj_opt_5.png`, { frameWidth: 17, frameHeight: 17 })
    this.load.spritesheet('mj_opt_6', `${IMG}/mj_opt_6.png`, { frameWidth: 17, frameHeight: 17 })
    this.load.spritesheet('mj_opt_7', `${IMG}/mj_opt_7.png`, { frameWidth: 17, frameHeight: 17 })
    this.load.spritesheet('mj_optwar', `${IMG}/mj_optwar.png`, { frameWidth: 17, frameHeight: 17 })
    this.load.spritesheet('mj_opttip', `${IMG}/mj_opttip.png`, { frameWidth: 17, frameHeight: 17 })
    this.load.spritesheet('mj_optron', `${IMG}/mj_optron.png`, { frameWidth: 17, frameHeight: 17 })
    loadBgSkinImage('mj_recBg')
    loadBgSkinSheet('mj_recPaeFt', { frameWidth: 20, frameHeight: 29 })
    loadBgSkinSheet('mj_recPaeSd', { frameWidth: 28, frameHeight: 22 })
    loadBgSkinSheet('mj_recPaeSm', { frameWidth: 13, frameHeight: 19 })
    for (const key of ['mj_recPaeBk']) {
      loadBgSkinImage(key)
    }
    loadBgSkinSheet('mj_num_rh', { frameWidth: 10, frameHeight: 14 })

    /* ── 操作ボタン (4フレーム スプライト) ── */
    this.load.spritesheet('btn_ron',   `${IMG}/mj_btRon.png`,   { frameWidth: 66, frameHeight: 40 })
    this.load.spritesheet('btn_tsumo', `${IMG}/mj_btTsumo.png`, { frameWidth: 66, frameHeight: 40 })
    this.load.spritesheet('btn_chi',   `${IMG}/mj_btChi.png`,   { frameWidth: 66, frameHeight: 40 })
    this.load.spritesheet('btn_pon',   `${IMG}/mj_btPon.png`,   { frameWidth: 66, frameHeight: 40 })
    this.load.spritesheet('btn_kan',   `${IMG}/mj_btKan.png`,   { frameWidth: 66, frameHeight: 40 })
    this.load.spritesheet('btn_pass',  `${IMG}/mj_btPass.png`,  { frameWidth: 76, frameHeight: 40 })
    this.load.spritesheet('btn_flow',  `${IMG}/mj_btFlow.png`,  { frameWidth: 76, frameHeight: 40 })
    this.load.spritesheet('btn_hua',   `${IMG}/mj_BtHua.png`,   { frameWidth: 58, frameHeight: 17 })
    this.load.spritesheet('btn_reach', `${IMG}/mj_btRichi.png`, { frameWidth: 66, frameHeight: 40 })
    this.load.spritesheet('btn_fury',  `${IMG}/mj_btFuriten.png`, { frameWidth: 66, frameHeight: 40 })
    const skinButtonSheets: Array<[string, string, number]> = [
      ['btn_ron', 'mj_btRon', 66],
      ['btn_tsumo', 'mj_btTsumo', 66],
      ['btn_chi', 'mj_btChi', 66],
      ['btn_pon', 'mj_btPon', 66],
      ['btn_kan', 'mj_btKan', 66],
      ['btn_pass', 'mj_btPass', 76],
      ['btn_flow', 'mj_btFlow', 76],
      ['btn_reach', 'mj_btRichi', 66],
      ['btn_fury', 'mj_btFuriten', 66],
    ]
    for (const [key, file, frameWidth] of skinButtonSheets) {
      if (hasFullCustomBg) this.load.spritesheet(`${key}_skin`, `${fullUiSkinBase}/${file}_${fullUiSkinSuffix}.png`, { frameWidth, frameHeight: 40 })
    }

    const btns: [string, string][] = [
      ['btn_ok',    'mj_btOk.png'],
      ['btn_noten', 'mj_btNoten.png'],
      ['btn_tsumogiri', 'mj_btTsumoGiri.png'],
      ['btn_autopass',  'mj_btAutoPass.png'],
      ['btn_autohora',  'mj_btAutoHoura.png'],
    ]
    for (const [key, file] of btns) {
      this.load.image(key, `${IMG}/${file}`)
    }

    for (const prefix of ['ron', 'tumo', 'kan', 'reach', 'pon', 'chi']) {
      for (let frame = 1; frame <= 14; frame++) {
        const name = `mj_${prefix}_w_${String(frame).padStart(2, '0')}`
        this.load.image(name, `${IMG}/${name}.png`)
      }
    }
    for (const dir of ['00', '01']) {
      for (let frame = 0; frame <= 4; frame++) {
        const name = `mj_ef_reachhai${dir}_${String(frame).padStart(2, '0')}`
        this.load.image(name, `${IMG}/${name}.png`)
      }
      for (let frame = 0; frame <= 6; frame++) {
        const name = `mj_ef_reachhai_lgt${dir}_${String(frame).padStart(2, '0')}`
        this.load.image(name, `${IMG}/${name}.png`)
      }
    }
  }

  create() {
    const durationMs = Math.round(performance.now() - this.preloadStartedAt)
    console.info('[GameAssets] load complete', {
      phase: this.preloadOnly ? 'app-startup-cache-warmup' : 'game-start',
      durationMs,
      completedAt: new Date().toISOString(),
    })
    if (this.preloadOnly) {
      this.onPreloadComplete?.()
      return
    }
    emitGameLoadProgress('scene', { resourceDurationMs: durationMs })
    const options = this.game.registry.get(GAME_OPTIONS_REGISTRY_KEY) ?? getGameOptions()
    this.scene.start('GameScene', options)
  }
}

