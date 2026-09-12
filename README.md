# CupkekGames EditorTools

Editor productivity windows extracted from HeroManager (Phase A, 2026-04-30).

## What's inside

**Editor** (`CupkekGames.EditorTools.Editor.asmdef`)
- `ScriptableObjectCreatorWindow` — `Assets > Create > Create Scriptable Object` window for grouped SO creation
- `FuzzySearch` — generic fuzzy-search utility (used by SO creator and scene quick swap)
- `MainToolbarButtons` — Project Settings + Reset Timescale buttons in main editor toolbar
- `MainToolbarElementStyler` — toolbar styling
- `MainToolbarTimescaleSlider` — timescale slider in main editor toolbar
- `MainToolbarSceneSwap` — scene quick-swap dropdown in main editor toolbar; label shows the open scene
- `SceneQuickSwap` / `SceneQuickSwapPopup` — scene index, per-project favorites + recents, searchable dropdown

## Dependencies

- `com.cupkekgames.editorui`

## Scene quick swap

The `Scenes` dropdown in the main toolbar opens any project scene in edit mode.

- Label shows the active scene, plus `+N` when other scenes are loaded additively.
- Lists favorites, the last 5 opened scenes, then every scene under `Assets/` except `Assets/ThirdParty/` and `Assets/Vault/`.
- Type to fuzzy-filter by scene name, then by folder. Arrow keys move, Enter opens, Esc closes.
- Click or Enter opens single (prompting to save first). Ctrl-click or Ctrl+Enter opens additive. Alt-click pings the asset.
- The star on each row toggles a favorite. Favorites and recents are stored per project, by scene GUID, so moves and renames survive.
- Disabled during play mode.

Replaces the `SceneBootstrapper` play-mode scene loader removed in 0.3.0.
