# NanoUintEditor

A visual scene editor for the NanoUint engine (WPF, net10.0-windows).

Unity-style three-pane layout (hierarchy / scene view / inspector) for composing visual novel screens on a
1920x1080 logical canvas (backgrounds, character sprites, buttons, dialogue boxes, choices, BGM, effects).
Saves to JSON scenes and can **generate C# code** (a `ScreenBase` subclass) or **preview live** (embedded engine play mode).

## Features

**Scene editing**
- 1920x1080 logical canvas + grid (32px/128px major-minor, toggleable) + zoom (25%-200%)
- Create/delete/duplicate/drag objects, resize with 8-direction handles, parent-child hierarchy (drag to reparent in the tree, or use the Inspector Parent dropdown)
- Multi-select (Ctrl+click) + marquee select (drag on empty space) + align/distribute/match-size toolbar and shortcuts
- Component-based objects: SpriteRenderer / TextRenderer / SpriteButton / PassiveButton / BackgroundRenderer /
  DialogueBox / ChoiceGroup / AudioSource / FlashOverlay / AdvanceIndicator / LineRenderer /
  HintRenderer / SDFTextRenderer (add/remove/edit properties in the Inspector)

**Assets**
- Asset panel browses `SteinsGateX/Resources` as a directory tree; double-click or drag to create a SpriteRenderer
- Drag images straight in from Explorer: copied into Resources automatically and turned into a SpriteRenderer
- Image decode cache (each path decodes once, so dragging and zooming stay smooth)

**Prefabs**
- "📦 Save prefab" stores the selected object (with subtree and components) as `saves/templates/*.json`
- "📦 Templates" node in the asset panel: double-click to instantiate (new Id + rebuilt hierarchy + centered), or drag to a position

**Play Mode**
- "▶ Play" opens a new window with the NanoUint engine rendering the current scene live (button clicks, typewriter, and animation run off the engine frame loop)
- The engine exposes the embedding API through `ScenePreviewHost` (`NanoUint/Rendering/ScenePreviewHost.cs`)

**Code generation**
- "⚡ Generate code" outputs a `ScreenBase.OnBuild()` subclass, aligned member by member with the real
  NanoUint/SteinsGateX API (`TextRenderer.Content/TextColor`, `SpriteRenderer.Tint`,
  `AssetDatabase.Load<Sprite|AudioClip>`, `DialogueBox.Show`, `ChoiceGroup.Show`,
  `AudioSource.IsLooping`, hex colors to `Color.FromRgb/FromRgba`, and so on)
- onClick event fields offer a dropdown of common script commands (selecting one inserts a template)

**Robustness**
- Undo/redo (Ctrl+Z / Ctrl+Y, snapshot-based, covering move/resize/add/remove/component/property/parent/prefab/align)
- Three-way parent cycle prevention (Inspector, hierarchy drag, load validation) + deleting a parent cascades to children + load repairs dangling references
- Crash-free save/load: TryGetProperty tolerance, version checks, exception prompts
- Dirty flag + unsaved-changes prompt on close; status bar shows `* unsaved`
- Recent files (MRU, max 8, persisted in AppData, quick-load dropdown in the toolbar)

## Shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+N | New scene |
| Ctrl+S | Save |
| Ctrl+Z / Ctrl+Y | Undo / redo |
| Del | Delete selection (multi-select included) |
| Ctrl+click | Toggle multi-select |
| Drag on empty space | Marquee select |
| Alt+← / → / ↑ / ↓ | Align left / right / top / bottom |
| Mouse wheel | Zoom |

## Workflow

```
Edit the scene (objects/components/layout)
  -> 💾 Save JSON (saves/*.json)
  -> ⚡ Generate code (paste into SteinsGateX/Screen/Impl) or ▶ Play to preview
```

## Build and Test

```bash
dotnet build          # the editor (depends on ..\NanoUint)
dotnet test           # NanoUintEditor.Tests (xunit, 32 tests)
```

The test project links the `SceneModel.cs` / `EditorUtils.cs` / `CodeGenerator.cs` / `UndoManager.cs` /
`AssetCache.cs` / `SceneRuntime.cs` sources and covers: hierarchy logic (cycle prevention/cascade/dangling
repair), code generation API alignment, color/identifier utilities, undo stack semantics, and play-mode
scene construction (STA integration).

## Project Structure

| File | Responsibility |
|---|---|
| `MainWindow.xaml(.cs)` | Main UI + scene view/inspector/hierarchy/asset panels (933 lines) |
| `SceneModel.cs` | Data model (SceneObject/ComponentData) + pure hierarchy logic (testable) |
| `ComponentRegistry.cs` | Component definitions and default property registry |
| `CodeGenerator.cs` | Scene -> C# code generator (aligned with the engine API) |
| `SceneRuntime.cs` | Scene -> engine runtime converter (play mode) |
| `AssetCache.cs` | Asset path resolution + image decode cache |
| `EditorUtils.cs` | Pure helper functions (identifiers/escaping/color parsing) |
| `UndoManager.cs` | Snapshot-based undo/redo |
| `ANALYSIS.md` | Analysis report (issue list / peer comparison / feature suggestions) |

## Related Projects

- `NanoUint` (engine): see `../NanoUint/README.md`
- `SteinsGateX` (game): `ScreenBase` lives in `SteinsGateX/Screen/`; paste generated code into
  `Screen/Impl/` and implement the game logic there (button callbacks and so on).
