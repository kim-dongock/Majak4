# Mahjong Engine Legacy Parity Audit

- Audit date: 2026-08-10
- Current engine: `server/Engine/`
- Legacy engine: `Majak4_legacy/server/server/`
- Legacy training AI: `Majak4_legacy/client/client/HgMajak2/`
- Specification priority: legacy implementation, then AP-15 official manual, then current implementation
- Product decision: when AP-15/current behavior differs from the legacy engine, preserve the legacy behavior

## Status legend

| Status | Meaning |
|---|---|
| `E` | Semantically equivalent to the legacy implementation |
| `A` | Adapted for C#/.NET without a game-behavior change |
| `X` | Current-only extension with no legacy engine counterpart |
| `F` | Mismatch fixed by this audit |
| `T` | Equivalent or plausible, but focused regression coverage is still missing |
| `N/A` | C++ lifetime/operator/debug machinery that does not require a C# method |

## Executive result

All production legacy engine entry points have a current counterpart. No core class is wholly missing. This audit fixed 17 concrete mismatches under the final legacy-first decision:

1. Restored the pair before general-yaku evaluation so `Churenpaotou` and `Churenpaotou2` are reachable.
2. Excluded an open `Chi` from concealed winning-tile attribution for `Sanankou`.
3. Excluded an open `Chi` from wait-fu and ron-triplet attribution.
4. Prevented `GetValidActions` from advertising a no-yaku ron.
5. Exposed the normal-rule fifth kan needed to reach the legacy abortive-draw path.
6. Preserved per-recipient sent flags when `Bipai.Open` makes a tile public.
7. Made failed `Pon` and `MinKan` calls leave pao state unchanged.
8. Restored legacy `ChaKan` validation order and `ErrPaiNotMatch` result.
9. Made `Richi` discard the physical hand tile selected by its wall index.
10. Restored `ErrPaiAlreadyUsed` for duplicate physical wall indices.
11. Preserved called-tile identity after sorting a chi, matching legacy AI visible-tile counts.
12. Removed current-only kuikae rejection so post-chi and post-pon discards follow legacy.
13. Preserved the completed added-kan state when it is robbed, as legacy does.
14. Restored single-value special yakuman in grade games through legacy `bRevaluate=false`.
15. Restored legacy arithmetic value `41` for `PaiCode.Invalid.GetSerial()`.
16. Restored legacy training-AI dora scoring from the original 14-tile array for tsumo and the original first 13 tiles plus the candidate tile for ron.
17. Restored the pair before the boolean `CheckYaku` general-yaku evaluation, matching the legacy decomposition order.

## PaiCode

Legacy sources: `CPaiCode.h`, `CPaiCode.cpp`. Current source: `server/Engine/PaiCode.cs`.

| Current function | Legacy function | Status | Result |
|---|---|---:|---|
| constructors / `Invalid` / `MakeSerial` | constructors / `MakeInvalidPai` / `MakeSerialPai` | `E/A` | Valid tile encoding and sentinel construction match. |
| `IsValid` | `IsValid` | `E` | Same valid code ranges. |
| `IsRaotoupai` | `IsRaotoupai` | `E` | Same suited terminal test. |
| `IsYaochupai` | `IsYaochupai` | `E` | Same terminal-or-honor test. |
| `IsShupai` / `IsTsupai` | same | `E` | Equivalent. |
| `IsWind` / `IsWindOf` / `IsFonpaiOf` | `IsWind` / `IsWind(int)` / `IsFonpai` | `E` | Equivalent wind predicates. |
| `IsSangenpai` / `IsSangenpaiOf` | same | `E` | Equivalent dragon predicates. |
| `IsHuapai` | `IsHuapai` | `E` | Same flower code boundary. |
| `IsGreen` | `IsGreen` | `E` | Same green-tile table. |
| `GetNumber` / `GetKind` | same | `E` | Equivalent. |
| `GetSerial` | `GetSerial` | `F` | Uses the same unconditional arithmetic; the invalid sentinel returns legacy value `41`. |
| `GetSerialRed` | `GetSerialRed` | `E` | Same red serial extension. |
| `GetNextNumberPai` / `GetNextKindPai` | same | `E/A` | Valid input matches; current throws for invalid honor navigation instead of relying on a C++ assertion. |
| equality, ordering, `Equals`, `GetHashCode` | comparison operators | `E/A` | Equality intentionally ignores red and wall index in both implementations. |
| `ToString` / public `Code` | debug-only `GetCode` | `X` | Diagnostic extension. |
| mutating `NextNumber`, `NextKind`, `++`, `--`, `+=`, `-=` | C++ operators | `N/A` | No production legacy call requires a mutating C# counterpart. |

## Bipai

Legacy sources: `CBipai.h`, `CBipai.cpp`. Current source: `server/Engine/Bipai.cs`.

| Current function | Legacy function | Status | Result |
|---|---|---:|---|
| constructor | `CBipai::CBipai` | `E` | Dead-wall reserve starts at four. |
| `Init` | `Init` | `E` | 136 tiles, red-tile layouts, indices, reserve, and visibility reset match. |
| `Chipai` | `Chipai` | `E/A` | Same Fisher-Yates shape and pointer reset; runtime RNG API differs. |
| `ChipaiYakuDebug` | same | `E/A` | Debug target tables and swaps are ported; helpers are extracted. |
| `BuildDebugTargets` / `ApplyDebugTargets` | inline debug loops | `A` | Structural extraction only. |
| `GetBipai` / `SetBipai` | same | `E` | Rotation, reindexing, and pointer reset match. |
| `GetPai` | `GetPai` | `E/A` | C# returns a value-type copy rather than a const reference. |
| `GetBipaiCount` | same | `E` | Live-wall count formula matches. |
| `SetOpenIdx` / `GetOpenIdx` | same | `E` | Equivalent. |
| `GetBipPtr` / `GetRinPtr` | same | `E` | Wraparound and rinshan pointer formulas match. |
| `GetDoraIdx` / `GetDoraDisplay` | same | `E` | Dora and ura positions match. |
| `Open` / `OpenAll` | same | `F` | Now ORs public visibility without erasing recipient sent bits. |
| `GetNextTsumo` / `GetNextRinshan` | same | `E` | Pointer increments and owner visibility match. |
| `GetPaiInfo` | same | `E` | Visibility and per-recipient one-time send masks match. |
| none | `Story` | `D` | Legacy `_TESTVER` file-driven wall injection is not ported; `SetBipai` covers deterministic production/test injection, not the file format. |

## EnginePlayer

Legacy sources: `HMajakPlayer.h`, `HMajakPlayer.cpp`. Current source: `server/Engine/EnginePlayer.cs`.

| Current function | Legacy function | Status | Result |
|---|---|---:|---|
| `InitHanchan` | same | `A` | End state through `MajakGameLogic.InitHanchan -> InitKyoku` matches. Current resets result aggregates here and mode/action in `InitKyoku`. |
| `InitKyoku` | same | `A` | Per-hand collections and flags match; current also clears cached `Yaku` and mode/action defensively. |
| `Tsumo` | same | `E` | Appends the physical tile. |
| `Tapai` | same | `F` | Removal and state changes match; no current-only kuikae rejection remains. |
| `Richi` | same | `F` | Tenpai validation and pending ippatsu match; physical discarded tile is now preserved. |
| `SetRichi` | same | `E` | Riichi type and 1,000-point deduction match. |
| `ClearIppatsu` / `ClearNagashiMangan` / `ClearYakitori` | same | `E` | Equivalent. |
| `SetTempFuriten` | direct legacy field write | `A` | Encapsulation only. |
| `Taopai` | same | `E` | Nine-or-more distinct terminal/honor kinds match. |
| `Chi` | same | `E/F` | Meld and menzen changes match; the called physical index is retained to reproduce legacy AI accounting after the C# list is sorted. |
| `Pon` | same | `F` | Successful behavior matches; pao now changes only after tile removal succeeds. |
| `MinKan` | same | `F` | Successful behavior matches; failed calls no longer assign pao. |
| `AnKan` | same | `E` | Riichi restriction, meld creation, and count match. |
| `ChaKan` | same | `F` | Pon lookup precedes hand removal and absent pon returns `ErrPaiNotMatch`. |
| `Hua` | same | `E` | Flower validation and movement match. |
| `CheckTempai(PaiCode)` / `CheckTempai()` | same overloads | `E` | Delegates to the equivalent hand evaluator. |
| `CheckHoraForm()` / `CheckHoraForm(PaiCode)` | same overloads | `E` | Equivalent. |
| `CheckFuriten` | same | `E` | Temporary and own-discard furiten match. |
| `FindInTehai` / `TryRemoveTehai` / `SortTehaiByCode` | `RemoveTehai` and list sort | `E/A` | Same atomic move for valid unique indices; current helper rejects invalid selections safely. |
| `ProcessPao` | same | `E/F` | Third dragon and fourth wind thresholds match; call timing was fixed. |
| legacy check-only overloads | no direct method | `A` | Read-only prediction is implemented by `GetValidActions`; its ron mismatch was fixed. |

## Hand

Legacy sources: `CHand.h`, `CHand.cpp`. Current source: `server/Engine/Hand.cs`.

| Current function | Legacy function | Status | Result |
|---|---|---:|---|
| constructor | `CHand::CHand` | `E` | Builds the same 34-count hand and four meld references. |
| `CheckTempai(int)` / `CheckTempai()` | same | `E` | Same discard/add candidate search. |
| `CheckHoraForm(int)` / `CheckHoraForm()` | same | `E/A` | Kokushi, chiitoitsu, then normal form; current adds bounds safety. |
| `CheckYaku` / `GetYaku` | same | `E` | Same control flow and runtime `bRevaluate` equivalent. Grade games pass false, matching legacy. |
| `CheckAnkan` | same | `A` | Current restores scratch counts on all exits, avoiding legacy evaluator-state leakage. |
| `ChkKokushi` / `ChkChitoi` | `chkKokushi` / `chkChitoi` | `E` | Exact form checks match. |
| `ChkHead` / `ChkMent` | `chkHead` / `chkMent` | `E/A` | Recursive form search matches with defensive bounds guards. |
| `SetContext` | inline wrapper assignment | `A` | Structural extraction. |
| `ChkYaku` / `GetYakuInternal` | `chkYaku` / `getYaku` | `E` | Same normal, chiitoitsu, and kokushi routing. |
| `ChkYakuGeneral(bool)` | same | `F` | Pair counts are restored before each yaku evaluation, matching the legacy decomposition order. |
| `GetYakuGeneral` | same | `F` | Pair counts are restored before scoring, enabling both Chuuren variants. |
| `PreFillFuroMen` | inline loop | `A` | Equivalent. |
| `ResMenShu` / `ResMenKou` | `resMenShu` / `resMenKou` | `E` | Same sequence-first and triplet-first recursive decompositions. |
| `ChkYakuGeneral(Yaku,...)` | same | `F` | All adopted yaku are present; open chi no longer causes false Sanankou. |
| `ChkYakuChitoi` | same | `E` | Seven pairs, 25 fu, flush, tanyao, honroutou, and all-honor logic match. |
| `ChkYakuKokushi` | same | `E` | 13-sided identification and grade-game single-value revaluation match legacy. |
| `AddYakuKui` | same | `E` | One-han open reduction matches. |
| `ChkYakuhai` | same | `E` | Dragon, round wind, and seat wind counts match. |
| `CalcFu` | same | `F` | Formula matches and open chi is excluded from winning-tile wait attribution. |
| `PaiIsGreen` / `Mentsu.IsGreen` | `PAI::IsGreen` / `MENTSU::IsGreen` | `E` | Green sequence and triplet tables match. |
| none | `chkTsuiso7`, `chkSushiho` | `N/A` | Dead legacy helpers with no call sites; active evaluators cover the outcomes. |

### Yaku coverage result

All AP-15 yaku are implemented. The four special double-yakuman forms use their legacy single value in grade games. The fixed Chuuren path recognizes normal and pure nine-sided variants. Direct deterministic fixtures now cover Iipeikou, Ryanpeikou, Chanta, Junchan, standard-form Honroutou, Sankantsu, Suukantsu, Shosangen, Daisangen, every open/closed triplet and kan fu class, kanchan, penchan, tanki, tsumo, menzen ron, and double-wind pair fu.

## Yaku

Legacy source: `HMajakYaku.h`. Current source: `server/Engine/Yaku.cs`.

| Current function | Legacy function | Status | Result |
|---|---|---:|---|
| `Clear` | same | `E` | Resets lists, counters, fu, points, chip, and flags. |
| `AddYakuman` | same | `E` | Adds multiplier entries and han sum. |
| `AddYaku` | same | `E` | Adds ordinary yaku and han. |
| `CalcHoraTen` | same | `E` | Fu exponent, mangan/kiriage, han limits, counted yakuman, and basic points match. |
| `CheckAndUpdate` | same | `E/A` | Chooses higher basic points then han. Current list assignment is safe in active local-candidate use, but aliasing has no direct regression. |

## MajakGameLogic

Legacy sources: `HMajakGameLogic.h`, `HMajakGameLogic.cpp`. Current source: `server/Engine/MajakGameLogic.cs`.

| Current function | Legacy function | Status | Result |
|---|---|---:|---|
| constructor / field initializers | constructor/destructor | `E/A` | Runtime state initialization matches; GC replaces destructor. |
| `Init` | same | `E/X` | Core reset matches; current also resets debug state. |
| `SetBipai` | same | `E` | Buffered wall and wareme injection match. |
| `SetDebugHaipaiYaku` / `SetDebugEndAfterEast1` | debug macros/test hooks | `X/A` | Controlled test extensions. |
| `InitHanchan` | same | `E` | Rule copy, seat shuffle, points, chips, wall, and first hand match. |
| `ProcessAction` | same | `F` | Count/range/mode dispatch matches; duplicate index now returns legacy `ErrPaiAlreadyUsed`. |
| `ProcessModeKyo` | `MODE_KYO` branch | `E/T` | Next-hand, all-last, agari-yame, cut, and end routing are ported. All-last choice combinations need more tests. |
| `ProcessModeAga` | `MODE_AGA` branch | `E` | Continue/stop behavior matches. |
| `ProcessTurn` | same | `E` | Tsumo, discard, riichi, abort, kan, flower, and response entry match. |
| `ProcessFuro` | same | `E/F` | Priority, multi-ron modes, temporary furiten, and resolution match, including retaining completed added-kan state after chankan. |
| accessors `GetBipai`, `GetBipaiPai`, `GetBipaiCount`, `GetHojuOrder` | same/inlines | `E/X` | Count accessor is a current convenience extension. |
| `SetCutGame` | tournament field write | `A` | Encapsulates legacy cut flag. |
| `GetValidActions` | legacy check-only action chain | `F/X` | Current UI/service extension; no-yaku ron and normal fifth-kan mismatches were fixed. |
| `GetHoraYaku` overloads | same | `F` | Base yaku, state yaku, dora, chips, and grade-game `bRevaluate=false` match legacy. |
| `EvaluateTrainingAiHoraPoints` | legacy `CEval::GetValSim` score leaf | `F/A` | Candidate shape uses simulated counts while dora uses the legacy original-hand arrays: all 14 original tiles for tsumo, and the original first 13 plus the candidate tile for ron. First-turn yaku are disabled as in `temp.m_b1stR=false`. |
| `EvaluateWaitGuide` | none | `X` | Current assist feature based on official wait-guide requirements. |
| `InitKyoku` | same | `E` | Dice, dealer, wareme, deal, first draw, indicators, and flags match. |
| `EnterFuroMode` | same | `E` | Opens discard, sets current action, modes, and hora forms. |
| `ClearAllIppatsu` | `ClearAllPlayerIppatsu` | `E` | Equivalent. |
| `ProcessKan` | same | `E` | Rinshan, indicators, and four/five-kan abort logic match legacy. |
| `CheckHoraYaku` | same | `E` | Shape and state-yaku shortcut then ordinary-yaku validation match. |
| `ProcessHora` | same | `E` | Winner order, first-winner bonus ownership, and renchan match AP-15. |
| `ProcessHoraPlayer` | same | `E/T` | Ron/tsumo, pao then wareme, sticks, honba, and chips match. Exact combined settlement fixtures remain incomplete. |
| `ProcessEndKyoku` | same | `E` | Statistics, reveal, result state, and Kyo modes match. |
| `ProcessEndHanchan` | same | `E/A` | Ranking, uma, oka/return, yakitori, tips, and result records are ported; current fixes player-order assignment. |
| `ProcessRyuukyoku` | same | `E/T` | Nagashi, 3,000-point noten penalty, renchan, and records match; all tenpai-count combinations need direct tests. |
| `ProcessPinchui` | same | `E/X` | Abortive-draw renchan matches; current result snapshot is an extension. |
| `MapKyoResultPin` | inline legacy result mapping | `A` | Structural extraction. |
| `CheckTobiAndRecord` | repeated legacy blocks | `A` | Structural extraction. |
| `CountDora` | same | `E/D` | Indicator, ura, red, and chip stacking match legacy. Contest exceptions are retained pending authoritative contest rules. |

## Training AI

Legacy sources: `HgMajak2/Eval.h`, `Eval.cpp`, `Eval1.cpp`, `Eval2.cpp`, `Eval3.cpp`, and `MJTblUser3.cpp::ComTurn`. Current source: `server/Engine/TrainingAiEvaluator.cs`.

| Current function | Legacy function | Status | Result |
|---|---|---:|---|
| `LegacyTrainingAiEvaluator.Evaluate` | `CMJTblUser::ComTurn` plus `CEval` construction | `E/F` | State conversion, TSU/riichi bypass/RON priorities, decision mapping, and sorted-chi called-tile accounting match. |
| `LegacyEvaluator` constructor | `CEval::CEval` | `E` | Constants, four parameter rows, arrays, and initial counts match. |
| `Evaluate` / `ApplyRandomization` | `CEval::Evaluate` inline randomization | `E/A` | Equivalent. |
| `GetMntCnt` | same | `E` | Equivalent recursion entry. |
| `ChkMntCnt` overloads / `ChkMntCntSub` | same | `E` | Equivalent mentsu search. |
| `GetJntCnt` / `ChkJntCnt` / `ChkJntCntSub` | same | `E` | Equivalent pair/taatsu search. |
| recursive and wrapper `GetShanten` | same | `E` | Equivalent. |
| recursive and wrapper `ChkShanten` | same | `E` | Equivalent. |
| `GetValSim` | same | `E/F` | Recursive draw/discard simulation matches; the delegated score leaf now preserves legacy original-hand dora accounting and riichi eligibility. |
| `IsRelevantDraw` | repeated legacy filters | `A` | Extracted predicate. |
| `GetValPai` / `GetValJntPai` | same | `E` | Equivalent. |
| `IsAdjacentToHand` | repeated legacy filters | `A` | Extracted predicate. |
| `GetValTatSub` / `GetValJntSub` | same | `E` | Equivalent. |
| `GetValMntMul` / `GetValTatMul` / `GetValMen` | same | `E/A` | Equivalent; unused legacy parameters were removed. Zero-initialized C# arrays stabilize undefined legacy memory. |
| none | declared `ChkJntCnt(int)` | `N/A` | Declared but never defined or called in legacy. |

## AdvancedTrainingAiEvaluator

There is no legacy counterpart. The whole class is an optional current extension selected by `GameSettings:TrainingAiLevel=Advanced`.

| Current function | Status | Result |
|---|---:|---|
| `Evaluate` | `X/F` | Candidate ranking extension; called-chi visibility was fixed. |
| `IsBetter` / `SelectPhysicalTile` | `X` | Stable tie break and red-tile preservation. |
| `BuildCounts` / `BuildRemainingCounts` | `X/F` | Remaining visible count now excludes exactly the called discard. |
| `CalculateUkeire` / `GetWaitQuality` | `X/T` | Live wait and shape scoring; special-hand fixtures remain sparse. |
| `CalculateValueLoss` / `CalculateDanger` | `X/T` | Dora/value retention and genbutsu/suji defense. |
| `ShouldDeclareRiichi` | `X/T` | Current strategic threshold extension. |
| `CalculateShanten` / `CalculateNormalShanten` / `SearchNormal` | `X/T` | Normal-hand shanten recursion. |
| `CalculateChiitoitsuShanten` / `CalculateKokushiShanten` | `X/T` | Special-hand shanten extensions. |

## Definitions and helper models

| Current function/model | Legacy counterpart | Status | Result |
|---|---|---:|---|
| `KyoResult.Clear` | result struct reset blocks | `A` | Equivalent reset. |
| `BipaiInfo.Create` / `RatingRecord.CreateEmpty` | zero-initialized C++ structs | `A` | Ensures required arrays exist. |
| `Mentsu.IsKan` / `IsKou` / `IsShu` / `IsGreen` | same | `E` | Equivalent. |
| `FuroBlock.IsKan` / `IsKou` / `IsShu` / `IsGreen` | `FURO_ST` plus helpers | `E/A` | Equivalent. Internal `CalledBipaiIndex` preserves legacy side-tile position semantics without changing the protocol. |
| `ValidActions` | check-only return behavior | `X/A` | Structured current response for UI and proxy play. |

## Remaining structural adaptations

1. `Bipai.Story` is absent. It was a `_TESTVER` file loader; current deterministic wall injection preserves the behavior through `SetBipai` rather than the legacy local-file format.
2. Legacy `InitHanchan` and `InitKyoku` divide a few defensive resets differently. The owning public lifecycle reaches the same state.
3. `EvaluateWaitGuide` and `AdvancedTrainingAiEvaluator` are current-only optional features and therefore have no legacy behavior to select.

## Highest-value remaining tests

- Double-ron honba/stick ownership and dealer renchan ownership.
- Ordinary rounded ron/tsumo payments, compound yakuman, and yakuman chips.
- Training AI `aiType` 1-3, furiten suppression, open-hand shanten, and advanced special-hand choices.

## Validation performed

Focused legacy-priority regressions cover kuikae acceptance, retained chakan state, grade-game single yakuman, invalid sentinel arithmetic, multi-ron modes, exhaustive draw distributions, pao/wareme settlement, kan boundaries, all-last ranking, rare yaku, exact fu classes, and training-AI original-hand dora scoring. The final Hand selection passed 75/75 tests and the legacy/advanced training-AI selection passed 14/14 tests. The broad engine selection passed 436 tests; its two failures are pre-existing `GameLogicService` end-processing tests outside the changed engine paths. The complete server suite finished with 1,626 passed and the same 32 pre-existing service/protocol failures present before this verification campaign; no new engine or parity regression failed.
