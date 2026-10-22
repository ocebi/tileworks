# Remote levels

Levels can be published after a build ships. Every build keeps its own levels (the `LevelPack`), and a published set is
layered on top: new levels, fixed levels and a new order. iOS, Android and the web build read the same files.

The prototype hosts them in the portfolio repo on GitHub Pages. The client treats that as any content CDN, so moving to
a real one changes only the manifest URL in `Assets/Games/Shared/Config/RemoteLevels.asset`.

## Publishing
1. Edit levels and the sequence in the Levels window (Tileworks > Levels) and save them.
2. Press **Publish** in its toolbar. It checks the game's saved levels (envelope, content hash, the game's own validation),
   exports them to `Builds/Levels/<game>/` and runs `Tools/publish_levels.py --game <game>` in the background. The
   toolbar shows "Published r3, up to date" or "changed since". A failing level stops the publish; the Console lists it.
3. The script commits the level files under `tessera/levels/<game>/` in the portfolio repo, then the manifest pointing
   at that commit on jsDelivr with the revision raised by one, and pushes. Nothing changed means nothing is pushed.
   After the push it writes `Content/published.json` (revision and play order). Commit that file with the levels.
   If the push fails, the commits stay in the Pages repo and the next Publish pushes them.

The same works from a terminal: Tileworks > Levels > Export for Publishing (all games), then `python3
Tools/publish_levels.py` (`--game`, `--no-push`, `--repo`, `--no-warm`).

Players get the update on their next start, or on their next return to Home if it arrives mid-session. GitHub Pages
caches the manifest for up to 10 minutes. `Tools/deploy_pages.py` leaves `tessera/levels/` alone. When the game page
moves to `/tileworlds/` at the next deploy, the levels stay under `tessera/levels/`, because installed games read them there.

## Builds and published levels
Every build ships every level in the content folder, published or not, as the studios we looked at do. Level files are
a few KB, so players get them at install and play offline from the first session. Downloads only cover what changed
after the build.

A build records the revision its levels match (`LevelPack.PublishedRevision`, from `published.json`, set by
`LevelPackBuildStamp` before each build). The client ignores manifests at or below that revision, and drops a cache
from an older one. So a new build never goes back to an older published set, even when it has levels that were never
published. Publish after a release to bring older installs up to the same set.

## Format
`tessera/levels/<game>/manifest.json`:
```json
{ "schema": 1, "game": "tilematch", "revision": 3,
  "files": "https://cdn.jsdelivr.net/gh/ocebi/ocebi.github.io@<commit>/tessera/levels/tilematch",
  "levels": [ { "id": "tm_0001", "hash": "c19a884548c0922f", "payloadVersion": 1, "reward": 10 }, ... ] }
```
`levels` is the play order. Level files are the usual envelopes, at `<files>/<id>.json`, with a copy next to the
manifest as the fallback.

## Client (`Tileworks.Tiles.Levels`)
- `LevelUpdateStep` (a boot step, not critical) runs `LevelUpdater`. Boot waits up to `bootWaitMs` (1.5 s). A slower
  update keeps running and lands on the next Home. Returning to the app after `refreshMinutes` checks again.
- `LevelUpdater`:
  - Takes a manifest only when its revision is higher than the one in use, so a stale CDN copy can't roll a player
    back.
  - Downloads only levels whose hash differs from both the pack and the cache, at most 4 at a time, with retries on
    timeouts and 5xx answers. A file jsDelivr won't send comes from the copy next to the manifest.
  - Checks every file: id, game, content hash against the manifest, payload version, and the game's validation
    (`ILevelChecker`). A failing level is dropped, or stays at its built-in version when the pack has it.
- `LevelCache` keeps accepted files in `<profile>/levels/<game>/` as `<id>.<hash>.json`. `index.json` is written last
  and names the current set, so an interrupted update leaves the previous one whole. Manifest ids and hashes are
  restricted to file-name characters.
- `LiveLevels` is the game's `ILevelSource` and `ILevelSequence`. It applies an accepted update when the app enters
  Home (`IAppStateListener`), never during a level, and clears the `LevelRepository` cache when it does.
- Progress is a level number, so a new order never moves a player back. Past the end, the sequence replays its last
  fifth, as before.
- Off in the Editor (`_inEditor`), so the Editor plays the files on disk rather than a published copy.

## Status
- Wired into all four games through `Tileworks.Tiles.Installer.LevelsInstaller`, which each game's App installer calls
  with its pack, the shared `RemoteLevels.asset` and its level checker.
- Checked on 16 Oct:
  - Unit tests (`RemoteLevelsTests`).
  - A Play-mode run against a local server for Tile Match: one appended level was downloaded, then used from the cache
    with no download, then used offline.
  - The same run for Blast, Gem Swap and Block Puzzle: each downloaded only its one appended level and played revision
    1 with 101 levels; Tile Match, with nothing published, kept its built-in levels.
  - The Publish flow against a scratch repo pushing to a local bare repo: r1; nothing changed; a reward change gave r2.
- Only the repo owner can push to the portfolio repo, so only the owner can publish. Signing manifests (a public key in
  the app) would also guard against a compromised host or account; not done.
