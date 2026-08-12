# Client Legacy Animation Parity Audit

- Audit date: 2026-08-12
- Legacy client: `Majak4_legacy/client/client/HgMajak2/`
- Current client: `client/src/`
- Resource roots: `client/public/assets/`, `client/_archive-unused/`
- Scope: visual sequences driven by frame arrays, timers, animated images, movement, fades, or repeated texture swaps
- Excluded: ordinary four-state button sprites and code paths disabled in the audited legacy revision

## Status legend

| Status | Meaning |
|---|---|
| `E` | Legacy animation is migrated with substantially equivalent trigger and timing. |
| `P` | The state or feature exists, but animation, frame sequence, or presentation is reduced or changed. |
| `M` | The active legacy animation has no current Web visual implementation. |
| `D` | The code or assets exist in legacy, but playback is disabled or commented out in the audited revision. |

## Executive result

Twenty active legacy presentation paths were compared with the current Web client. The ten missing paths found by the audit were migrated on 2026-08-12.

| Result | Count |
|---|---:|
| Equivalent or close migration (`E`) | 17 |
| Partial or intentionally different presentation (`P`) | 3 |
| Missing active legacy animation (`M`) | 0 |

The migrated playback uses the existing `richiEffect`, custom costume IDs, result type, score class, trick title, and `gemGame` values. Phaser and the React result overlay share one result-duration calculation so the result UI no longer covers the board effects.

## Migrated animations

| Priority | Legacy animation | Legacy evidence | Migrated Web behavior |
|---:|---|---|---|
| P0 | Match-start representative tile reveal | `CMJTblDraw::IniSet`, `MJTblDraw4.cpp`: four server-assigned wind tiles change together from down for 1,000 ms, to hand for 100 ms, to open for 2,000 ms | The first `INIKYO` after `INIHAN` uses the authoritative engine-order mapping to reveal East/South/West/North at the legacy positions and timings, then continues into skill, gem, and deal staging. Replay, resync, hidden-tab, and reduced-motion paths remain immediate. |
| P0 | Initial deal staging | `CMJTblDraw::PutHaipai`, `MJTblDraw4.cpp`: 12 four-tile batches followed by four single tiles, with 100 ms pauses, about 1.6 seconds total | Authoritative hands are applied immediately, while sprites reveal in the legacy order; replay, resync, hidden-tab, and reduced-motion paths remain immediate. |
| P0 | Dora/dead-wall reveal | `CMJTblDraw::PutExpose`, `MJTblDraw4.cpp`: tile rises 5 px, changes to open after 100 ms, then returns | Newly exposed indicators use the 5 px lift, 100 ms flip, return, and both expose sounds. |
| P0 | Reach declaration effect | `CMJTblDraw::PutRich`, `MJTblDraw4.cpp`: type 1 uses 9 frames/730 ms; type 2 uses 13 frames/1,060 ms; carnival type has spin/flash stages | Types 1, 2, and Festa use their original frames, coordinates, timings, and sounds before the final stick remains visible. |
| P0 | Ron score-tier effects | `CMJTblDraw::CallAction`, `MJTblDraw4.cpp`: 11, 20, or 27 additive frames depending on score tier; higher tiers stagger copies | Score tiers use 30 ms additive frames; high ron uses the legacy staggered copies and captured claimed-tile position. |
| P0 | Tsumo score-tier effects | `CMJTblDraw::CallAction`, `MJTblDraw4.cpp`: 12, 13, or 15 additive frames depending on score tier | Score tiers use 30 ms additive frames, legacy fixed positions, and seat rotation. |
| P0 | High-win and yakuman finish | `CMJTblDraw::EndKyo`, `MJTblDraw4.cpp`: three-frame hora fire and 13-frame yakuman fan with a 1.3-second final hold | Fire and yakuman fan run after score effects; L2 winners suppress hora fire as in legacy, and the React result overlay waits for the shared total duration. |
| P1 | Elemental trick-title effects | `CMJTblDraw::IniSet` and `PutPaiEffect`, `MJTblDraw4.cpp`: fire/water/wind/earth L1 round-start and L2 winning-tile sequences | L1/L2 families use the legacy title-to-element formula, per-frame timing, coordinates, additive blend, and sounds. |
| P1 | Dragon-orb game opening | `drawGemGameBegining`, `MJTblDraw4.cpp`: `mj_ryu_normal_01..10` or `mj_ryu_big_01..10`, about 1.8 seconds | `gemGame` 1/2 selects the normal/big ten-frame 1.8-second sequence and matching sound before dealing. |
| P1 | Costume action animation | `CMJOdrBox::PutOdrBox` / `OnCharaAnim`, `MJTblDraw3.cpp` and `MJTblDraw1.cpp`: default, chi, pon, kan, reach, ron, and tsumo streams updated every 100 ms | All GIF-backed streams run at 100 ms; one-shot calls return to default or reach, while reach remains looping. |
| P1 | Result hora/hoju status animation | `CMJKyoRes::PutKyoBal`, `MJKyoRes.cpp`: animated `mj_ef_status_hora` / `mj_ef_status_hoju` images | The responsive result keeps its layout and cycles the original 2/4 frames at their 200 ms GIF delay and 122 x 48 size. |

## Partial or changed presentations

| Legacy path | Status | Current difference |
|---|---:|---|
| Discard presentation (`PutTapai`) | `P` | Legacy performs immediate state changes and an optional 167 ms reveal. Web adds a 167 ms curved discard flight, so it is animated but not legacy-equivalent. |
| Action-selection tile blink (`BlinkTapai`) | `P` | Legacy toggles the actionable discard/tile source frame every 100 ms. Web uses selection lift/cursor and button state rather than the same blink. |

## Equivalent or close migrations

| Animation | Status | Current implementation |
|---|---:|---|
| Waiting/ready/start prompt alpha pulse | `E` | `RoomScreen` uses a 2,000 ms opacity cycle matching `CMJObjPrompt::OnAnimate`. |
| Dice/wareme roll | `E` | `UIScene.startRoundDiceRoll()` preserves the 2,000 ms delay and 35 random pairs at 20 ms. |
| Reach discard streak and glare | `E` | `GameScene.showReachTileEffect()` preserves the 5 x 25 ms movement and 7 x 50 ms glare stages. |
| Call balloon and avatar lifetime | `E` | `UIScene.showCallAction()` uses the same action-frame mapping and about 1.1-second lifetime. |
| Lottery slot | `E` | `LotSlotDlg` preserves 30 ms spinning and serial reel stopping. |
| Slide announcement | `E` | `SlideAnnounce` preserves 1 px/ms slide-in/out and the 2,000 ms hold. |

The room/game emoticon schedules are also migrated closely, but they are not included in the 20-path count because their frame ownership is partly outside the audited `HgMajak2` animation code.

## Dormant legacy effects

These should not be counted as missing active behavior without a separate product decision.

| Legacy effect | Status | Reason |
|---|---:|---|
| `mj_kan_w_*`, `mj_pon_w_*`, `mj_chi_w_*`, `mj_reach_w_*` board words | `D` | Families are loaded, but their legacy playback assignments are commented out. Web preloads them but only plays ron/tsumo board words. |
| `mj_ef_hora_exmk00..09` | `D` | Playback code remains, but the legacy initialization that loads the frames is commented out. |
| Tengoku dragon rotation | `D` | The legacy condition is forced false and animation setup is disabled. |
| Old Han-result XP/rank sequence | `D` | The legacy block is commented out. The Web responsive result count-up is a current presentation, not a missing legacy port. |

## Resource archive correction

The earlier unused-resource scan classified assets by current Web references. That is valid for current bundle usage, but it is not sufficient for migration parity.

Of the 141 files moved to `client/_archive-unused/game` on 2026-08-12, at least 119 were frames from active legacy animations. They have been restored to active public assets:

| Archived family | Files | Active legacy role |
|---|---:|---|
| `eff_roneff_c_*` | 27 | High-tier ron effect |
| `eff_tumoeff_*`, `_b_*`, `_c_*` | 40 | Tsumo score-tier effects |
| `eff_rontumoeff_d_*` | 30 | Yakuman ron/tsumo effect |
| `mj_ryu_normal_*` | 11 | Dragon-orb normal opening family, including its related image |
| `mj_ryu_big_*` | 11 | Dragon-orb big opening family, including its related image |

They are now preloaded and used by the Web runtime. Do not return these families to the unused archive.

Additional missing-animation resources were already archived before this audit, including `mj_ef_L1*`, `mj_ef_L2*`, and `mj_ef_horafire*`. Active public resources still include the reach-effect frame families, low/mid ron effects, status effects, yakuman variants, and costume images.

## Completed implementation order

1. Restored and implemented reach declaration effects.
2. Implemented ron/tsumo score-tier, high-win, and yakuman visuals with shared result timing.
3. Added staged dealing and dora reveal with replay, reconnect, hidden-tab, and reduced-motion safeguards.
4. Ported trick-title L1/L2 and Dragon Pearl opening effects.
5. Extracted animated HIM frames and ported costume streams and responsive result status motion.

## Validation notes

- Client TypeScript validation passed with `tsc --noEmit`; no frontend build was run.
- Focused Vitest coverage for shared result durations passed (4 tests), including L2 ordering and hora-fire suppression.
- Mobile landscape effect positioning passed focused geometry tests and browser checks at 667 x 375 and 932 x 430. Seat-bound L1/L2, tsumo, and reach effects preserve their tile/hand/stick anchor offsets; full-board gem and yakuman effects use the visible mobile world center.
- Mobile L1 and tsumo effects use the first rendered hand sprite as their seat anchor; L2 uses the rendered winning tile. This avoids aspect-ratio-dependent shifts from static mobile coordinates.
- The legacy `CMJImgBmpEx` loader preserves the fourth byte of 32-bit BI_RGB `mj_ef_*` / `eff_*` bitmaps as alpha. The converter now preserves that byte, and 268 existing Web effect PNGs were regenerated from their original HIM files. All 244 source-alpha bitmaps matched byte-for-byte; 24 GIF/other-format resources retained their existing fallback conversion path.
- Animated HIM extraction produced 366 costume frames and 6 result-status frames; all referenced animation families were checked on disk.
- The official manual does not prescribe frame counts or timing for these effects, so the legacy client is the controlling source for animation details.
- Before implementing each effect, verify image dimensions and orientation against the corresponding legacy coordinates and frame order.