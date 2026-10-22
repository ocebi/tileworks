# Web build on the portfolio site: plan

Research from 15 Oct, not implemented yet. Goal: a WebGL build of Tileworlds that loads fast and runs well in desktop and
mobile browsers, published on the portfolio site next to Casino Mares.

## Decisions
- **Local builds, no CI.** Build in the open Editor, then run a deploy script. `ARCHITECTURE.md` §18.2 (`build.yml`,
  `pages.yml`) predates this.
- **Hosted in the portfolio repo** (`~/Projects/ocebi.github.io`) under `tessera/`, served at
  `https://ocebi.github.io/tessera/`. The portfolio's own page is unaffected; the URL stays the same when the game's repo
  goes public. The next deploy moves the game to `tileworlds/` (`https://ocebi.github.io/tileworlds/`) and leaves a
  redirect page at `/tessera/`.
- **Same loading scheme as Casino Mares:** the page is on GitHub Pages, the build files come from jsDelivr pinned to the
  commit that holds them, and any file jsDelivr doesn't send comes from the page's own `Build/` copy on Pages.
- **One build with all three games.** Visitors download the engine once. `GameSwitcher` already lists every game whose
  scenes are in the build.

## Where the project stands
First all-games build on 15 Oct (menu: Tileworks > Build > All Games > WebGL (showcase)). It runs in desktop Chrome; all
four games load and play.
- Release build that keeps `TILEWORKS_DEV` (`BuildScript.Build(..., dev: true, development: false)`), every enabled scene
  with the hub first.
- Code Optimization: Disk Size. Disk Size with LTO was stopped after 20 minutes in the final link (one core, no
  progress output); without LTO the whole build takes about 5 minutes.
- Managed stripping Medium. `PreserveOwnCode` (an `IUnityLinkerProcessor`) keeps our own assemblies and sqlite-net
  whole, because the container and sqlite-net work by reflection.
- Textures ASTC, passed as the build's `subtarget` (the build settings value alone is ignored).
- Sizes: `.wasm` 51.5 MB uncompressed, over jsDelivr's 20 MB limit, so the build is gzipped with decompression
  fallback: `.wasm` 13.8 MB, `.data` 9.0 MB, the rest 0.1 MB; 21.9 MB in all, inside the 30 MB budget.
- Heap: about 96 MB after the hub and a game. Initial memory 128 MB, maximum 512 MB.
- `AppRoot` leaves `targetFrameRate` at -1 on the web, so the browser paces frames.
- The console shows two URP shaders (`CoreCopy`, `HDRDebugView`) as unsupported on WebGL 2; they aren't used.
- `Tools/serve_webgl.py` serves the build to this Mac and phones on the same Wi-Fi.
- Two menu builds: WebGL (release) → `Builds/WebGL/Tileworlds`, WebGL (showcase, keeps the debug tools) →
  `Builds/WebGL/Tileworlds-dev`.

## Build settings
1. **Release build** (no `BuildOptions.Development`) that keeps `TILEWORKS_DEV`. Split the two in `BuildScript`.
2. **Code Optimization: Disk Size with LTO** (`UnityEditor.WebGL.UserBuildSettings.codeOptimization`).
3. **Compression Format: Disabled.** Both hosts compress on the fly and the browser decompresses natively, so the
   WebAssembly compiles while it downloads (Unity's JavaScript decompressor blocks that). See "Hosting facts".
   - jsDelivr serves GitHub files only up to 20 MB, and with compression off that's the uncompressed size. If `.wasm`
     or `.data` is over it, use gzip with decompression fallback instead (what Casino Mares does).
4. **Managed stripping:** try Medium, with a `link.xml` or `[Preserve]` for what the container builds by reflection.
   Run the game to check nothing was stripped.
5. **Memory:** measure peak use in a release build, then set the initial size close to it and lower the maximum.
   Reports say iOS Safari can crash when the heap grows past 256 MB at runtime.
6. **Textures:** ASTC (phones decode it in hardware; Unity unpacks it on desktops without support). The art is small,
   so one data file should do. Casino's second ASTC data file is the fallback plan if desktops struggle.
7. **Frame rate on the web:** Unity recommends `vSyncCount` over `targetFrameRate` for smooth pacing in browsers.
   Compare both on a phone.
8. **Scenes:** a combined profile with all three games' scenes. The first scene decides which game opens; consider a
   `?game=` URL parameter.
9. **Budget:** under 30 MB download (`ARCHITECTURE.md` §1.3); the build report should print it.

## Page template
Ported on 15 Oct from Casino Mares' template (`~/Projects/CasinoMares/Assets/WebGLTemplates/CasinoMares/index.html`),
keeping the game's portrait layout, except the creeping loading bar. Added: phones in portrait fill the screen (the
column is capped at 62% of the height on touch devices, 50% on desktops), and phones held sideways see "Turn your phone
upright". iPhone Safari shows a "Play full screen" hint (Share, then Add to Home Screen) on the splash, which then stays
up at least 4 s; the manifest and icons (`TemplateData/icon-180.png`, `icon-512.png`, from `Hub/Art/app_icon.png`)
make the home screen version open standalone in portrait. A home screen app on iOS keeps its own storage, so progress
made in Safari doesn't carry over.
- `var buildUrl = "Build";` and `var mirrorUrl = "Build";` lines for the deploy script to rewrite.
- `window.fetch` wrapper: a build file that jsDelivr answers with an error, or doesn't start sending within the wait,
  loads from `mirrorUrl`. A 304 counts as success. Use a wait of about 8–10 s (Casino uses 25 s for bigger files).
- The loader is a `<script>` tag, so its fallback is `onerror` → load it from `mirrorUrl`.
- `cacheControl`: the data file is `"immutable"` when loaded from a commit-pinned URL, `"must-revalidate"` locally,
  everything else `"no-store"`.
- Loading bar that creeps up while a file waits for the fallback.
- `errorHandler` and `showBanner` show errors in the page instead of `alert()`.
- `config.devicePixelRatio = Math.min(devicePixelRatio, 2)` on touch devices.
- `webglcontextlost` → a "tap to reload" message.
- Reload when the page comes back to the front and the server has a newer build (ETag check).
- Skip what Tileworlds doesn't need: landscape lock, camera context menu, text-input key handling.

## Content (16 Oct)
The data file holds code, the entry scene and TMP's resources; every scene and its art, the fonts and the music are
Addressables bundles built by `BuildScript` (Remote profile) into `ServerData/WebGL` and copied to `Build/content/WebGL`.
The template sets `window.tileworksContentUrl` from `contentUrl` (same base as `buildUrl`, rewritten by the deploy script),
and `Tileworks.Kit.Content.ContentUrl.Remote` hands it to the Addressables load path at runtime. Bundles go through the
same `fetch` wrapper, so a bundle jsDelivr doesn't send comes from Pages. The catalog is local (`StreamingAssets/aa`),
pinned with the build: content and code always match. Run `Tileworks/Content/Set Up Groups` after adding a scene.

## Deploy script
`Tools/deploy_pages.py` (first deploy 16 Oct: https://ocebi.github.io/tessera/, files from jsDelivr, all four cached on
the first run). Modelled on `~/Projects/CasinoMares/Tools/deploy_pages.py`, with `tessera` as the site folder:
1. Fail if there is no build, or if any build file is over 20 MB.
2. `rsync --delete` the build into `ocebi.github.io/tessera/`, keeping the previous build's bundles for one deploy.
   The Addressables catalog (`StreamingAssets/aa/catalog.bin`) comes from Pages, unpinned and cached for 10 minutes,
   while the bundles are pinned to the commit. So for a while after a deploy, a browser can pair the new build with the
   old catalog. If the bundles that catalog names were deleted, the game hangs on the loader with a 404 (seen 18 Oct).
   Bundle names carry content hashes, so the script reads the deployed catalog and keeps the bundles it names that the
   new build lacks, in `Build/content/WebGL` and `StreamingAssets/aa/WebGL`. The next deploy prunes them. If that turns
   out awkward, the fallback is to pin the catalog with the build.
3. Commit the game files, then rewrite `buildUrl` to
   `https://cdn.jsdelivr.net/gh/ocebi/ocebi.github.io@<files commit>/tessera/Build` and commit the page.
   Fail if the page has no `mirrorUrl` line.
4. Push, then request each file from jsDelivr so it's cached before the first visitor. Retry a few times; a failure
   isn't fatal, since the page falls back to Pages.
5. Commit only those paths in the portfolio repo; it has unrelated untracked files.

Each deploy adds the whole build to the portfolio repo's history (its local `.git` is already about 290 MB from Casino
deploys; GitHub recommends under 1 GB). Deploy at milestones, not on every commit.

Also: a card on the portfolio's `index.html` linking to `/tessera/`. Give the game its own page rather than an
iframe; Safari has no IndexedDB in iframes, so saves would be lost.

## Hosting facts (measured 15 Oct)
| | GitHub Pages | jsDelivr (`/gh/`) |
|---|---|---|
| Raw `.wasm` | `application/wasm`, gzip on the fly | `application/wasm`, Brotli on the fly |
| Raw `.js`, binary (`octet-stream`) | gzip on the fly | Brotli on the fly |
| `.unityweb` (already compressed) | sent as is | sent as is |
| Cache | `max-age=600` | commit-pinned: 1 year, immutable; branch: 12 h |
| Limits | 1 GB site, 100 GB/month (soft) | 20 MB per GitHub file |

- Casino's 12.7 MB `.wasm`: GitHub Pages sent it in 1.0 s (12 MB/s) on this Mac's connection. jsDelivr answered 503
  ("No healthy backends") for its big files until they were cached again. That's why the Pages fallback exists.
- The reason for jsDelivr at all: Pages was about 100 KB/s on some networks.
- Unity 6.3 web support: iOS Safari 15+, Chrome 58+ on Android. WebGL 2 is the default; WebGPU is opt-in in 6.3.

## To verify on the first build
1. Release build size, file sizes against the 20 MB limit, and peak memory.
2. That both hosts also compress a `.wasm` over 10 MB on the fly (only checked on smaller files).
3. Saves persist in iOS Safari and Android Chrome.
4. Load time on a phone on mobile data, with jsDelivr cold and warm.
5. The game plays at a steady frame rate on a mid-range Android phone and an iPhone.
