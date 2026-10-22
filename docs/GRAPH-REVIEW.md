# Graphify architecture review (22 Oct 2026)

Graph: `graphify update .` from the repo root, AST only (no LLM pass, so no semantic-similarity edges or hyperedges).
8110 nodes, 26187 edges, 218 communities, 81% extracted / 19% inferred, no import cycles. `.graphifyignore` excludes
third-party, templates, Tools/, binary and Unity asset files; `graphify-out/` is git-ignored. Asmdefs are not in the
graph, so assembly layering was checked by hand. Reviewed by Developer-1 (Tileworks tier) and Developer-2 (Games tier);
every finding below was confirmed in source. Nothing has been changed yet.

## Rules that hold
- No Tileworks assembly references a game; no `using` of a game namespace under `Assets/Tileworks`.
- Kit references neither Tiles nor Meta; Tiles does not reference Meta.
- No game references another game. No cycles anywhere.
- No dead code confirmed: the graph's 1798 isolated nodes are static helpers, `[MenuItem]` entry points and event
  structs used through `Emit`/`From`.

## Real problems
1. **Every game's Service assembly references `Tileworks.Tiles.View`.** Used only for `IEventPlayer`
   (`Tiles/Runtime/View/EventPlayer.cs`) and the `BoardBoosters.Hammer/Shuffle` constants
   (`Tiles/Runtime/View/BoardBoosters.cs`). Service is therefore not engine-free, and the architecture test does not
   catch it: `LayerMatrix` only checks references within one feature prefix and the tier rule lets Game reference all
   of Tiles. Fix: move `IEventPlayer` and `BoardBoosters` into `Tileworks.Tiles.Model` (or a small Tiles contracts
   assembly); drop the View reference from the four Service asmdefs; add an architecture rule that a game's
   Api/Model/Service/Generation may not reference any `*.View` assembly. Owner: Developer-1 (Tileworks side),
   Developer-2 (asmdefs and the test).
2. **Blast and Match3 are copies of each other across ~20 files** (line similarity after renaming 0.8–1.0: Idle,
   ResultView, SpecialPresenters, HomeView/Presenter/SceneInstaller, AppInstaller, Persistence, PlayLevelScenario,
   Session, GamePresenter, Bonus, HudView, Editor Ui and ContentBuilder). The copies have drifted: Blast's
   `SpecialPresenters` calls `_kit.CountSpecial` and shows the cascade on the HUD, Match3's does not; `IsSettled`
   differs between the sessions. Candidates to move into Tiles: home presenter/view/scene installer (generic over
   progress and start-args types), result view, special-idle loop and combo presenters behind a kit interface, the
   persistence DAO pair, PlayLevelScenario, the session's booster/continue/settle skeleton. Keep per game: State,
   Rules, Events, Generator, Planner, BoardView, FeelConfig.
3. **Tile Match's `HudView` reimplements the shared `BoosterBarView`** (refresh, press, explain bubble, intro card,
   lock badges, bubble timer) and hardcodes the bubble duration as a const. Blast and Match3 use the shared view.
   Fix: Tile Match's HUD hosts `BoosterBarView` and keeps only level, pause and replay.

## Worth a look
4. Hardcoded tunables in views and presenters (fuse colour in `BlastIdle`/`Match3Idle`; glow, arrival scale, spark
   size in `SpecialPresenters`; squash, swell, shake, flash, hop in `Match3Presenters`/`BlastPresenters`; lift scale
   in booster/move presenters; action gap, bottom padding and disabled alpha in the result views; `BoardMargin` in
   `BlastBoardView`). Fix: move into the FeelConfig assets with `Range` limits.
5. The 400-line test skips `Assets/Games/Shared` (`AssemblyCatalog.cs:115` classifies it as Project tier).
   `Shared/Editor/UiFactory.cs` is 612 lines; `Match3GamePresenter.cs` is exactly 400. Fix: count Shared as Game
   tier and split `UiFactory`.
6. `BoosterBarView` (363 lines, 33 serialized fields, four jobs). Fix: move pick mode (~120 lines) into a
   `BoosterPickOverlay` view.
7. `BlastBoardView` and `Match3BoardView` are the strongest cross-community bridges: piece pool, cell↔screen mapping,
   hint glow, hammer aim, bonus playback and landing relay in one type. Optional split like BlastFalls/BlastIdle.
8. `TileMatch/Editor/GameViewSize.cs` is a project-wide tool living in TileMatch.Editor. Move to Shared/Editor.
9. Plain-C# files holding 7–10 types (`BoardEvent.cs`, `Rules.cs`, `MetaApi.cs`): legal, but hard to find by name.

## Noise (checked, no action)
Inferred interface→override "dispatches_to" edges across tiers; `Destroy` matched to BlockPuzzle `PieceView.Destroy`;
namespace and utility god nodes (`Guard.NotNull`, `ILog`); communities named after `Color`/`TMP_Text`; doc→code
"surprising connections".

## Suggested order
1 (small, changes what the test enforces) → 3 → 5 → 4 → 2 (large, plan separately) → 6–9 as time allows.
Decision pending from the user.

## Decisions (22 Oct, user)
Tileworks stays lean: it will be copied into other single-game projects, so nothing game-specific moves into it.
- Item 1: `IEventPlayer` leaves Tiles.View (into Tiles.Model if Kit.Core already references UniTask, else a new
  `Tileworks.Tiles.Service` contracts assembly). `BoardBoosters` is game knowledge and leaves Tileworks: each of Blast
  and Match3 gets its own copy in Model (View and Controller use it too). New test: game Api/Model/Service/Generation never reference a `*.View`
  assembly. Owner Developer-1.
- Item 2: stays per game by design; ADR records it (Level Designer). Developer-2 reports whether the CountSpecial and
  IsSettled differences are omissions or rule-driven; no change unless an omission.
- Item 3: Tile Match HUD hosts BoosterBarView. Owner UX Designer.
- Item 4: feel values into Blast/Match3 FeelConfig. Owner UX Designer, after item 3.
- Item 5 and 8: Shared counted by the size test, UiFactory split, GameViewSize moved to Shared/Editor. Owner Developer-2.
- Item 6: BoosterBarView pick overlay. Owner Developer-1, after item 3.
- Items 7 and 9: not done (optional).
Editor order: Developer-1 (1) → UX Designer (3) → Developer-2 (5, 8) → UX Designer (4) → Developer-1 (6).

## Status
- **Item 2, decided by the user (22 Oct):** no extraction. Tileworks stays lean because it is copied into single-game
  projects; Blast and Gem Swap keep their own presentation, session and persistence code on purpose. Recorded in
  [ADR 0025](adr/0025-showcase-games-keep-their-own-code.md) by the Level Designer.
- **Item 1, done by Developer-1 (22 Oct):** `IEventPlayer` moved to `Tileworks.Tiles.Model`
  (`Tiles/Runtime/Model/IEventPlayer.cs`; the assembly references Kit.Core and UniTask and stays engine-free).
  `EventPlayer` and `IEventPresenter` stay in Tiles.View; no Tiles.Service assembly was needed. The four games'
  Service asmdefs no longer reference `Tileworks.Tiles.View`. `BoardBoosters` left Tileworks: Blast and Match3 each
  have a copy in their **Model** (not Service, because their View and Controller use it too). New architecture test
  `GameLogicLayers_ReferenceNoViewAssembly`: game Api, Model, Service and Generation reference no `*.View` assembly.
  Architecture 15/15, EditMode 941/941, graph rebuilt with no cycles. `ARCHITECTURE.md` §6.3, §6.7, §9, §9.10.1 and
  §12 updated by the Level Designer.
- **Item 3, done by the UX Designer (22 Oct):** Tile Match hosts the shared `BoosterBarView` like Blast and Match3.
  `HudView` keeps only level, replay tag and pause; `GamePresenter` implements `IBoosterBarHandler` and takes
  `IBoosterBarView`; the bubble time comes from `BoosterBar.asset`. `BoosterBarBuilder` (Games/Shared/Editor) gained a
  `Slot` struct and a slots overload, used by Tile Match's `SceneBuilder.Boosters.cs`.
- **Item 4, done by the UX Designer (22 Oct):** the hardcoded feel values moved into `BlastFeelConfig` and
  `Match3FeelConfig` (fuse colour, special glow, arrival scale, spark size, squash, lift, win after landing, result
  popup gap, padding and disabled alpha; Match3 also box swell, shake, floor flash, chain shake and hop, flyer spark,
  fade and ring, floor shrink, chain swell, twist and hop times). Result views take the feel config. Blast's
  `BoardMargin` became `BlastSkin.FrameMargin`, matching `Match3Skin`.
  Tests: TileMatch 177/177, Blast 166/166, Match3 185/185, Architecture 15/15; Play checks on a scenario profile
  passed. `ARCHITECTURE.md` §9, §10 (Controller and View rows) and §10.13 updated by the Level Designer; no doc listed
  these values as presenter constants, so `MATCH3.md` needed no change.
- **Item 6, done by Developer-1 (22 Oct):** `BoosterBarView` (now 287 lines) keeps the bar, badges, bubble, intro card
  and lift/drop. Pick mode (dim panels, cancel, aim card, sorting levels) moved to a new optional `BoosterPickOverlay`
  in Tiles.View. `BoosterBarBuilder` builds an overlay only when a slot has aim text, so a game that never picks gets
  none; `SetPicked` on a bar without one throws. Blast and Match3 each have one overlay; Tile Match has none.
  EditMode 941/941; Blast and Match3 PlayMode flows 2/2 each. `ARCHITECTURE.md` §9 updated by the Level Designer.
- **Items 5 and 8, done by Developer-2 (22 Oct):** the 400-line test now checks every assembly under `Assets/Games`,
  `Shared` included; `Shared` stays Project tier for the layer rules. `UiFactory` is a static partial class split into
  `UiFactory.cs`, `UiFactory.Screens.cs`, `UiFactory.Fonts.cs` and `UiFactory.Popups.cs`. `GameViewSize` moved to
  `Assets/Games/Shared/Editor` (namespace `Tileworlds.Games.Editor`). Architecture 15/15, EditMode 941/941, every
  scene rebuilt.
- **Item 2 drift, answered by Developer-2 (22 Oct):** Match3 shows cascades from a model-emitted `Cascade` event;
  Blast counted specials in the view instead, and Developer-1 is moving that into a Blast model event so both games
  follow the same design. Match3's extra `IsSettled` clauses are defensive and never change the result; left as is.
- **Item 6 follow-up, Developer-1 (22 Oct):** `BoosterPickOverlay` takes an `IBoosterPickHandler` through `Bind`
  instead of a C# event; `BoosterBarView` implements it.
- **Item 2 drift fixed, Developer-1 (22 Oct):** Blast's combo count moved into the model, as Match3's cascade count
  already was. `BlastState` keeps a per-action `Chain`; `SpecialFired` and `SpecialsCombined` carry it; the view shows
  the callout from chain 2 with a stateless `ShowChain`, and the old view counter is gone. This also fixes a hammer
  showing the previous move's count. Two new Blast rules tests. Blast golden replays: only `eventDigest` refreshed
  (99 of 100); actions, hashes and `rulesVersion` unchanged. EditMode 943/943.
- **Play check, UX Designer (22 Oct):** passed in Blast and Gem Swap. A hammer pick dims the board and shows the aim
  card; cancel works through the new handler path; a strike removes the piece and spends the count. Chain callouts
  ("Nice!", "Great!") fire at chain 2 and 3, and a hammer after a move shows no carried number.
- **Round complete (22 Oct).** Items 1–6 and 8 done, item 2 decided (ADR 0025), items 7 and 9 not done (optional).
  Nothing is committed.

## Parked
1. PrimeTween warning for `Goal0` in the Gem Swap HUD (the booster badge one is fixed with a one-line activeSelf
   guard). Developer-1's diagnosis (22 Oct): it appears right after the level starts, before any booster use.
   `Match3HudView.ShowGoal` (~line 161) punches, sparkles and plays GoalArrive while a parent of the goal chip is still
   inactive during the level intro, so the sound and burst also run unseen. The real fix is the start order in
   `Match3GamePresenter` around the ShowIntro await (~line 206), not a view guard.
2. The aim card's title sits tight against its line in Blast and Gem Swap.
3. When a rocket hits two specials in the same wave, only the later callout shows. The timing predates the model
   change and is not caused by it.
