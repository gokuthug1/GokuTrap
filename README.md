# GokuTrap

> [!CAUTION]
> The only official places to download GokuTrap are this GitHub repository and
> our official releases. Any other websites offering downloads
> or claiming to be us are not controlled by us; do not download from them.

<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Images/GokuTrap-Dark.png">
  <source media="(prefers-color-scheme: light)" srcset="Images/GokuTrap-Light.png">
  <img alt="GokuTrap Banner" src="Images/GokuTrap-Dark.png" width="100%">
</picture>

<br/><br/>

![License][badge-license]
![Builds][badge-actions]
[![Latest Release][badge-latest]][repo-latest]
![Stars][badge-stars]

</div>

**GokuTrap** is an all-in-one modern custom bootstrapper and launcher for Roblox, built to give you total control over your game client, appearance, performance, and integrations.

If you encounter any bugs, please [open an issue here][repo-new-issue].

> [!NOTE]
> GokuTrap is an application for **Windows 10 and above (64-bit).**

---

## 📸 Showcase

<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Images/Showcase-Dark.png">
  <source media="(prefers-color-scheme: light)" srcset="Images/Showcase-Light.png">
  <img alt="GokuTrap Showcase" src="Images/Showcase-Dark.png" width="85%">
</picture>

</div>

---

## ⚡ Features

- **Custom Bootstrapper Styles & Theming**
  - Choose between modern Fluent, glass/acrylic, terminal, legacy, or custom XML-authored bootstrapper designs.
  - Custom branding and launcher icons (GokuTrap signature, Ultra Instinct alternate, legacy eras, or custom .ico files).
- **FastFlags Management & Presets**
  - Unlock framerates, tweak rendering, adjust display scaling, and manage game engine flags directly.
- **Global Basic Settings (GBS) Editor**
  - Pre-configure in-game graphics quality, framerate caps, UI transparency, reduced motion, and mouse sensitivity.
- **Rich Activity & Integrations**
  - Discord Rich Presence with game details, place information, and deep-link join support.
  - Server location and datacenter queries with map display.
  - Multi-instance launch support and background Roblox optimization.
- **Client Modifications (Mods)**
  - Seamlessly apply custom fonts, cursor packs (2006, 2013, or custom), classic character sounds (OOF, classic jump/walk), and custom skyboxes.
- **Channel Switching & Version Control**
  - Switch release channels and force deployment versions effortlessly.
- **Diagnostic & Maintenance Tools**
  - Built-in cache cleaner, log exporter, and automated migration from older launchers (Bloxstrap / Fishstrap).

---

## Saiyan & feature tools

Open **Saiyan & feature tools** in Settings. Sound packs are also reachable from **Mods → Sound packs**. Feature Apply buttons persist immediately; existing launcher settings retain the normal Save flow.

- **Saiyan Mode:** opt-in Above Normal or advanced High priority for verified installed Roblox Player processes only. Windows-reported core/group/efficiency topology, optional valid hexadecimal affinity, conditional faster-class selection, memory measurements and a confirmed one-time working-set trim. Reset disables management and restores the original policy. Trimming may cause paging/stutter; no heap compaction or automatic cleaner.
- **Sound packs:** browse local audio (up to 20 MiB), managed copy, preview, enable/disable and remove/restore. Existing jump, footsteps and get-up `.mp3` slots accept MP3 only; launcher startup/success cues support MP3/WAV/OGG/M4A subject to Windows codecs. Instant Transmission / Super Saiyan is a theme definition, **not bundled copyrighted audio**. Import only content you have rights to use. Conflicting user mods are preserved; client restoration occurs on the next launch. No respawn/teleport/hit-event claim.
- **Account profiles:** public user ID, display name, avatar, nickname, selected-profile indicator and separately validated desktop identity. Add public profiles without credentials, or explicitly opt into the existing DPAPI cookie reader to validate the current desktop account. Selection is **not session switching**. Use the confirmed official sign-in handoff, complete desktop sign-in if necessary, then Refresh. No credentials are stored in profiles and Roblox's protected cookie store is never modified.
- **Community Hub:** three rendering-only presets, exact diff, explicit apply and conflict-aware restore of previous values. Bounded schema-validated repository HTTPS catalog with bundled/cache fallback. Roblox allowlist reviewed 2026-10-05; effects remain client/renderer dependent. No measured FPS, power or network-latency promises. The remote URL will become available only once the catalog is published in this repository; offline presets work now.
- **Region radar:** five named regions with honest **latency unavailable / not measured** states. No supported regional Roblox probe exists here, so automatic fastest-region ranking is unavailable. Explicit country-server discovery cross-checks candidates against Roblox's public server API, uses recently observed public-place context, and confirms before official join handoff. Country data cannot prove Frankfurt/Tokyo city location or distinguish US East/West. Roblox decides final join success.
- **External overlay:** disabled by default; static cross/dot/circle, color, size, opacity, position offsets and configurable modifier hotkey. Click-through/non-activating; hides when its Player is absent/minimized/not foreground. No injection, graphics hooks, aiming or automation. Use only where permitted; exclusive fullscreen is not guaranteed. FPS/frametime is unavailable rather than fabricated.
- **GitHub updater:** one shared existing upgrade flow, manual check/retry, configurable automatic cadence, prerelease opt-in, release notes, cancellable staged download, deterministic x64 asset/PE/version/size checks and published SHA-256 verification when available. Digest metadata is not an independent publisher signature. Quiet mode never prompts/installs. Close other GokuTrap instances to update. Atomic replacement keeps `GokuTrap.exe.rollback`; recovery instructions are in the Updates tab. Cancel/interruption leaves the installed executable intact; retry replaces incomplete staging.

Runtime settings are picked up by the existing Player watcher within five seconds. All verified running Player instances receive the scheduling policy; the external overlay follows one window. Multiple-instance activity context is limited to the watcher's observed instance. Normal shutdown restores captured policies; forced termination cannot guarantee restoration—restart Player if needed.

See [delivery notes and verification](docs/FEATURE_DELIVERY.md) for implementation boundaries and manual checks. New strings use the existing resource manager; locales without translations fall back to English.

## 🚀 Building from Source

### Prerequisites
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) or higher (.NET 10 compatible)
- Windows 10/11 x64

### Build Command
```bash
dotnet build GokuTrap.sln -c Release
```

### Focused feature / WPF checks
```bash
dotnet run --project tests/GokuTrap.FeatureChecks -c Release -- .
```
Runs in isolated temporary storage on Windows, without launching Roblox, signing in, modifying the real installation or downloading updates. Includes actual WPF tab/binding and click-through window checks. CI runs the same harness. .NET 6 is out of support; the supported project target is intentionally unchanged.

### Publish Single-File Executable
```bash
dotnet publish GokuTrap/GokuTrap.csproj -p:PublishSingleFile=true -r win-x64 -c Release --self-contained false
```
The output binary will be located in `GokuTrap/bin/Release/net6.0-windows/win-x64/publish/GokuTrap.exe`.

---

## 🎨 Asset Management

GokuTrap includes a self-contained Python asset pipeline that synchronizes and generates all multi-size `.ico` packages and showcase mockups from source:

```bash
python Scripts/GenerateAssets.py
```

---

[badge-license]:   https://img.shields.io/github/license/gokuthug1/GokuTrap?style=flat-square
[badge-actions]:   https://img.shields.io/github/actions/workflow/status/gokuthug1/GokuTrap/ci-release.yml?branch=main&style=flat-square&label=builds
[badge-latest]:    https://img.shields.io/github/v/release/gokuthug1/GokuTrap?style=flat-square&color=ff7b00
[badge-stars]:     https://img.shields.io/github/stars/gokuthug1/GokuTrap?style=flat-square&color=dd9900

[repo-latest]:    https://github.com/gokuthug1/GokuTrap/releases/latest
[repo-new-issue]: https://github.com/gokuthug1/GokuTrap/issues/new/choose
