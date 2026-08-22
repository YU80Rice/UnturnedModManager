# Unturned Mod Manager (UMM) Domain Context

UnturnedModManager (UMM) is a modern, high-stability mod management platform and game launcher for Unturned, featuring 100% offline-autonomous physical file management, PCL2-inspired card UX, intelligent crash diagnostics, and open theme customization.

## Language

### Core Mod Management

**Profile**:
A named, physical snapshot of enabled and disabled plugins and their associated configuration files. Switching profiles physically renames or stages `.dll` / `.dll.disabled` and `.cfg` files without using virtual filesystems.
_Avoid_: Preset, config pack, virtual workspace

**ModPackage (`.ummpk`)**:
A standard ZIP archive containing a `manifest.json` metadata manifest, plugins (`BepInEx/plugins/**`), and configs (`BepInEx/config/**`) used for one-click sharing and multiplayer synchronization.
_Avoid_: Bundle, modpack archive, addon zip

**LocalMod**:
A BepInEx plugin DLL physically residing in the game's `BepInEx/plugins/` directory, managed via `.dll` / `.dll.disabled` suffix switching.
_Avoid_: Local assembly, plugin file

**CommunityMod**:
A curated Unturned mod distributed through `unmod.online` with verified dependency trees, semantic versioning, and download integrity hashes.
_Avoid_: Online addon, web mod

### Diagnostics & Safety

**DiagnosticAnalysis**:
An automated evaluation of game execution logs (`Client.log`, `LogOutput.log`, `doorstop.log`) matching specific failure patterns (`MissingDependency`, `BattlEyeConflict`, `DoorstopFailure`, `DxvkFailure`, `UnityCrash`) and generating actionable remediation advice.
_Avoid_: Log dump, crash check

**SanitizedReport**:
An exported diagnostic package where user home paths (`C:\Users\<USER>`) and sensitive auth tokens (`token=<REDACTED>`) have been scrubbed before sharing.
_Avoid_: Raw log export, bug report zip

**SandboxWhitelist**:
A strict destination boundary that allows unpacking files only into `BepInEx/plugins/**` and `BepInEx/config/**`, explicitly intercepting dangerous script/executable extensions and directory traversal attempts.
_Avoid_: File filter, extractor rule

### Customization & UI

**ThemePackage (`.ummtheme`)**:
A ZIP archive containing `theme.json` (defining accents, background tints, card opacity, and border radiuses) and background wallpaper assets for open UI personalization.
_Avoid_: Skin, style pack, UI template

**ThemePalette**:
A predefined, WCAG AA compliant color harmony (such as Fluent, WarmPaper, MascotOrange, MistyForest, OceanDusk, KleinBlue, Lavender) that dynamically injects accent and surface brush resources across all WPF views.
_Avoid_: Color scheme, preset colors

### Multiplayer & Ecosystem

**NetworkBridge (`LaunchMultiplayerNet`)**:
The fundamental Unturned multiplayer transport library facilitating bidirectional client-server communication channels across mods.
_Avoid_: Socket lib, net helper

**MultiplayerSync**:
The integrity validation and alignment of required network bridge libraries and mod versions between hosts and clients before joining a session.
_Avoid_: P2P connector, server joiner
