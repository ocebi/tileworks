# Fall motion: slides, play during falls, landing clears

> Owner: UX Designer (task D1 in `docs/FALLING-BOARD.md`). Developers build the presenters to this; the numbers live
> in the Feel configs and get tuned in the D2 Play check. Written 17 Oct 2026. Studio names stay out of this file.

## 1. What the player should feel
- A piece falls like a weight: it starts slow, speeds up, and never stops in mid-air unless it lands.
- A slide is clearly a slide: the piece tips diagonally into the pocket at a steady pace you can follow, then keeps falling.
- When the board refills, you can keep playing on what has already landed. Nothing you see resting is "not ready yet".
- A gem that lands into a line pops out of its landing, so the cause (the landing) and the effect (the pop) read as one beat.
- Specials, combos, shuffles and boosters still hold the board: big moments get their full stage.

## 2. One fall model for both games (replaces `FallCurve`)
Today a fall is a tween whose length is `FallBase + FallPerRow · √rows`, eased `InQuad` from rest. That can't carry
speed into a second leg, so a fall that is extended mid-air (rule D4) would stop dead and start again. Both reference
games move falling pieces by speed instead: a start speed, a constant acceleration, a top speed, and a queue of
targets that a new fall appends to, so the piece keeps its speed.

Fields, in cells per second, identical names in `BlastFeelConfig` and `Match3FeelConfig` (a shared `FallMotion`
struct in Tiles View, like `FallCurve` today, built from them and used by the drop-in too):

| Field | Default | Meaning |
|---|---|---|
| `_fallStartSpeed` | 4 | Speed a resting piece (or a new spawn) starts falling at. |
| `_fallAcceleration` | 60 | Gain per second while falling straight. |
| `_fallMaxSpeed` | 24 | Top speed. |
| `_slideMinSpeed` | 8 | A slide never moves slower than this, even from rest. |
| `_slideMaxSpeed` | 12 | A fast piece slows to this as it turns into a slide, so slides stay readable. |
| `_landSquashFloor` | 0.35 | Share of `LandSquash` a slow landing gets; a landing at top speed gets all of it. |
| `_fallColumnStagger` | 0.03 | Kept, but measured from the move's origin column (see 2.3), not from column 0. |

`_fallBase` and `_fallPerRow` go. `_slideStep` (Gem Swap) goes, replaced by the two slide speeds.
These defaults give 0.13 s for one row, 0.30 s for four and 0.51 s for nine (today 0.20, 0.32, 0.44): short drops get
snappier, long ones keep their weight. D2 tunes them on device.

### 2.1 Legs
- **Straight leg:** accelerate from the entry speed at `FallAcceleration`, capped at `FallMaxSpeed`.
- **Slide leg** (one diagonal step, length √2 cells): constant speed `clamp(entry speed, SlideMinSpeed, SlideMaxSpeed)`,
  linear. No rounding of the corner; the turn is sharp, as in the references.
- The next leg starts at the exit speed of the one before. A leg that starts from rest uses `FallStartSpeed`.
- Spawns start at their `Drop` height above the board at `FallStartSpeed`. Blast's spawn fade lasts as long as one row
  takes.
- While a piece moves it draws above blockers and resting pieces (Gem Swap's `Lift` / `Land` sorting already does this;
  Blast adopts it), so a slide past a box corner never looks like it goes through the box.

### 2.2 A fall extended mid-air (rule D4)
- The piece keeps its current position **and speed**. It first finishes the path to the new leg's `Source` (it is
  always on the way there), then follows the new legs. No squash, no land sound, no landing report until the leg with
  `Lands`.
- **Follow rule:** a falling piece never comes closer than one cell to the piece below it in the same column, or to the
  piece on its slide target. When it catches up it takes that piece's speed. Without this, a fast piece overtakes the
  slow one under it that just started from rest (a tap under a resting piece while a refill streams down onto it). Both
  reference games keep this gap.
- This needs a per-frame mover (one update over the moving pieces, legs in a small per-piece queue, driven by the
  presentation clock so `BoardFilm` stays deterministic), not one tween per leg. A4 and B3 rewrite the fall presenters
  anyway; the presenter returns the leg's planned duration to the event player as it does today.
- If the per-frame mover is out of scope, the tween fallback is: the extended fall restarts at `FallStartSpeed` (not
  from zero) from where the piece is. With constant acceleration, pieces that start together move in lockstep, so
  nothing overtakes. Speed visibly drops mid-air, but the piece doesn't stop.

### 2.3 Column stagger
`FallColumnStagger · |column − origin column|`, where the origin is the tapped cell (Blast), the swap's cells (Gem Swap),
the special's cell, or the landed cell for a landing-triggered clear. The refill ripples outwards from where you
played. Today's left-to-right sweep holds the right-hand columns in the air up to 0.24 s longer than the left ones,
and with play during falls that's 0.24 s in which those pieces can't be tapped. Extended legs take no stagger:
they are already moving.

### 2.4 Landing
- Squash = `Lerp(1, LandSquash, Lerp(LandSquashFloor, 1, impact speed / FallMaxSpeed))`, over `LandDuration` as today
  (35% down, 65% back with `OutBack`). A one-row drop thumps lightly and a long drop slams. Slide landings are light.
- The land sound plays on the landing leg only, throttled as today.
- Static pieces (Gem Swap boxes and chains, Blast boxes from now on) never get a squash or a land sound, and **don't drop
  in at level start**: they are on the board when it appears and the movable pieces rain in around them.
  `BoardDropIn.Play` gets only the movable pieces. This is the cheapest way to say "this is fixed".

## 3. Play during falls
| Situation | What the player sees |
|---|---|
| Tap / swap on landed pieces while others fall | Normal clear or swap, at once. The pieces above it extend their falls (2.2). |
| Tap on a piece in flight | No visual and no sound (a wobble would fight the fall). The tap is **buffered**: held `TapBuffer` and applied when that piece lands, if it lands in time. |
| Blast tap rejected `GroupTooSmall` while anything is in flight | Buffered the same way: retried after each landing within `TapBuffer`. The refuse wobble plays only when the buffer runs out. This covers the "I tapped a pair, its partner was 40 ms from landing" case. |
| Gem Swap swipe touching an in-flight gem | Buffered; applied when both gems have landed within `TapBuffer`, else dropped silently. A swap between landed gems that makes no match is refused at once, as today. |
| Input during a special, combo, shuffle or booster | Ignored as today. Not buffered: a move firing after a long effect would surprise. |
| Booster bar | As today: usable only when settled. The buttons don't dim during falls: they'd flicker on every refill. |
| Hint | The idle timer runs only while settled; every landing and every move restarts it. |

`TapBuffer`: `_tapBuffer` = 0.15 s in both Feel configs. The buffered action goes through the normal `Tap` / `Swap`
path at the moment it's applied, so replays record it like any move.

## 4. Gem Swap: clears made by a landing
- The pop starts `_landClearGap` = 0.06 s after touch-down: the landing's squash-down plays, then the clear swells
  out of the squashed piece. The clear stops the landing's scale-back tween on that piece and swells from wherever
  the scale is: thump, then pop, as one beat. The other gems in the line pop at the same moment, staggered by
  `ClearStagger` from the landed cell.
- Simplest build: the view reports the landing `LandClearGap` after touch-down, so the clear arrives on time with no
  special case in the player. The piece stays in flight for those 60 ms, which the tap buffer covers.
- The land sound plays at touch-down and the match pop at the pop; the voice caps keep a burst of landings in check.
- A special made by a landing appears on the landed gem (as the rules say) with the usual merge.
- Blast keeps reporting at touch-down (`LandClearGap` isn't in its config): no clear there comes from a landing.

## 5. Callouts
With cascades driven by landings, the cascade count rises over several landings, and today every `Cascade` event shows
a callout. A five-match chain would read "Nice! Great! Amazing! Amazing!".
- Show a callout only when the **tier** rises within the move (Nice at 2, Great at 3, Amazing at 4+), at most once per
  tier. A higher tier replaces the current callout at once; the restart reads as an upgrade.
- Time it to the pop of the match that crossed the tier, not to the landing.
- The tier resets on the player's next tap or swap, even while the previous move's pieces are still landing.
- Same rule for Blast's special-count callouts.
- No new fields: the callout timings stay in the Kit UI motion config. (Gem Swap's `_callout*` / `_banner*` Feel fields
  are still unused; the board developer can remove them.)

## 6. Blast boxes are static now
- No fall, squash or land cue on boxes; nothing to animate when cubes clear around them. They don't drop in at level
  start (2.4).
- **Sliding into a pocket under a box:** the cube beside the box falls to the box's row, turns, and slides diagonally
  under the box's lower corner at slide speed, drawn above the box (2.1), then lands lightly. The box doesn't react:
  any wobble would say "loose".
- **Art:** Blast's box is a wooden plank crate with corner brackets, and it reads as a crate that could fall. Gem Swap's
  box is a riveted steel-banded block and reads as fixed. Proposal (design, after D1): redraw Blast's `box_1` / `box_2`
  as a heavier, bolted block with a darker bottom edge so it sits *in* the cell, keeping Blast's warm palette. The two
  hit stages stay distinguishable.

## 7. Board frame on pocket and overhang shapes
Each band of identical rows is drawn as one rounded rect per run of cells, with a margin past the cells (Blast 0.25 cell,
Gem Swap 0.28). Corner slices are 0.25 cell, so even a one-cell-wide run fits its corners. Checked 17 Oct with HTML
mock-ups of the six slide shapes and Window at 8×9 and 9×9, using the real sprites and the views' band logic
(`Artifacts/fall-motion/`):
- **Blast reads cleanly on every shape.** Rims go first and fills on top, so the bands merge into one silhouette, holes
  have a continuous rim, and concave corners are sharp and tidy.
- **Gem Swap shows three artifacts**, on today's shapes too wherever rows split into bands. Its frame is one sprite,
  a gold rim plus a navy fill, drawn per band at one order:
  - a notch in the straight outer edge at every band boundary, where two bands' rounded corners meet;
  - gold spurs at concave corners (Keyhole shoulders, Goblet and Chimney steps);
  - a hole's top rim drawn inside the hole, under the band above it.
- **Fixed 17 Oct (D4):** Gem Swap draws in two passes too: every band's `board_rim` (brown outline, gold bevel rings,
  dark inner line), then every band's `board_fill` inset by `FrameRimWidth` (0.07). Both sprites are shaded in rings, so
  bands meet without a colour step. Checked in Unity on all six shapes and Window at 8 and 9 wide
  (`frame-gemswap-unity.png`).
- **Single-cell interior holes** show only a 0.5 × 0.5 cell speck of backdrop in both games. Shapes avoid them: interior
  holes are at least 2 wide, or 1-wide slots at least 2 tall (agreed with the Level Designer).

All six new shapes work in both games.

## 8. Decided (Advisor, 17 Oct)
1. Per-frame mover with the follow rule: yes, task B2b (Developer-2), `FallMotion` + `FallMover` in Tiles View,
   replacing `FallCurve` and feeding the drop-in. Presenters still return the planned leg duration; the watchdog waits
   0.5 s of idleness; motion tests assert `IsSettled` on the last real landing.
2. Tap buffer: yes, `_tapBuffer` 0.15 s, `Range(0, 0.3)`, controller only, retried on every landing report. On expiry, a
   landed cell still refused gets the refuse feedback; a cell still in flight is dropped silently. Nothing is buffered
   while a special holds the board.
3. Origin-relative stagger: yes. Origins: the tap, the swap's destination, the special's or hammer's cell, the landed
   cell, and the board centre for a shuffle. The controller hands the origin to the board view before it submits.
4. Blast box art: redraw as a bolted block (task D3). Static pieces skip the drop-in in both games.

## 9. Play check (D2)
Runs on the throwaway `ux-check` profile: a `play-level` scenario with the level file as payload, `fresh = true`, so no
save of the user's is read or written. One Editor slot per game, announced, last in the queue. Captures go to
`Artifacts/fall-motion/d2-*`. The motion is checked as filmstrips: `Time.timeScale` 0.25 and a frame every 1/30 s of game
time, from a UniTask loop started in Play. `Time.timeScale` goes back to 1 afterwards.

Levels, one per shape, from the 17 Oct `report.csv`: Gem Swap `m3_0002` Bridge (the slide lesson), `m3_0013` Shelves,
`m3_0023` Keyhole, `m3_0025` Chimney, `m3_0058` Goblet, `m3_0095` Cave. Blast `bl_0002` Bridge, `bl_0015` Keyhole,
`bl_0023` Cave, `bl_0028` Chimney, `bl_0078` Shelves, `bl_0088` Goblet. Plus a hand-made 7×8 with a pocket under a
2-wide box, per game.

| # | Check | Looks right when |
|---|---|---|
| 1 | Level start | Boxes and chains are there from the first frame; gems rain in around them; frame clean on the shape. |
| 2 | Plain refill after a swap | Columns start from the swap outwards; one-row drops land lightly, long drops slam; one land sound per landing. |
| 3 | Slide into a pocket | The gem turns at the overhang, slides at a speed you can follow (about 0.12-0.18 s a step), draws above the box, lands lightly. |
| 4 | Swap on landed gems mid-fall | Accepted at once; nothing still falling is disturbed. |
| 5 | A further fall under falling gems | Gems in the air keep their speed through the new target: no stop, no squash or sound mid-air, never closer than a cell to the gem below. |
| 6 | Landing clear | Thump, then pop 0.06 s later out of the squash; no scale fight; the line pops as one beat. |
| 7 | Cascade callouts | One callout per tier (Nice, Great, Amazing), at the pop that crossed it; no repeats; a new swap resets it. |
| 8 | Swipe onto a falling gem | No visual; it swaps if the gem lands within 0.15 s, else nothing. Swipe a landed pair with no match: refused at once. |
| 9 | Special or combo while gems fall | Input is ignored for the effect's length, not buffered; play resumes on landed gems after it. |
| 10 | Hint | Shows only after the board has settled for the idle time; any landing restarts the wait. |
| 11 | Spam swaps through a cascade | No phantom clears, no stacked gems, no gem lands twice (the 15 Oct bug). |

What feels wrong goes to the Advisor with a capture. Feel values are tuned in `Match3Feel.asset` (my file); anything
past a number goes to the developer.

### 9.1 Gem Swap results (17 Oct)
Measured from per-frame recordings of every piece (position, scale, the callout) on `m3_0002` Bridge, `m3_0023`
Keyhole and the hand-made pocket level, about 25 moves. Recordings in `Artifacts/fall-motion/d2/`.
| # | Result |
|---|---|
| 1 | Frame two-pass draws cleanly in the game. Static boxes present. The level-start drop-in itself wasn't captured. |
| 2 | Fall speeds match the spec: start 4, gain 60, top 24 cells/s. Origin stagger checked in code, not measured. |
| 3 | Slides run at 8 cells/s from rest and about 11.7 when entering fast. **Hitch:** the piece all but stops for 1-2 frames (about 25 ms) at every turn between a fall and a slide. |
| 4 | Swaps on landed gems during falls are accepted at once; swaps on gems still settling are held and applied 70-80 ms later. |
| 5 | No stop in mid-air on the board; pieces wait only above the board, under the mask. **Bug:** a gem that slides into a cell can arrive before a gem that falls through that cell, which then passes through it (`k2.csv`, pieces 27 and 95, 1.98-2.2 s). |
| 6 | Landing clear: squash, then the pop about 0.065 s after touch-down, swelling from the squashed scale. |
| 7 | One callout per tier, upgraded in place (Nice!, then Great!), never repeated in 10 recorded moves. |
| 8 | A swipe onto a falling gem shows nothing and spends no move when it can't be applied. |
| 9 | Input during a flyer is ignored and not buffered. **Feel:** the cells cleared by the swap stay empty until the flyer has hit, about 0.85 s, before any refill starts (`mid.csv`). |
| 10 | Hint not checked. |
| 11 | Six swaps in 0.7 s through a cascade: the view matches the model exactly (no missing, misplaced or stacked gems). |
No Feel value changed: nothing measured needs a different number yet. The user's own play-test decides the feel.

### 9.2 Blast results and Gem Swap re-check (18 Oct)
Same method on `bl_0002` Bridge and `bl_0015` Keyhole, then `m3_0023` Keyhole again after the FallMover fixes.
- Blast passes: speeds within the spec, slides about 7-12 cells/s, taps on landed groups during falls accepted
  (15 of 15 at 0.3 s intervals), taps ignored while a special plays, no mid-air stops, no crossings.
- **Blast: boxes still drop in at level start** (`kstart.csv`: `box_2` pieces fall in from the top, scaling 0.76 to 1).
  Static pieces should be in place when the board appears (§2.4).
- **Blast: corner hitch remains** (`b1.csv`, piece 28 at 0.78 s: the fall ends 0.005 short of the corner and the piece
  waits a frame at 0.3 cells/s before sliding at 11.6). Fixed in Gem Swap.
- **Blast: the hint counts from the tap, not from the settle** (`hint.csv`: tap 0.07 s, settled 1.02 s, hint 5.07 s,
  so 4.05 s after settling instead of 5). Gem Swap restarts the wait while anything moves.
- **Gem Swap: new spawns can pass through each other** (`g1.csv`, pieces 91 and 92 at 1.24-1.36 s; also `g2.csv`, 103 and 104).
  Two gems spawned for the same column come in reversed: the one for the lower cell starts higher and falls through the
  one that lands in the top cell. The slide crossing from 9.1 didn't recur in 6 moves; the corner hitch is gone.
- Level-start drop-in in Blast plays before the New Booster cards, 0.4 s for 48 pieces.

### 9.3 Final re-check (18 Oct)
| Item | Result |
|---|---|
| Gem Swap: two spawns in one column crossing | Pass: 17 moves on `m3_0023` with swaps 0.2 s apart, no crossings (`r1.csv`, `r2.csv`). |
| Gem Swap: corner hitch | Pass: every turn keeps its speed. |
| Blast: boxes at level start | Pass: boxes sit in place at full size while cubes, balloons and ducks drop in (`kstart2.csv`, `bl_0015`). |
| Blast: corner hitch | Pass: three fall-to-slide turns at 12.1-12.9 cells/s against about 12 around them (`p1.csv`). |
| Blast: hint | Pass: 5.0 s after the board settles (`hint2.csv`). |
| Blast: a column refilled by two moves in a row | Pass: 8 rounds on `bl_0023` Cave, the same column tapped again 0.15 s after the first tap (and a third time in one round); every tap was accepted and there were no crossings (`rf6.csv`). |

## 10. Sealed empty pens (round 2, 18 Oct)
A pen is a group of playable cells that start empty behind a wall of boxes or chains (`docs/FALLING-BOARD.md` §6b R1).

**At level start, an empty pen cell shows its plain cell tile: no tint, no extra art.**
- Holes already read differently: they have no tile, the background shows through, and the frame runs around them. A
  pen cell keeps its tile inside the frame, so it reads as a slot waiting to be filled, not as a missing piece.
- The wall is in place from the first frame, because static pieces skip the drop-in. The pen therefore reads as closed
  off: something to open.
- A darker tint would add a third look for cells, and it would have to fade away while the pen fills, which is the
  moment that should be about motion. In Gem Swap it would also sit close to the dark checker cell and the hole colour.
- The drop-in needs no change. Empty cells get no piece. With the bottom row landing first, a bottom pen stays empty
  while the rows above fill in, and the board is at rest about 0.6 s later.

**The fill.** When the last wall piece clears, the same move carries on into the pen: pieces fall into the wall's cells
and slide on into the pen without stopping, at slide speed (§2.1), deepest pen cells first. Nothing new is added: no
callout and no Feel values. The hint has nothing to point at inside a pen, and it still counts from the moment the board
settles.

**For the slide intro on level 2, both games (a demo; most players play only five levels):**
- One pen at the bottom, 2-4 cells.
- A wall of single-hit pieces: one-layer boxes or chains.
- The wall touches a group the player is likely to clear in the first two or three moves, so the fill comes early and
  can be seen happening.

### 10.1 Round-2 Play check (18 Oct)
Levels as generated at 03:18 (Blast) and 03:31 (Gem Swap); `ux-check` profile; timeScale 1. Recordings are in
`Artifacts/fall-motion/d2/round2/`.

| Check | Result |
|---|---|
| Empty pens at level start | Pass. Plain cell tiles inside the frame, below the wall, reading as empty slots (`b2_start.png`, `g5_wide.png`). |
| Level 2, fill when the wall clears | Pass. Blast: the wall broke in 3 taps, and the first pen cell filled 0.33 s after the falls started, one of them by a slide at slide speed. Gem Swap: the wall broke on the third swap, and the pen filled in 0.15 s. |
| Corner pens (Blast 4, Gem Swap 5) | Pass. Both pens fill by slides from the opening, with no crossings, no jumps and no stops in mid-air. |
| Swaps and taps during the fill | Pass. On landed pieces they are accepted at once; on falling pieces they are refused and spend no move. |
| Landing clears (Gem Swap) | Pass. The pop comes 0.066 s after touch-down. |
| Hint | Pass. Blast 4.98 s and Gem Swap 7.99 s after the board settles (delays of 5 s and 8 s). |
| Gem Swap 10, Blast 17 | Pass. Both load and play: 3 swaps and 4 taps accepted, no console errors. |

**Feel note, not a failure:** where pieces queue at one pen opening, each one holds on its cell for about 80 ms before it
slides. The column above follows in steps instead of one stream; in Blast 4, one piece held three times in 0.7 s. A
slide waits until the piece ahead is a full cell past the slide's target, which puts two cells between them. The
follow rule elsewhere keeps one. This wait is the D2 crossing fix, so it is safe to ship. A follow-up could let the
slide start once the piece ahead is one cell along its own path.
The level-start captures show every booster card, because a directly launched level unlocks every booster. That comes
from the test setup and doesn't happen in the real progression.

### 10.2 Re-check before deploy 2 (18 Oct)
After Developer-2's change to how a queued slide waits (it now starts once the piece ahead will be a cell clear), and after
adding Blast's missing `icon_music`. Recordings: `b4r.csv`, `g5r.csv`; capture: `b_pause.png`.

| Check | Result |
|---|---|
| Levels 1 and 2, both games | Pass. All four load and play, 3 moves each, with no console errors. |
| Pen opening, Blast 4 and Gem Swap 5 | Pass. Short holds on a cell before a slide went from 30 to 0. In Blast the left pen now fills in 0.29 s, down from 0.68 s, and pieces 28 and 75 turn from the fall straight into the slide. No crossings, no follow-gap breaks, no stops in mid-air. |
| Landing clears, play during the fill | Pass, unchanged: the pop comes 0.067 s after touch-down; swaps and taps on landed pieces are accepted, and on falling pieces refused with no move spent. |
| Blast pause popup | Pass. The Music button shows its note icon, in the same style as Sound, Vibration and Motion. |

What remains in a queue: a piece falling behind a slide pulses between about 8 and 15 cells/s, every 0.1 s, as it
catches up and then matches the slide's downward speed. It never stops, and the turn into each slide stays at 12-14
cells/s, so it reads as a stream.
