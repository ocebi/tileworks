# Falling board: diagonal slides, play during falls, a skilled tuning bot

> Owner: the Advisor session (architecture, task board, reviews). Implementation: Developer-1, Developer-2,
> Level Designer, UX Designer. Status lines live at the bottom; each owner updates their own rows.
> Written 17 Oct 2026. Studio names stay out of this file.

## 1. What the user asked for
1. **Diagonal slides in Blast and Gem Swap.** Pieces fall straight; when a piece can't fall straight and a cell
   diagonally below it (left or right) is empty and nothing can reach that cell from above, the piece slides into it.
   Never sideways, only to a cell below itself. Levels are designed around this.
2. **Harder levels, tuned for a skilled player.** The casual bot (25% random, one-ply score) undervalues special
   creation, so a player who builds specials wins easily. Build a skilled bot and retune the 100 levels of each game.
3. **Play while pieces fall.** A tap (Blast) or swap (Gem Swap) on pieces that have landed is accepted while other
   pieces are still falling. The rules handle it, not the view. Tests focus on this.

## 2. Decisions (settled unless the user overrides)
- **D1. Holes block falls.** A non-playable cell ends a column segment, as it does in the genre's leading games. Pieces
  no longer pass holes. Only the top segment of a column refills from above; everything under a hole is fed by slides.
  Every playable cell of a shipped level must be reachable by falls and slides (a validator enforces it).
- **D2. One gravity, in the Tiles package.** The fall / spawn / slide loop with leg recording moves from Gem Swap's
  `SettleSystem` into `Tileworks.Tiles.Model` as a generic `Gravity` over a small grid interface. Both games call it.
  This is the first step of ADR 0021's shared board without a wholesale extraction while the tree holds uncommitted work.
- **D3. Pieces in flight are rules state.** Gravity marks every moved or spawned piece *in flight* at its destination.
  The view reports each landing as a `Land` action; the model excludes in-flight pieces from groups, matches, swaps,
  hints, shuffles and end-of-level decisions. Bots and replays use `LandAll`. Replays stay exact because landings are
  recorded actions. (The alternative, model-side timed falls, rewrites every presenter and motion test; rejected for now.)
- **D4. Falls preempt falls.** A piece that must fall again while still in the air continues from where it is; no landing
  in between. This is an event-player rule (same-type preemption), not presenter guesswork.
- **D5. Specials still lock the board** for their effect's duration (rocket crossing, bomb ring, disco, combos, shuffle,
  boosters). Only plain falls are playable through.
- **D6. Blast boxes are static** (user decision, 17 Oct), like Gem Swap's boxes and chains and like the reference
  games' crates. A box ends a column segment; the cells under it are fed by slides. A box on a column's top cell blocks
  that column's refill until the box clears. Boxes never emit fall events.
- **D7. Tuning profile.** Levels are tuned on the skilled profile against a second `LevelCurve` table
  (`LevelCurve.For(profile)`; the casual table stays for Block Puzzle and Tile Match). Skilled targets, decided 17 Oct
  from the Level Designer's measurement (today's levels: skilled 1.00 / 0.90 / 0.63 / 0.32 by role): easy and breathers
  0.97 (levels 1-4 keep casual ≥ 0.8); normal 0.82 at chapter 2 falling 0.015 a chapter to about 0.70; hard 0.45 falling
  to 0.38; super hard 0.22; tolerance 0.07; floors 0.9 / 0.6 / 0.3 / 0.15. Easy, normal and hard levels keep a casual
  rate ≥ 0.02 (a content test); super hard levels are governed by the skilled band alone, since the skilled floor already
  proves them winnable and tent-poles are meant to be nearly closed to weak players. Both rates go in `report.csv`.
  Golden replays keep the casual bot's playouts.
  Changing the table and regenerating takes a few minutes per game (skilled playouts cost about 8× casual ones), so
  the user can shift difficulty after play-testing. The content builders construct the generators with the skilled
  profile; `report.csv` carries `casualWinRate` and `skilledWinRate`; the Levels window flags unreachable cells.

## 3. Rules specification

### 3.1 Gravity (`Tileworks.Tiles.Model.Gravity`, generic)
Grid interface (implemented by `BlastState` and `Match3State`): `Width`, `Height`, `IsPlayable(cell)`,
`IsStatic(cell)` (Gem Swap: box or chained; Blast: box), `IsEmpty(cell)`, `CanMove(cell)`, `Move(from, to)`,
`Spawn(cell, drop)` (the game picks the colour from its refill stream), and a leg sink.

Loop until nothing moves:
1. **Fall.** Per column, bottom-up: a movable piece drops to the lowest empty cell below it in the same *segment*. A
   segment is a run of playable cells bounded by holes, static pieces or the board edge.
2. **Spawn.** Only the top segment of each column refills: its empty run at the top fills from the stream, `Drop`
   counting rows above the top playable cell. A hole under the top cell makes a one-cell top segment.
3. **Slide.** Rows bottom-up, cells left to right: an empty cell that is *not fed* (walking up its column the first
   thing met is a hole or a static piece, not a movable piece or the board top) takes a *resting* movable piece from
   up-left or up-right. Resting = the cell under the source is not empty. Side preference: by `(x + y)` parity, then the
   other side (symmetric on mirrored boards; one constant, so it can be changed).
4. **Legs.** Each piece's path is recorded; straight runs merge; `Lands` is set on the last leg only (Gem Swap's
   `SettleStep` moves to Tiles). Every moved or spawned piece is marked in flight at its destination.

`Gravity.Reachable(mask, statics)`: the cells a piece can reach from the spawn cells (top playable cell of each column
that isn't static) by falls and slides, treating holes and the given static cells as walls. Checked twice per level:
over the bare shape, every playable cell must be reachable; with the level's static blockers in place, every
non-static playable cell must be reachable (so no column is sealed by boxes with nothing to open them). Generation and
the Levels window reject levels that fail either.

### 3.2 Flight and landing (both games)
- State: `InFlight[cell]`, `FlightCount`; part of `CopyTo` and `ComputeHash`.
- `Land(cell, id)`: valid when the cell holds that piece and it is in flight (otherwise reject `NotInFlight`; the session
  drops such rejections silently). Apply clears the flag, then the pipeline resolves as after any action.
- `LandAll()`: lands in-flight pieces one at a time in canonical order (lowest row first, then left to right), resolving
  between landings so matches form as they would one by one. A system that lands one piece per pass does it naturally;
  raise `RuleEngine.MaxResolutionPasses` if a 9×10 board with cascades needs more than 64.
- Neither action costs a move. Both are recorded (codecs), so replays are exact. `RulesVersion` goes up in both games.
- `ISearchDomain.Apply` for bots: the move, then `LandAll`. Golden replays are rebaselined.
- Blast: `FindGroup` skips in-flight cells (a group is landed cubes only; a tap whose landed group is under 2 is
  rejected `GroupTooSmall`). `HasMove`, shuffle and hints run only at `FlightCount == 0`. A duck is collected when it
  *lands* on its column's lowest playable cell (not during settle).
- Gem Swap: `CanSwap` is false for in-flight cells; `MakesMatch` / `MatchAt` / `HasMove` ignore in-flight pieces. After
  a clear or special, `MatchSystem` clears matches among landed pieces only. A `Land` runs match detection for lines
  through the landed cell; the special it makes sits on the landed piece (`Landed` clock, as today). The cascade counter
  keeps counting across `Land` actions until the board settles without a clear; it resets on the next player move.
- End conditions: with `FlightCount > 0`, no loss is declared (the last landing may still win). A win can come from a
  `Land`. The loss is decided by the `Land` that settles the board.
- Swaps don't put pieces in flight; the view's lane check (3.3) covers their animation.

### 3.3 Session, player, presenters, input
- `IEventPlayer`: `bool IsBusy(int entity)`; `EventTypeRegistry.Register(..., preempts: true)` for `PieceFell`. A
  preempting event whose entity lane is occupied by an event of a preempting type starts at `max(now, previous step
  end)` and overwrites the lane; otherwise lanes behave as today. The player therefore remembers which type holds a lane.
- Falls move by speed, not by tween (decided 17 Oct from the UX note `docs/FALL-MOTION.md` §2): a shared `FallMotion`
  (start speed, acceleration, top speed, slide speeds; replaces `FallCurve`, also for the drop-in) and a per-frame
  `FallMover` in Tiles View, driven by the presentation clock, with a leg queue per piece. A fall extended mid-air keeps
  its position and speed; a falling piece never comes closer than one cell to the piece below it (follow rule). The
  presenter still returns the leg's planned duration to the event player; the follow rule may delay the real arrival a
  little, so the session's watchdog (below) waits 0.5 s of idleness before it steps in. Squash (scaled by impact speed),
  land cue and the landing report only on a leg with `Lands`. On completion of a landing leg the view reports
  `(id, cell)` through a handler interface (no `Action` fields in runtime code), the controller calls
  `Session.Land(cell, id)`.
- Column stagger is measured from the action's origin column (`FallColumnStagger · |column − origin|`): the tapped
  cell, the swap's destination, the special's or hammer's cell, the landed cell for a landing-triggered clear, the board
  centre for a shuffle. The controller hands the origin to the board view before it submits the action (a view method,
  not a scene search or a delegate field). Extended legs take no stagger.
- Session: `IsSettled => player.IsIdle && State.FlightCount == 0`; `CanActOn(cell) => !InFlight[cell] &&
  !player.IsBusy(Ids[cell])`. The effect hold (`EffectsBusy`, the presenter kit's hold that specials, combos, flyers,
  hammer and shuffle set; falls don't) lives in the view, so the game presenter checks it first on every input path:
  during a hold a drag never becomes a swap, a tap never selects, activates or blasts, nothing is played or buffered,
  and a buffered touch is dropped if a hold starts before it goes through (as built in Gem Swap; Blast matches). Watchdog in `Tick`: `player.IsIdle && FlightCount > 0` → `LandAll` and a
  warning (the view missed a landing), after 0.5 s of idleness, since the follow rule can delay a landing past the
  lane's planned end.
- Controllers / gestures: taps and swaps check `CanActOn` on the touched cell(s); boosters, hint and the booster bar keep
  `IsSettled`.
- Tap buffer (controller only, never the rules; `TapBuffer` in the Feel config, `Range(0, 0.3)`, default 0.15 s): a
  tap or swipe refused because a touched cell is in flight, or a Blast `GroupTooSmall` while a neighbour of the group is
  in flight, is held and retried on every landing report until it is accepted or the buffer expires. On expiry, a cell
  that has landed and is still refused gets the usual refuse feedback; one still in flight is dropped silently. Nothing
  is buffered while a special effect holds the board.
- The 15 Oct spam bug (phantom clears, stacked tiles) is prevented by construction: the model never acts on a piece the
  player hasn't seen land.

### 3.4 Skilled bot and tuning (`Tileworks.Tiles.Search`, both games' search domains, generators)
- `BotProfile`: Casual (ε 25%, one ply, today's `Score`) and Skilled (ε 5%, `LookaheadPolicy`).
- `LookaheadPolicy<TState,TMove>`: for the top-N moves by immediate score, apply on a scratch state and add the value of
  the resulting position (`IPositionEvaluator<TState>`) plus a share of the best follow-up move's immediate score.
  Deterministic given the seed. Budget: N ≤ 6, depth 1.
- Evaluators (per game, in the search domain file): specials on the board (disco > bomb > flyer/rocket), adjacent specials
  (combo setups), largest group / open four-in-a-row setups, goal left relative to moves left, blockers adjacent to
  movable pieces. Immediate scores value special *creation* properly: Blast group ≥ 5 / 7 / 9; Gem Swap 4-line, L/T,
  2×2, 5-line.
- Checks: on every shipped level the skilled bot's win rate ≥ the casual bot's; on a sandbox level it creates more
  specials per game.
- Tuning: skilled drives the move limit / blocker count / colour count against `LevelCurve`; `report.csv` carries both
  rates; levels 1-4 keep a casual rate ≥ 0.8 (tutorial levels stay winnable for anyone).

### 3.5 Levels for slides
- New mirrored shapes with overhangs and pockets (every playable cell reachable): e.g. chimney, cave, bridge, steps,
  goblet, keyhole, twin towers, zigzag. Existing shapes with mid-column holes (Window, Pillars, Hourglass, Plus) already
  make pockets under D1.
- Teach the slide early on an easy level (Blast 2 or 3, Gem Swap 2) with one obvious pocket; 2-3 slide-heavy shapes
  per chapter; hard levels combine pockets with static blockers (Gem Swap) and goals under overhangs.
- Re-plan and retune all 100 levels per game on the new gravity, update `docs/LEVEL-DESIGN.md` "Where it stands".

## 4. Tests the user asked for (in flight) and the rest
Rules, EditMode, no scenes (`BlastRulesTests`, `Match3RulesTests`, new `*FlightTests`):
1. A tap leaves pieces in flight; a group with one in-flight cube counts landed cubes only; a 2-group with one in flight
   is rejected; after `Land` it is accepted.
2. Tapping a landed group under in-flight pieces makes them fall again: new legs with the same ids, still in flight,
   exactly one landing each.
3. `LandAll` equals the same landings in canonical order (same hash and events).
4. Gem Swap: swap during flight only between landed pieces with a match among landed pieces; a landing completing a line
   clears on that `Land`; cascade count carries across lands; the special sits on the landed piece.
5. Moves 0 with pieces in flight → `Ongoing`; the cascade from the last landing wins → `Won`; otherwise `Lost` on settle.
6. Fuzz: random playouts interleaving moves with random subsets of landings; invariants: no piece lands twice, a
   landed piece never lands again without moving, replay reproduces the hash.
7. `Land` and `LandAll` allocate 0 B after warm-up (as taps do).
8. Dead-board shuffle and hints never run while anything is in flight.
Gravity (Tiles tests, ASCII boards): holes block; top segment only refills; pocket fed by slide; side preference;
falls before slides; legs merge; `Reachable` on every planner shape and size.
Motion (`*BoardMotionTests` with `BoardFilm`): every in-flight piece lands once; `IsSettled` flips on the last landing;
a mid-fall tap on a landed group clears before the other columns land; a preempted fall has no squash or cue
mid-way and ends on its final cell.
PlayMode flow: the spam check from 15 Oct, now with taps mid-cascade accepted on landed pieces only.

## 5. Task board
Owners own paths; nobody edits another owner's paths without a message. Shared files are named explicitly.

| # | Task | Owner | Paths | Depends on |
|---|---|---|---|---|
| A1 | `Gravity` + grid interface + legs + `Reachable` in Tiles.Model, ASCII tests | Developer-1 | `Assets/Tileworks/Tiles/Runtime/Model/Gravity*.cs`, Tiles tests | — |
| A2 | Flight primitives: `Land`/`LandAll` conventions, `NotInFlight` reason, pass cap | Developer-1 | Tiles.Model (`Rules.cs`, `RuleEngine.cs`) | — |
| A3 | Blast onto A1/A2: settle, flight, taps, ducks, end conditions, search domain, replays, tests | Developer-1 | `Assets/Games/Blast/**` | A1, A2 |
| A4 | Blast session, controller, presenters, gestures per §3.3 and `docs/FALL-MOTION.md`; motion + flow tests; ADR 0022 (gravity) | Developer-1 | `Assets/Games/Blast/**` | B2, B2b |
| B1 | Gem Swap onto A1/A2: settle, flight, matches on land, cascade counter, end conditions, search domain, replays, tests | Developer-2 | `Assets/Games/Match3/**` | A1, A2 (start the flight parts now) |
| B2 | `EventPlayer` preemption + `IsBusy`, tests | Developer-2 | `Assets/Tileworks/Tiles/Runtime/View/EventPlayer.cs`, `Model/BoardEvent.cs` (registry flag), Tiles tests | — |
| B2b | `FallMotion` + per-frame `FallMover` (leg queue, follow rule, planned durations) in Tiles View, replacing `FallCurve` and feeding the drop-in; tests with `BoardFilm` | Developer-2 | `Assets/Tileworks/Tiles/Runtime/View/FallMotion.cs`, `FallMover.cs`, `BoardDropIn.cs`, Tiles tests | B2; spec `docs/FALL-MOTION.md` §2 |
| B3 | Gem Swap session, controller, gestures, presenters per §3.3 and `docs/FALL-MOTION.md`; motion + flow tests; ADR 0023 (flight) | Developer-2 | `Assets/Games/Match3/**` | B1, B2, B2b |
| C1 | `BotProfile`, `LookaheadPolicy`, `IPositionEvaluator`; evaluators in both search domains; bot checks | Level Designer | `Assets/Tileworks/Tiles/Runtime/Search/**`, `BlastSearchDomain.cs`, `Match3SearchDomain.cs`, generators | — |
| C2 | New shapes + reachability in planners and the Levels window; slide teaching levels | Level Designer | `Assets/Tileworks/Tiles/Runtime/Generation/**`, `Assets/Games/*/Generation/**`, `Assets/Tileworks/Tiles/Editor/**` | A1 for `Reachable` |
| C3 | Regenerate and retune 100 levels per game on the new gravity, both rates in `report.csv`; docs; ADR 0024 (tuning profile) | Level Designer | `Assets/Games/*/Content/**`, `docs/LEVEL-DESIGN.md` | A3, B1, C1, C2 |
| D1 | Motion spec: slide pacing in Blast, preempted falls, landing-triggered clears, cascade callout timing, feedback rules for taps in flight; frame/background on pocket shapes | UX Designer | `docs/FALL-MOTION.md`, Feel config fields (names agreed with devs) | — |
| D2 | Play check and Feel tuning once A4/B3 land; screenshots | UX Designer | Feel config assets, `Artifacts/` | A4, B3 |
| D3 | Blast box art redrawn as a fixed block (two layers), glossy style, 128 px; frame seams on pocket shapes checked with the Level Designer | UX Designer | `Assets/Games/Blast/Art/**` | — |
| D4 | Gem Swap frame drawn in two passes like Blast's (gold rim per band, inset fill per band) so pocket shapes have no seams or spurs; two skin colours with builder defaults; renders on all six new shapes | UX Designer (announces to Developer-2; edits only `Match3BoardView.DrawFrame`, `Match3Skin`, builder defaults; rebuild in an agreed window) | `Assets/Games/Match3/View/Match3BoardView.cs`, `Match3Skin.cs`, Match3 builder | — |

Order: A1, A2, B2, C1, C2, D1 start now. A3/B1 as soon as A1 compiles (Developer-1 announces). B2b after B1's
flight parts; A4/B3 after B2b.
C3 last. The Advisor reviews each task's diff on request and arbitrates shared-file edits.

## 6. Working rules for this effort
- Check `EditorApplication.isPlaying` before any compile, refresh, test run or scene rebuild; announce them to the
  other sessions first (SendMessage), and wait during the user's play windows.
- Write code in your own paths only; a shared file (named above) is touched by its listed owner. If you need a change
  there, message the owner (or the Advisor) with the exact edit.
- No `git checkout`, `stash`, `reset` or `clean`. Commit only when the user says so, your own paths only.
- Own Play checks on a scenario profile (`ScenarioFile.Write(... fresh = true)`), `save.sh hold` / `restore`.
- Keep studio names out of the repo; cite "the reference games".

## 6b. Round 2: level feedback from the user's play-test (18 Oct)
The mechanics passed the play-test. Feedback: the slide is wasted on pockets under overhangs; the interesting pens are at
the **bottom**, where gems funnel down and slide outward (a wall funnel narrowing to a chute over a wide bottom), and
**sealed empty pens** behind clearable walls, which start to fill the moment the wall clears. Slides should be introduced
around level 4-5 and used sometimes, not everywhere. Gem Swap 10 was too hard (three dead-board shuffles), Gem Swap 11
too easy (a rocket gift over the box frame, won in two moves), Blast 15 felt right, Blast 17 too easy.

What the report rows say: `m3_0010` = 6 colours + 16 boxes + 12 chains on Plus (cramped, dead boards); `m3_0011` won with
81% of moves left; `bl_0017` = 10 balloons, 17 moves, won with 51% of moves left. The tuner had no goal-size knob and no
cap on moves left on wins or on shuffles.

Decisions:
- **R1. Pens.** A level may contain empty playable cells (`_` in both payloads: playable, no piece). The generator makes
  sealed cells empty instead of unsealing their blockers (`BoardReach.Unseal` goes). A pen must be reachable on the bare
  shape and sealed only by clearable statics (boxes, chains), never by holes alone; it holds no pieces or goals at start.
  Fed empty cells are a validation error (no settle at level start).
- **R2. Pattern library for slides**: bottom funnels (box walls narrowing to a chute over a wide bottom), bottom corner
  pens behind a box column, side pens, pens under a box shelf; mirrored; drawn by the Level Designer from the user's
  sketches. Shapes with holes stay as a secondary source of pockets.
- **R3. Schedule** (user, 18 Oct: this is a demo and most players play only five levels, so the slide comes early).
  Slide intro at level 2 in both games, kept easy: one visible bottom pen of 2-4 cells behind a single-hit wall that
  touches a group the player clears in the first 2-3 moves, nothing else new on the board. Levels 3-5 stay easy and may
  reuse a simple pen. From chapter 2 on, two pocket/pen levels per chapter (the hard 5th and one varying slot).
- **R4. Tuning knobs, in order:** goal size, then moves, then blockers, then colours. New acceptance rules on the skilled
  profile: moves left on wins ≤ 0.50 easy / 0.35 normal / 0.25 hard; mean shuffles per game ≤ 0.15 (≤ 0.3 on hard and
  super hard); no sixth colour before chapter 3; on levels under 30 at least 60% of playable cells are movable pieces.
  `report.csv` gains `shufflesPerGame`.
- **R5. Model support (small):** `_` token in both serializers; a `Shuffles` counter on both states, incremented by the
  shuffle systems, in `CopyTo`; dead-board detection and shuffles must cope with empty cells.
- **R6. Owners.** Developer-1: Blast serializer + counter. Developer-2: Match3 serializer + counter + a check that
  matching, hints and shuffles cope with empty cells. Level Designer: R1-R4, brushes and thumbnails for `_`, regeneration,
  per-role table and the rows for levels 4, 10, 11, 17, `docs/LEVEL-DESIGN.md`. UX Designer: how a sealed empty pen
  reads at level start and when it fills; the level-4 intro.

- **R7. Web deploy** (user, 18 Oct): after the round-2 levels are generated and Play-checked, Developer-1 runs the
  PlayMode flow tests, the Remote content build and the All Games WebGL release, the serve round trip (level 2 shows the
  pen), then `Tools/deploy_pages.py`. No `publish_levels.py`: both `published.json` files sit at revision 1, which the
  build records, so the embedded pack wins. The game repo stays uncommitted until the user says otherwise.

- **R8. First-four casual floors** (Advisor, 18 Oct, from Developer-1's deploy note: Blast 1 casual 0.96, Gem Swap 1
  0.85): a demo's first levels are close to unlosable. Casual floors become level 1 ≥ 0.99, levels 2-3 ≥ 0.95,
  level 4 ≥ 0.90 in both games; the spare-moves cap on levels 1-4 is relaxed to 0.60 since near-unlosable means
  spare moves. Retune by goal size first, then moves; boards, pens and everything from level 5 on stay byte-identical.
  The shipped-level test carries the new floors. Level 5 (normal, casual 0.24 / 0.27) is left on the curve and put
  to the user. Ships in deploy 2 together with the streaming-slides fix (§7), Editor order after deploy 1: Level
  Designer (done 12:21: casual losses out of 240 are Blast 0/1/6/0 and Gem Swap 1/6/5/11 on levels 1-4, skilled 1.00,
  spare 0.51-0.59; goals and moves changed only, Blast 3 asks 6 of 8 boxes; `MinCasualRate`, `MinCasualPlayouts`,
  `PassesCasual`, `MaxMovesLeftOnWin(slot)` in `LevelCurve`; EditMode Tiles 289, Blast 166, Match3 184)
  → Developer-2 → UX Designer (fix the blank Music icon in the pause popup, build only, found in the deploy-1
  serve round trip; then re-check levels 1-2, Blast 4 and Gem Swap 5 streaming, the pause popup) → Level Designer
  (split `Match3Generator.cs`, 440 lines after R8, under the 400 budget; Architecture back to 14/14) → Developer-1.

## 7. Optional follow-ups (not scheduled)
- Levels window badge for slide-fed cells (Level Designer).
- Duck terms in `Match3Evaluator` like Blast's (Level Designer; not needed for 100/100).
- One landing system: `LandAllSystem` calls an `OnLanded` hook so Gem Swap drops its own (Developer-1 + Developer-2).
- Refill under a flying flyer: the flyer's hit becomes a deferred `Arrive` action the view reports when the flight ends
  (like `Land`), with the target kept as a piece id; bots resolve it in `LandAll`. Today the falls wait about 0.85 s for
  the hit, because chains resolve on a stable board (ADR 0019). Found in D2; deferred (Developer-2).
- Streaming slides at a pen opening: `FallMover.Crossed` lets a slide start only when the piece ahead is a full cell
  past the slide's target, so pieces queued at one opening each hold about 80 ms and the column steps down. Start
  the slide once the piece ahead is one cell along its own path; keep the D2 crossing invariant and `Overlaps()`
  green. Found in the round-2 Play check (FALL-MOTION.md §10.1). Done 18 Oct 12:40 (Developer-2): one sorted column
  walk with a predicted position, plus `Leads()`: a follower may slide the same way behind a sliding leader once a
  cell apart and no faster than it; queue test and pocket twin with `Overlaps()`; Tiles 290, Match3 185, Blast 166.
  UX re-check PASS 18 Oct 12:55 (FALL-MOTION.md §10.2): holds before a slide 30 → 0, Blast 4 left pen fills in 0.29 s
  (was 0.68 s), no crossings, landing clears unchanged. Closed.
- Follower speed pulse: a piece falling behind a queued slide pulses 8-15 cells/s as it snaps to the leader's speed;
  ease toward the leader's speed in `Fall()` instead of snapping (Developer-2, from the 18 Oct re-check).
- Single-frame speed dip at a fall-to-slide corner (Blast 4 piece 3 at 4.6 s: 13, 5, 12 cells/s); predates the
  streaming change (Developer-2).
- Flaky `BlastFlightTests.Lands_DoNotAllocate_AfterWarmUp`: failed once in a full Blast run on 18 Oct 13:05, passed
  alone and on rerun (and once before, 17 Oct, when the log was pre-sized). Order-dependent: find the one-time
  allocation (lazy static, first JIT path, pooled buffer grown by an earlier test) and warm it up in the test, or
  measure the minimum over several runs after a forced collection. Done 18 Oct 13:30 (Developer-1): the test plays the
  whole tap sequence once on a throwaway engine before measuring; Blast EditMode 166/166 on three full runs.
- Deploy race on the Addressables catalog: keep the previous build's bundles for one deploy (names carry content
  hashes, so old and new coexist; prune on the deploy after) in `Tools/deploy_pages.py`, or pin the catalog with the
  commit. Found after deploy 2. Done 18 Oct 13:40 (Developer-1): `previous_bundles()` reads the deployed catalog and
  protects the bundles it names from `rsync --delete`, one build back; checked offline on an archive of deploy 1;
  documented in WEB-BUILD.md "Deploy script" step 2. Takes effect at the next deploy.
- Tuner hit check vs band test: the tuner's float comparison marks Blast 32 and Gem Swap 20 off target while the
  band test (0.005 rounding) counts them in; make the tuner round the same way so `report.csv` agrees with the test
  (Level Designer, with the next generation run).

## 8. Status
- A1 · A2 · A3 · A4 — Developer-1: all green 17 Oct. Tiles `Gravity`/`GravityGrid`/`Flight` (196/196 at the time),
  Blast on Gravity + flight (BlastRules v2, boxes static, ducks collected when resting at the bottom, shuffle only in a
  quiet pass, FindGroup/HasMove skip in-flight pieces, neighbour damage skips in-flight balloons while specials hit
  everything, reasons Falling 104 / NoMovesLeft 105, codecs Land 5 / LandAll 6), presenters on FallMover (BlastFalls,
  ticked by the game presenter: board, session watchdog, tap buffer), Feel speed fields + `_landClearGap`, drop-in on
  FallMotion, origin stagger, impact squash, landing reports after the gap, effect holds, ADR 0022. Final: Blast EditMode
  163/163 (×4), Blast PlayMode 2/2 incl. the spam check, on the retuned content. Advisor decisions kept: slide side by
  (x+y) parity; spawn + straight fall as two legs; `LandAllSystem` one piece per quiet pass; `MaxResolutionPasses` 4096.
- B1 · B2 · B3 — Developer-2: B2 done 17 Oct (`IsBusy`, `preempts:` flag, `EventPlayerTests` 11/11; only lanes from
  an earlier `Play` are preemptible; wave mode waits for every earlier step, since landings leave step gaps). B1 done
  17 Oct (Gem Swap on `Gravity` + flight, `Match3FlightTests` 10/10, replays rebaselined on RulesVersion 2). B2b done
  17 Oct (`FallMotion`, `FallMover`, `FallMoverTests`; `BoardFilm.Run(idle, frame)` and `Track`). B3 done 17 Oct (view
  landings with `LandClearGap`, origin stagger, effect hold, `CanActOn` + tap buffer, watchdog, level context ticks the
  falls; BoardMotion 34/34, Match3 PlayMode 2/2 with the spam check; ADR 0023). Closed 17 Oct: `Match3Moves`
  (controller: swaps, activations, buffer, hold checks, landing handler, booster origins) and `Match3Hint` split out for
  the 400-line budget; `FallCurve` deleted; settled-within-2-frames assertion in every landing motion test. Final:
  Architecture 14/14, Tiles 276/276, Match3 EditMode 170/170, BoardMotion 37/37, Match3 PlayMode 2/2.
  D2 fixes 18 Oct: crossing rule at slide-leg start (schedule-order priority); later-move spawns lifted above a column's
  earlier runners (the real pass-through: a spawn placed under an earlier spawn still coming down); `BoardFilm.Overlaps()`
  in every Gem Swap motion test; leg turns follow on at once when they wait only for earlier legs (the 25 ms hitch);
  IsSettled true once the level has ended. Tiles 280/280, Architecture 14/14, Match3 EditMode 171/171, BoardMotion 38/38,
  Match3 PlayMode 2/2.
- C1 · C2 — Level Designer: started 17 Oct. Evaluators live in `Blast/Model/BlastEvaluator.cs` and
  `Match3/Model/Match3Evaluator.cs` (interface `IPositionEvaluator<TState, TMove>` with `Score` and `Value`); bot
  checks in `BlastBotTests.cs` / `Match3BotTests.cs`. C1/C2 green 17 Oct: BotProfile (Casual ε25; Skilled ε5, lookahead
  over the top 6 moves, 50% follow-up share), shapes Bridge, Keyhole, Cave, Shelves, Goblet, Chimney, Window sill cell at
  odd widths, BoardReach (Unreachable, SlideFed, Unseal, FallsToBottom), pocket boards on each chapter's 3rd/5th/8th
  level, level 2 = Bridge. C3 done 17 Oct: both games 100/100 on the skilled table (Gem Swap normal 0.76 / hard 0.43 /
  super hard 0.20 skilled, casual 0.30 / 0.08 / 0.04; Blast 0.76 / 0.42 / 0.22, casual 0.31 / 0.06 / 0.00); 8 Gem Swap
  levels use a sixth colour; super hard has no casual floor; BlastEvaluator values the cube under a duck; replays
  rebaselined; EditMode 606/606 with the winnable checks live. LEVEL-DESIGN.md updated and ADR 0024 written 17 Oct.
- D1 · D2 — UX Designer: D1 spec written 17 Oct in `docs/FALL-MOTION.md` (fall model, slides, play during falls,
  landing clears, callouts, static Blast boxes, frame on pocket shapes; §8 decided by the Advisor). D3 done 17 Oct (box_1/box_2 redrawn, `Artifacts/fall-motion/blast-box-before-after.png`).
  D4 done 17 Oct (Gem Swap frame in two passes, `board_rim` + `board_fill`, `_frameRimWidth` skin field; all six new
  shapes plus Window clean in `Artifacts/fall-motion/frame-gemswap-unity.png`; Level Designer has the go). D2 Gem Swap done
  17 Oct (`docs/FALL-MOTION.md` §9.1, `Artifacts/fall-motion/d2/`): speeds, slides, squash, landing clears, buffer,
  specials hold, callouts and view = model all pass; two FallMover fixes to Developer-2 (slide arriving before a
  cross-action faller passes; 25 ms hitch at leg turns). D2 Blast done 18 Oct (§9.2): speeds, slides, 15/15 taps during
  falls, specials hold, no crossings; to Developer-1: boxes still drop in, the corner hitch remains in Blast, hint counts
  from the tap not from settling. Gem Swap re-check: hitch and crossing fixed; new to Developer-2: two same-move spawns
  in one column can come in reversed (g1.csv). Fixes 18 Oct: Developer-1 (boxes skip the drop-in, hint counts from
  settling, stall check in Blast motion tests; the Blast hitch recordings predate the mover fix); Developer-2 (a move's
  new pieces in a column lifted by one shared offset via `FallMover.Spawn` + `NextMove`, replacing `Behind` which lifted a
  batch piece by piece; slide-crossing priority by leg queue order; `FallRunner.cs` split out; Match3 EditMode 179/179,
  BoardMotion 46/46). Blast switched to Spawn/NextMove 18 Oct (Developer-1; 163/163, motion 19/19, PlayMode 2/2). UX
  re-checks 18 Oct (§9.3): Gem Swap two-spawn crossing and hitch PASS; Blast boxes in place at start, no hitch, hint 5.0 s,
  refill of one column by taps 0.15 s apart PASS. D1-D4 closed; no Feel numbers changed. `Behind` deleted 18 Oct
  (Tiles 280/280, BoardMotion 46/46, Architecture 14/14). Implementation closed; next: the user's play-test, then commits.
- Round 2 (R1-R7) — status 18 Oct 11:30: pens, `_` cells, `LevelTuner`, `shufflesPerGame` and the new acceptance
  rules are in; the Level Designer's final run is on disk (Blast 03:18, Gem Swap 03:31; 21 pen levels per game, level
  2 = Intro pen). 93/100 per game hit; no misses in levels 1-10. Shipped soft-cap misses: Blast 32, 37, 42, 50, 63, 70;
  Gem Swap 12, 20, 39, 57, 71, 75 (shuffles 0.52, the worst). Two band misses were hand-retuned by goal size 11:40 (Blast 68
  +10 colour: 0.82 → 0.72 vs 0.73; Gem Swap 24 +5 colour: 0.88 → 0.73 vs 0.76; moves unchanged, rest of both
  packs byte-identical; new test `EveryShippedLevel_IsInsideItsBand_AndTheFirstTenMeetEveryRule` in both games;
  Tiles 289, Blast 166, Match3 184). Per role, skilled: Blast easy 0.96 / normal 0.75 / hard 0.40 / super hard
  0.20; Gem Swap 0.97 / 0.76 / 0.42 / 0.20; casual on easy 0.54 / 0.68. The user's levels now: Gem Swap 10 skilled 0.54 / 0 shuffles, Gem Swap
  11 0.96 with 24 boxes under a Frame, Blast 17 0.80 with 15 boxes and a bomb gift. Editor order from 11:25: UX Designer
  Play check (level 2 and a Corners pen in each game) → Level Designer retune → Developer-1 web deploy (R7). Deploy 1
  live 18 Oct ~12:10 at https://ocebi.github.io/tessera/ (portfolio commits c0b16eb game files, 879666e page; PlayMode
  flow 6/6; build 307 s, 30.2 MB, wasm 14.2 MB). jsDelivr now answers 403 "package size exceeded 50 MB" for the
  portfolio repo at this commit, so new files fall back to GitHub Pages (works, slower on cold loads); decision with
  the user: a small dedicated repo for the build files, or Pages only. Deploy 2 (R8, streaming slides, Music icon,
  generator split) live 18 Oct 13:20: commits 7958576 game files, d115588 page; Architecture 14/14, Blast 166, Match3
  185, PlayMode 6/6; build 193 s, 30.2 MB; serve round trip clean; cold open to hub ≈ 3.5 s. Finding: the Addressables
  catalog (StreamingAssets/aa, from Pages, 10-min cache) is unpinned while bundles are pinned, so a browser that cached
  the old catalog around a deploy asks for a deleted bundle and hangs on the loader; fix in §7.
