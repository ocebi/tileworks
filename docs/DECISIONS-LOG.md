# Decisions log

Running record of implementation decisions that refine or deviate from `ARCHITECTURE.md`. Each entry says what
changed and why. Entries worth a full ADR get one in `docs/adr/`.

## Dependencies
- **ZString dropped.** Its OpenUPM build does not compile on Unity 6.3 (it expects
  `System.Runtime.CompilerServices.Unsafe`). Logs format only when the level is enabled; HUD counters use cached
  number strings.
- **Third-party content lives in `Assets/ThirdParty/<Vendor>`**, following the newest reference project's layout.
  Placeholder art and audio are Kenney packs (CC0). TextMesh Pro essentials sit in `Assets/ThirdParty/TextMesh Pro`.
- **Addressables deferred.** Levels ship as TextAssets referenced by a `LevelPackAsset` (an `ILevelSource`). The
  asset-scope seam stays; Addressables comes in only when remote packs are needed.
- **Unity Localization not added yet** (M3 decides between it and a small table-based localizer).

## Kit
- **Lifecycle interfaces (`IInitializable`, `ITickable`, …), `ILoopRegistry` and `IBootStep` live in `Kit.Core`.**
  Services implement them without referencing `Kit.Injection`, so "only installers and the app shell reference the
  container" is an asmdef rule the architecture tests can enforce.
- **`IAppNavigator`.** States and presenters request transitions through a dependency-free navigator the state
  machine listens to. The first version had `BootState → IAppStateMachine → IReadOnlyList<IAppState> → BootState`,
  which the container's validation reported as a cycle before anything was created.
- **`SceneState` is concrete.** A scene's scope receives the state's arguments through `ISceneEntry`, so games
  register `new SceneState("Level", "Game", loader)` instead of writing a state class per scene.
- **`AppComposition.RegisterCore`** holds the App-scope registrations shared by `AppRoot` and the composition tests.

## Rules (Tile Match)
- **Resolution order:** `PendingInsert → TripleClear → ModifierRefill → Uncover → ModifierReact`. Selected tiles
  wait in a pending queue and are inserted one per pass, so a triple is checked after every insertion (§2.2)
  without the handler special-casing linked pairs or the magnet. Spawners refill before uncovering, so tiles under a
  spawner never flicker uncovered.
- **Lose reason `NoMoves`** (tray not full but nothing selectable, e.g. every free tile frozen). Continue works for
  `TrayFull` and `NoMoves`, not `TimeUp`.
- **Linked tiles:** both must be free and the tray must have room for the pair.
- **Magnet:** needs free tray slots for the missing tiles; never pulls locked, hidden or linked tiles.
- **Undo** is rejected when a spawner has refilled the tile's slot.
- **State hash excludes undo history**, so the solver merges equivalent positions.

## Presentation
- **Tuning lives in config assets, not on views.** View MonoBehaviours hold only wiring set by the scene builders, shown
  read-only (`[ReadOnlyInInspector]`). Layout, timings, sounds and colours sit in per-game ScriptableObjects (`*Feel`,
  `*Skin`, `*Sounds`) with private fields, read-only properties and `Range`/`Min` limits, so a designer can't break a
  scene by editing a component and every tunable has one home.
- **Tint via `SpriteRenderer.color`** (vertex colour) rather than a `MaterialPropertyBlock`: no material instances and
  the sprites still batch.
- **Tray slot changes apply at once:** a flying tile re-targets its Bézier end point; a resting tile slides after any
  triple pop (`TrayView.ClearBusyUntil`).

- **One input router: the EventSystem.** World input arrives through `PointerSurface`, an invisible full-screen
  raycast target under every canvas, so each press goes to exactly one target: a button or the board, never both.
  This replaced per-frame pointer polling, which let a button that closed a popup also tap the tile beneath it.
  The fix holds without any script execution order.

## Tooling and tests
- **Scenes, prefabs and config assets are generated** by `Tileworks/Tile Match/Rebuild Scenes and Prefabs`
  (`SceneBuilder`), so the placeholder setup is reproducible and reviewable as code.
- **Levels:** `Tools/make_layouts.py` writes kind-less ASCII layouts; `Tileworks/Tile Match/Generate Levels` assigns kinds
  along a witness line, measures difficulty and writes canonical JSON plus `report.csv`. Assigner parameters depend on
  the target band.
- **Allocation tests use Unity's `Is.Not.AllocatingGCMemory()`.** `GC.GetAllocatedBytesForCurrentThread` always
  returns 0 on Unity's Mono, which made the first version vacuous; a control test now proves the constraint fires.
- **Playing a game scene on its own boots the App scene** (Editor only): a `SceneRoot` that wakes as the only loaded
  scene loads build scene 0. This replaced setting `EditorSceneManager.playModeStartScene`, a global Editor setting
  that also redirected the test runner's own scene.
- **Flow tests build the app in code** (`AppRoot.Configure`) on a temp save folder and click with the Input System's
  virtual mouse, so they never touch a real profile and exercise the same input path as a player.
- **`LevelPackAsset` moved from Tile Match to `Tileworks.Tiles.View`** when Block Puzzle became its second user
  (the rule of two in §9.12). The move kept the script GUID, so existing pack assets still bind.
- **One Unity object per file, named after it.** Unity binds a script file to the one class with its name; a
  second MonoBehaviour in the file silently goes missing from scenes. An architecture test enforces this.
- **No package folder may end in `.meta`:** Unity reads it as a meta file, so the meta-game package was `com.tessera.metagame`.
  Since 21 Oct the framework is source folders, and the meta-game lives in `Assets/Tileworks/Meta`.
- **Golden replays** record a solver line per shipped level (`Tileworks/Tile Match/Rebaseline Replays`).
- **Builds** use `BuildScript` (menu and command line) instead of Unity 6 Build Profile assets for now.
- **Local workflow:** compile, tests, Play Mode and screenshots run in the open Editor. Editor
  preference *Interaction Mode = No Throttling* and `runInBackground` keep Play Mode running while the Editor is
  unfocused. `Tools/unity.sh` runs the same steps in batch mode when the Editor is closed.

## Live events and race (M5, removed 22 Oct)
- **Removed 22 Oct, user decision:** the race and the live-event framework were dropped before the first public
  release (ADR 0018 withdrawn). The entries below are history. `ILevelResultListener` and Editor time travel stay.
- **Opt-in join.** A race is joined from its popup, which opens by itself once per session, instead of silently at the
  first level start. Puzzle games in the genre usually ask first, and it keeps the intro meaningful.
- **No `live_event_instances` table yet.** The schedule is a pure function of config and time (`LocalLiveEventSchedule`),
  and the race keeps its own `race_entries` rows; a generic instance table arrives with a second event type.
- **Bots are timelines, not ticks.** Each bot's win times are drawn once from `Pcg32(hash(raceId, bot))` starting at the
  join time; standings at any moment are counts of those times. Nothing runs in the background.
- **The race ends for the player** at the target, when bots have taken every prize place, or at the window's end.
- **Wins are recorded through `ILevelResultListener`** inside each game's level-result transaction, so a rolled-back
  result never counts.
- **Scenarios run on a throwaway profile** (`Scenarios/<profile>` under the persistent data path), so Editor launch
  states never touch the player's save. Time travel shifts only the running app's clock.

## Blast (M7)
- **Waves.** The rules engine's resolution passes are the animation waves: each pass fires the pending specials, then
  gravity, ducks, refill and shuffle run once nothing is pending. The event player runs in wave mode.
- **Boxes fall** like every other piece. Static blockers need diagonal or no-refill rules; they can come later as a
  rules variant.
- **Ids per piece.** Every piece keeps an id while it falls, so presenters follow pieces, not cells.
- **Combos by the two strongest touching specials** (disco > bomb > rocket); a special hit by another fires next wave.
- **Cleared pieces carry their cause** (the effect, or none for a tapped group) and, for a group that makes a special,
  the cell they slide into, so presenters time pops to each special's rhythm without knowing the rules.
- **Difficulty by move limit.** The generator keeps the board recipe and tunes moves until a casual bot lands in band.

## Web build
- **Local builds, no CI** (replaces §18.2's `build.yml` and `pages.yml`). The WebGL build is made in the Editor and
  published by a deploy script to the portfolio site under `/tessera/` (`/tileworlds/` from the next deploy). It's a portfolio project; CI isn't what it
  should show.
- **Files from jsDelivr with a GitHub Pages fallback**, the scheme Casino Mares uses: commit-pinned CDN links, and the
  page's own copy for any file the CDN doesn't send. Plan and measurements: `docs/WEB-BUILD.md`.

## Level design (100 levels per game)
- **One curve for every game** (`LevelCurve`): ten-level chapters with hard levels 5th and 10th, an easy breather after
  each, super hard at 40/60/80/100. Each level is tuned to its own target win rate rather than a band, so neighbouring
  levels don't jump.
- **Measured over several starting boards.** With one seed, the same Gem Swap level measured 0.10 and 0.03 on two
  seeds; 8-12 boards per level match how players' attempts differ.
- **Designed boards, generated numbers.** Planners pick mirrored shapes, blocker patterns and pictures and decide when
  each mechanic appears; the bot only sets moves, goal size and blocker amount. Random scatter read as unfinished.
- **Tuned without boosters**, so Hammer and Shuffle only ever make a level easier.
- Details and how to regenerate: `docs/LEVEL-DESIGN.md`.

## Boosters in Blast and Gem Swap
- **Hammer and Shuffle are rule actions**, recorded in the replay like taps and swaps, so a replay with boosters
  matches exactly. Neither uses a move. The session pays for one only after the rules accept it.
- **The hammer hits like a special**: the top layer only (a chain, a box layer, or a piece with one floor layer), and a
  special it hits goes off. Ducks can't be hit.
- **The shuffle moves every piece it can to a new cell** (Sattolo's shuffle gives a single cycle), falls back to any
  valid order, and in Gem Swap leaves no match. Blockers, chained gems and ducks stay.
- **Shared, not per game**: `Tileworks.Meta.Boosters.Service` holds the stock (economy balances), unlock levels (4 and 7,
  in config) and the seen-its-card flag. The aim and the effects live in `Tileworks.Tiles.View`, so both boards play the
  same motion from one `BoosterFx.asset`.
- **One-time grants in the economy** (`grants` table, `IEconomy.GrantOnce`): the starter stock is granted in the same
  transaction that records it, so it arrives once per save even on saves made before boosters existed.

## Board timing: falls wait for what exploded
- **The board settles a pass after specials go off**, in both games. Blast's gravity, ducks and refill used to run in
  the same pass as the last wave of specials, so the view started falls while the explosion was still popping pieces.
  Shifting all three by one pass keeps the order of state changes, so outcomes and replays are unchanged.
- **A fall never moves its piece before its own start.** A piece can fall twice in one move (after a duck leaves), and
  placing it at the second leg's start up front made it jump.
- **A presenter's busy time includes any wait it adds** (a Gem Swap match waiting for its swap), or the next step
  starts early.
- **Board motion tests** (`*BoardMotionTests`, `BoardFilm`) play moves through the real scene board and presenters,
  stepping PrimeTween by hand at 120 fps, and check that nothing jumps, no fall starts before the pieces on its path
  are gone, the board is still once idle, and every piece rests on its cell.
