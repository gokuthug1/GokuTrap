# Feature delivery and verification

## Acceptance criteria and integration map

- Preserve `net6.0-windows`, WPF/WinForms, Wpf.Ui, existing settings JSON and launcher modes. No elevation, secrets, binaries, copyrighted recordings or additional network providers.
- Settings live in `Settings.json`, safe defaults in `FeatureSettings`; public identities only for accounts. Restore journals for catalog values and sound ownership persist with settings.
- UI: discoverable Saiyan / feature tools navigation; sound packs reachable from Mods; seven functional areas with resource-based text and asynchronous commands.
- `App` owns services and shared HTTP; `Watcher` owns runtime process/overlay changes; `Bootstrapper.ApplyModifications` owns client asset apply/restore; `Bootstrapper.CheckForUpdates` uses the existing `-upgrade` installer handoff.
- `ActivityWatcher.OnGameJoin/OnGameLeave` supplies observed public place context via an atomic, non-secret `PlayerActivity.json` snapshot, separate from user state to prevent watcher refreshes overwriting settings-window state. Radar must cross-check discovered IDs against Roblox's public server list, confirm before handoff, and never claim country discovery proves city location or join success.
- Catalog: repository HTTPS JSON, schema 1, <=256 KiB, duplicate rejection, known keys/ranges, bundled offline fallback, diff and conflict-aware undo. Unrelated values remain untouched.
- Sound imports: managed copies <=20 MiB, format signature/readability validation, MP3 only for existing `.mp3` client slots, no transcoding by renaming, preserve unrelated mods.
- Updater: deterministic Windows x64 executable selection, <=512 MiB, HTTPS approved hosts including redirect destination, SHA-256 metadata verification where available, PE x64 validation, temporary staging, single-flight/cancel/retry, atomic replacement plus backup. No executing release notes.
- Verification: Release solution build; dependency-free Windows STA verification harness for pure logic, backward compatibility, hostile input, restore conflicts and actual WPF controls. Real Roblox/auth/update installation and multi-monitor interaction require separate opt-in manual validation.

## Documented boundaries

- Microsoft `GetSystemCpuSetInformation` reports group/core/efficiency classes. Higher efficiency-class values identify faster, less power-efficient processors only when distinct classes exist. No AMD X3D CCD inference. Affinity is offered only with complete single-group topology.
- Microsoft `EmptyWorkingSet` removes resident working-set pages; it is not heap compaction or garbage collection and can cause paging/stutter. Manual only, never a cleaner loop.
- The existing current-user DPAPI cookie reader is used only with explicit cookie-access opt-in for authenticated GokuTrap requests. Profiles retain no session material. Browser sign-in alone may not change Roblox desktop authentication; refresh after signing in in the official client.
- Roblox does not expose a supported seamless desktop session-switch API here. Selection is not authentication; authenticated identity must be revalidated, and public avatar refresh must not mark a session valid.
- No integrated non-invasive PresentMon/ETW source: FPS/frametime telemetry is unavailable. The overlay is external, click-through, and not guaranteed over exclusive fullscreen.
- No documented region-specific Roblox probe endpoints: US East/West, Frankfurt, Singapore and Tokyo latency values remain explicitly unavailable. Country discovery cannot prove a city or select a fastest region. Manual verified country server discovery is the supported alternative.
- Roblox's [local FastFlag allowlist](https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569) includes `FIntDebugForceMSAASamples` and `DFIntDebugFRMQualityLevelOverride` (reviewed 2026-10-05). Effects depend on client/renderer; no FPS, network latency or power guarantee.
- Digest metadata comes from the same GitHub release API as the asset: SHA-256 verifies consistency, not an independent publisher signature. Without a digest only HTTPS, length and executable-format checks apply.

## Verification results

Baseline: `dotnet build GokuTrap.sln -c Release --no-restore` failed with CS0051 in the pre-existing draft overlay (`SetBounds` exposed an internal RECT). SDK 10.0.102; Windows x64 with .NET 6 desktop runtime installed. No AGENTS.md in this repository; README reviewed.

Final verification on Windows 11 x64 (SDK 10.0.102, .NET 6 desktop runtime):

- Release solution build succeeds with no errors. Remaining warnings: NETSDK1138 (.NET 6 EOL) and WFAC010 (WinForms analyzer recommends a startup DPI API; the WPF-owned application intentionally uses the per-monitor manifest). No warnings were suppressed.
- `dotnet run --project tests/GokuTrap.FeatureChecks -c Release -- .`: 69 focused checks, including settings defaults/null migration, stale-auth and avatar sanitization, malformed/duplicate/oversize catalog rejection, idempotence/conflict-aware revert, release tag/asset/digest/PE validation, unsafe-URL failure, atomic replacement/rollback preservation, local sound ownership/path/header validation, non-Player rejection, truthful radar/stale candidate states, real seven-tab WPF loading/bindings, AutomationPeer invocation of the memory button, actual diff list binding, native overlay click-through/non-activation and hotkey parsing.
- This machine reports 8 logical CPUs / 4 cores / 1 group with no distinct efficiency classes; automatic faster-class selection correctly remains unavailable.
- Resource XML validation passes: unique resource names and all 155 literal feature localization references resolve. `git diff --check` passes. Existing pre-task draft changes were extended, not discarded. No commits, publishing, real-account sign-in, Roblox launch/hop or installed-version upgrade performed.
- CI now builds and runs the same dependency-free STA checks. CI itself was not executed here.

The initial UI test found a read-only progress binding configured as TwoWay; fixed with OneWay. Tab teardown also exposed the existing enum converter's unsafe cast; fixed by handling non-enum transient contexts. Both affected checks were rerun.

### Final acceptance repairs

- Windows-default scheduling now performs no priority/affinity writes. Priority-only management never touches affinity, including on grouped systems; only changed fields are restored. Exited process journals are pruned.
- Radar uses response-header streaming before its 256 KiB cap, reads activity without recovery writes, and checks the observed place again before handoff. An atomic dedicated activity snapshot avoids stale watcher writes over unrelated user state.
- Catalog refresh persists version/time metadata immediately, validates redirect HTTPS, and bounds streamed read time. Updater bounds download lifetime, cleans failed handoff locks independently of progress, and compares omitted trailing version zeros correctly.
- Runtime reload updates only runtime controls, not unrelated in-memory settings. Local validation failures surface their localized actionable messages.

### Not verified / manual acceptance

1. Start Player through GokuTrap; confirm AboveNormal/High/explicit-affinity on every verified instance, then disable and confirm original values restore. Test denied access, client exit/restart and manager shutdown. Forced process termination cannot guarantee restoration; restarting Player resets its scheduling.
2. Preview genuinely decodable licensed MP3/WAV and optional codec formats; launch to verify action-slot playback and package-based original restoration. Header validation does not certify the entire audio stream. Experiences can override these assets.
3. Test official sign-in handoff, desktop reauthentication, expired/revoked cookies, account thumbnails and cookie-access disable using an authorized test account. No browser stores or protected cookie files are modified. Refresh is explicit; there is no covert login polling.
4. Publish the catalog in the repository before expecting remote refresh success. Verify offline/404/rate-limit responses retain the bundled/cache catalog. Catalog text from remote data is shown as untrusted plain text, with local safety explanations.
5. Join a public place, verify country-discovered IDs against official server data, confirm hop, and verify full/stale/private server behavior. Only the first 100 official public servers are checked; no claim of exhaustive discovery or fastest-region ranking.
6. Test overlay visibility and positioning on mixed-DPI/negative-origin monitors, minimize/focus changes, hotkey conflicts and exclusive fullscreen. Native style flags and actual WPF windows were tested, not real game input routing.
7. In a disposable installation, test staged download cancellation/interruption, digest/length/version mismatch, upgrade/restart, sharing violations and recovery from `GokuTrap.exe.rollback`. Atomic file replacement and exact backup bytes were tested on temporary files; a live release install was not. Rollback is retained until overwritten by the next successful upgrade; restoration is explicit/manual, not an automatic crash detector.

### Remaining scope boundaries

All seven areas have integrated controls and implementation/safe fallback. Seamless session switching, event-aware heap compaction, regional ping/automatic best-region hop, FPS/frametime and game-specific respawn/teleport/hit audio are deliberately unavailable. The overlay is static (the request permits static or simple animated styles). English resource fallback is delivered; translations of the new keys remain a localization task. Settings/window page search uses the existing OptionControl-only search index, so the new tools are discoverable by navigation and Mods entry rather than each tab being indexed. Multiple Player instances receive scheduling policy, but activity/radar context remains the existing singleton watcher's instance.
