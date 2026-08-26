# Game Mode Validation Matrix

This document tracks game-mode verification in execution order. A mode is not
considered complete until every required scenario passes or an approved
exception is documented.

## Status Values

- `Planned`: scenario is specified but has not been run.
- `Passed`: automated or reproducible manual verification passed.
- `Failed`: observed behavior differs from the expected result.
- `Blocked`: verification cannot proceed because an environment or contract is missing.

## Mode 01: Training Lobby (`00T5A`)

### Contract

- Empty seats are represented and played by training NPCs.
- Training NPCs use the room-selected legacy or advanced AI policy; new rooms default to advanced.
- Advanced NPCs may chi or pon only when one-step discard lookahead strictly improves shanten.
- Human players receive a prompt-scoped blinking discard recommendation in training games.
- Training matches do not change GP, dragon gems, rating, rank, ordinary match records, titles, or result missions.
- Training matches do not create normal history, training history, or replay-paifu archive records.
- Training matches still send valid game-start, action, and game-result packets to connected human players.

### Scenarios

| ID | Area | Expected result | Existing automated coverage | Status |
|---|---|---|---|---|
| TRN-01 | Channel classification | `00T5A` sets `IsTrainingChannel`. | `GameRoom` channel predicate | Planned |
| TRN-02 | Solo/two-player start | Game starts with empty seats after the client-ready sequence. | `StartGameLogic_*Training*` | Planned |
| TRN-03 | NPC turn | Empty-seat NPC makes a legal discard and the next turn continues. | `ProxyEmptySeats_TrainingEmptyDealer_AutoDiscardsAndAdvancesTurn` | Planned |
| TRN-04 | NPC policy | Room-selected Legacy/Advanced changes NPC behavior only for that training room. | `TrainingRoomAiLevelPayloadTests`, `AdvancedTrainingAiEvaluatorTests` | Implemented; test execution blocked by existing test-project compile errors |
| TRN-05 | Result payload | Empty engine seats appear as NPC result rows, without player payouts. | `MakeGameReport_TrainingEmptyEngineSeats_AddsNpcRows` | Planned |
| TRN-06 | Economy/rating | GP, gems, rating, rank, ordinary records, titles, and result missions remain unchanged. | Result-mission coverage exists; remaining assertions required | Planned |
| TRN-07 | Persistence | No normal history, training history, or replay-paifu archive is written. | Paifu path guarded; existing training-history expectation conflicts | Planned |
| TRN-08 | Idempotency | Repeated result processing does not create any persistence or duplicate client result. | New coverage required | Planned |
| TRN-09 | Recovery | Disconnect/reconnect preserves the human seat and NPC mapping until game end. | New training-specific coverage required | Planned |

### Exit Criteria

All scenarios `TRN-01` through `TRN-09` are `Passed`. Any expected policy
change requires a documented contract update before the next game mode begins.

## Mode 02: Standard Exchange Lobbies (`0086B`, `0082B`)

### Contract

- `0086B` is the seeded basic east-only lobby; `0082B` is the seeded basic
	hanchan lobby with kuitan enabled.
- Both use `unit_money=20`, complete with four connected players, persist one
	normal game history, and update all four player result records.
- Neither lobby writes training history.

### Scenarios

| ID | Area | Expected result | Automated coverage | Status |
|---|---|---|---|---|
| EXC-01 | East-only play | `0086B` applies east-only rules and completes a random full game. | `StandardExchangeLobby_RandomGameCompletesAndPersistsResults` | Passed |
| EXC-02 | Hanchan play | `0082B` applies hanchan/kuitan rules and completes a random full game. | `StandardExchangeLobby_RandomGameCompletesAndPersistsResults` | Passed |
| EXC-03 | Settlement and history | Each completed normal game succeeds, stores one normal history, updates four results, and stores no training history. | `StandardExchangeLobby_RandomGameCompletesAndPersistsResults` | Passed |

### Verification

The parameterized simulation was run three times after build, covering six
randomly dealt games in total. Every run passed.

## Mode 03: High-Stakes Exchange Lobby (`0085F`)

### Contract

- The seeded high-stakes lobby uses `unit_money=100`.
- A completed four-player match writes one normal game history and updates the
	common and hi-class result records for every player.
- Every settlement amount is divisible by 100 GP.

### Scenarios

| ID | Area | Expected result | Automated coverage | Status |
|---|---|---|---|---|
| HIG-01 | Random full game | Four connected players complete a randomly dealt game through the server action protocol. | `HighStakeExchangeLobby_RandomGameUsesHundredGpSettlementAndHiClassResults` | Passed |
| HIG-02 | High-stakes settlement | Normal history and four common/hi-class updates occur; each hi-class money change is a multiple of 100 GP. | `HighStakeExchangeLobby_RandomGameUsesHundredGpSettlementAndHiClassResults` | Passed |

### Verification

The randomized high-stakes simulation was run four times in total. Every run
passed.

## Mode 04: Wareme Exchange Lobby (`0075B`)

### Contract

- `RoomOption[10] == '1'` enables the wareme rule.
- The dice-derived wareme seat has its win, discard, and tsumo score transfers
	doubled; honba, noten, and nagashi payments are not doubled.
- A completed four-player match uses normal game history and standard result
	settlement at the seeded 20 GP unit.

### Scenarios

| ID | Area | Expected result | Automated coverage | Status |
|---|---|---|---|---|
| WAR-01 | Rule and random full game | `0075B` parses wareme from its room option and completes a randomly dealt hanchan through the server action protocol. | `WaremeExchangeLobby_UsesWaremeRuleAndCompletesRandomGame` | Passed |
| WAR-02 | Wareme score adjustment | Wareme-adjusted player and result-record points include the doubled transfer. | `ProcessHoraPlayer_WaremeRecordPoints_UseAdjustedHoraPoints` | Passed |
| WAR-03 | Initial kyoku protocol | `waremeOdr` is sent only when the wareme rule is enabled. | `OnInitKyoku_WaremeOdr_FollowsWaremeRule` | Passed |
| WAR-04 | Settlement and history | The completed random match writes one normal history and updates all four player results at 20 GP increments. | `WaremeExchangeLobby_UsesWaremeRuleAndCompletesRandomGame` | Passed |

### Verification

The combined wareme suite was run three times after build. Each run covered a
random full game, adjusted scoring, and both enabled/disabled initial-kyoku
packet states; all 12 test executions passed.

## Mode 05: Ranked East-Only Lobbies (`0ZG6A` through `0ZG6D`)

### Contract

- Grade-mode channels use fixed east-only rules: kuitan enabled, two red fives,
	and no yakitori, wareme, nagashi, or chips.
- Entry is restricted by both GradeLevel and GP: A 0--12 / 500 GP, B 10--18 /
	5,000 GP, C 13--18 / 10,000 GP, and D 16--18 / 30,000 GP.
- A completed ranked game writes normal history, common results, and four
	grade-mode result updates.

### Scenarios

| ID | Area | Expected result | Automated coverage | Status |
|---|---|---|---|---|
| RKE-01 | Entry boundaries | Each lobby accepts its minimum qualifying GradeLevel and GP, and rejects the adjacent lower GP or grade boundary. | `CheckEnterGradeMode_UsesOfficialRoomBoundaries` | Passed |
| RKE-02 | Fixed game rules | Grade-mode rule resolution is east-only with kuitan and two red fives. | `BuildRuleInfo_GradeMode_SetsGradeRules` | Passed |
| RKE-03 | Random full games | Four qualifying players in each of `0ZG6A`--`0ZG6D` complete a randomly dealt game through the server action protocol. | `RankedEastLobby_RandomGameCompletesAndUpdatesGradeResults` | Passed |
| RKE-04 | Grade settlement | Each completed match writes one normal history, four common-result updates, and four grade-result updates. | `RankedEastLobby_RandomGameCompletesAndUpdatesGradeResults` | Passed |

### Verification

The combined ranked-east suite was run three times after build. Each run
executed 19 tests: four random full games, 14 entry-boundary cases, and one
fixed-rule case. All 57 test executions passed.

## Mode 06: Ranked Hanchan Lobbies (`0ZG7A` through `0ZG7D`)

### Contract

- Grade-mode channels use the fixed hanchan rule when `subId[3] == '7'`, with
	kuitan enabled, two red fives, and no yakitori, wareme, nagashi, or chips.
- The final SubID character controls the same GradeLevel and GP entry boundary
	as the ranked east lobbies: A 0--12 / 500 GP, B 10--18 / 5,000 GP, C 13--18
	/ 10,000 GP, and D 16--18 / 30,000 GP.
- A completed ranked hanchan writes normal history, common results, and four
	grade-mode result updates.

### Scenarios

| ID | Area | Expected result | Automated coverage | Status |
|---|---|---|---|---|
| RKH-01 | Entry boundaries | Each hanchan lobby accepts its minimum qualifying GradeLevel and GP, and rejects the adjacent lower GP or grade boundary. | `RankedHanchanLobby_EntryBoundaries_AreEnforced` | Passed |
| RKH-02 | Fixed game rules | Grade-mode rule resolution creates a hanchan whenever `subId[3]` is `7`. | `BuildRuleInfo_GradeHanchanSubId7_SetsHanchan` | Passed |
| RKH-03 | Random full games | Four qualifying players in each of `0ZG7A`--`0ZG7D` complete a randomly dealt hanchan through the server action protocol. | `RankedHanchanLobby_RandomGameCompletesAndUpdatesGradeResults` | Passed |
| RKH-04 | Grade settlement | Each completed match writes one normal history, four common-result updates, and four grade-result updates. | `RankedHanchanLobby_RandomGameCompletesAndUpdatesGradeResults` | Passed |

### Verification

The combined ranked-hanchan suite was run three times after build. Each run
executed 17 tests: four random full games, 12 entry-boundary cases, and one
fixed-rule case. All 51 test executions passed.

## Mode 07: Custom Exchange Rooms and Rule Combinations

### Contract

- Custom normal rooms derive their engine rule solely from `RoomOption`.
- The supported custom dimensions are game length, kuitan, aka dora, yakitori,
	wareme, chips, ron mode, and uma.
- A configured custom rule must remain active through randomized server-action
	play and normal-game settlement.

### Scenarios

| ID | Area | Expected result | Automated coverage | Status |
|---|---|---|---|---|
| CUS-01 | East-only defensive rules | An east-only room with kuitan disabled, yakitori enabled, one red five, and uma mode 2 completes a random game. | `CustomExchangeRoom_RandomGameHonorsConfiguredRules` | Passed |
| CUS-02 | Full custom rules | A hanchan with kuitan, two red fives, yakitori, wareme, chips, ron mode 2, and uma mode 2 completes a random game. | `CustomExchangeRoom_RandomGameHonorsConfiguredRules` | Passed |
| CUS-03 | Alternative hanchan rules | A hanchan with kuitan disabled, ron mode 1, and uma mode 3 completes a random game. | `CustomExchangeRoom_RandomGameHonorsConfiguredRules` | Passed |
| CUS-04 | Option parser basics | `RoomOption[0]` selects hanchan/east-only play and `RoomOption[3]` enables kuitan only when set to `0`. | `BuildRuleInfo_RoomOptionHanchan_SetsHanchan`, `BuildRuleInfo_RoomOptionTonpu_NotHanchan`, `BuildRuleInfo_Kuitan_WhenOpt3Is0` | Passed |
| CUS-05 | Settlement and history | Every custom random game writes one normal history and four common result updates; kuitan-off games preserve their configured rule. | `CustomExchangeRoom_RandomGameHonorsConfiguredRules` | Passed |

### Verification

The custom-rule suite was run three times after build. Each run executed three
random full games and three option-parser checks. All 18 test executions
passed.

## Mode 08: Automatic Matching

### Contract

- Only auto-matching channels accept `mjkc2e` queue requests; GP, beginner,
	grade, and cup eligibility are checked before enqueueing.
- Queues are isolated by channel. Four compatible players form a reserved
	match unless their pre-match exclusion lists prohibit the combination.
- A reserved room starts only after all expected players confirm `mjkc6e`; an
	incomplete reservation is eligible for FAILEROOM expiry.
- Automatic room options select east-only or hanchan settings from the channel
	game-type character.

### Scenarios

| ID | Area | Expected result | Automated coverage | Status |
|---|---|---|---|---|
| AUT-01 | Eligibility and cancellation | Valid automatic-channel requests enqueue; insufficient GP, beginner, and cup restrictions reject; cancellation removes a queued player. | `AutoMatchingCommandTests`, `CancelAutoMatchingCommandTests` | Passed |
| AUT-02 | Queue isolation and rematch exclusion | Separate channel queues produce only their own four-player match, while mutual pre-match exclusions prevent a repeat combination. | `TryMatch_SeparatesChannelsAndHonorsPreMatchExclusions` | Passed |
| AUT-03 | Reservation lifecycle | Three confirmations do not complete a reservation; the fourth does, while an incomplete reservation is removed on expiry. | `PendingMatch_RequiresAllFourEntriesAndExpiresIncompleteReservation` | Passed |
| AUT-04 | Automatic room options | Grade and normal auto-matching channels resolve their east-only and hanchan room options from the game-type character. | `ResolveAutoRoomOption_UsesChannelGameType` | Passed |

### Verification

The automatic-matching suite was run three times after build. Each run
executed 16 command and lifecycle tests; all 48 test executions passed.

## Mode 09: Tournament Mode

### Contract

- Tournament registration and joining validate rule format, schedule, capacity,
	password, GP, and cancellation time limits.
- When the join count exceeds half of a bracket, pre-matching creates a WAIT
	bracket and fills remaining seats with NPC placeholders.
- Match start reserves the tournament room and sends each connected participant
	the public `pix` identifier, never the internal member number.
- Two-play finals rank participants by total result, then the legacy tie-break
	order encoded in the tournament result key.

### Scenarios

| ID | Area | Expected result | Automated coverage | Status |
|---|---|---|---|---|
| TRN-01 | Registration and participation | Invalid schedules, rules, capacity, GP, passwords, duplicate joins, and late cancellations are rejected; valid plans and joins are accepted. | `TournamentServiceLogicTests`, `TournamentTablesTests` | Passed |
| TRN-02 | Insufficient or complete brackets | Under-half participation rejects a plan, while four participants create a WAIT bracket. | `PreMatchingAsync_NotEnoughPlayers_StatusReject`, `PreMatchingAsync_EnoughPlayers_StatusWait` | Passed |
| TRN-03 | NPC bracket | Three participants in a four-player tournament create one WAIT detail with exactly one NPC seat. | `PreMatchingAsync_ThreePlayers_FillsTheBracketWithOneNpc` | Passed |
| TRN-04 | Match start and disconnects | Ready tournament details reserve a room, notify present participants, and mark absent participants as exited. | `GoMatchingAsync_StartTimeReached_StatusPlay`, `GoMatchingAsync_MissingLobbyMember_MarksJoinExitAndUsesPresentPlayers`, `NotifyMatchStartAsync_SendsLegacyAutoMatchingPayload` | Passed |
| TRN-05 | Results and progression | Two-play final ranking honors aggregate score and the legacy tie-break ordering; all-finished plans progress to END. | `SetTournamentResultRank_TwoPlayFinal_UsesTotalThenBestRound`, `PostMatchingAsync_AllFinished_StatusEnd` | Passed |

### Verification

The focused tournament suite was run three times after build. Each run executed
50 registration, bracket, match-start, result, and table tests. All 150 test
executions passed.

## Planned Mode Order

1. Training lobby: `00T5A`
2. Standard exchange lobbies: `0082B`, `0086B`
3. High-stakes exchange lobby: `0085F`
4. Wareme exchange lobby: `0075B`
5. Ranked east-only lobbies: `0ZG6A` through `0ZG6D`
6. Ranked hanchan lobbies: `0ZG7A` through `0ZG7D`
7. Custom exchange rooms and rule combinations
8. Auto matching
9. Tournament mode