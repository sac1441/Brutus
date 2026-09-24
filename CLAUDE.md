# CLAUDE.md — Brutus

Guidance for Claude (and any AI assistant) working in this repo.

## Project
- **Brutus** — 2D hypercasual platform climber for **Android**, by Grinding Gate.
- **Unity 6.3 LTS (6000.3.x)**, URP 17.3, new Input System. Also runs Unity MCP (`com.coplaydev.unity-mcp`).
- Google Play production access granted; ads via Google Mobile Ads (`AdManager.cs`).

## Layout
- `Assets/_My Files/` — all our own content
  - `01 Scenes/` — build order: `Opening` → `ManualScenes_Level25` → `ManualScenes_Endless` (`ManualScenes` = original hand-built level, kept as backup; ~56 MB, watch GitHub's 100 MB limit)
  - `03 Scripts/` — `Level/` (EndlessLevel, BackdropStages, milestones), `Platforms/` (stack generator/resizer/exit trigger), `Managers/`, `Others/` (CameraFollow, ReviveUI, UI, environment FX), `Parallex/`, `Collectables/`, `Ads/`, `Dev/` + `Editor/` (tooling: autopilot, store screenshots, trailer recorder, colour grade snapshots)
  - `05 Prefabs/` — `VerticalStack`, `HorizontalStack`, `Platforms/Dragon/ScafoldSystem`, `Collectables/Egg`, `Environment/`
  - `levels.json`, `Player Controller.asset` (ScriptableStats tuning)
- `Assets/Tarodev 2D Controller/_Scripts/PlayerController.cs` — player (namespace `TarodevController`)

## How to work here
- **Use Unity MCP** to work directly in the editor (scene, scripts, assets). If Project-CH (the car game) is also open, make sure the Brutus instance is the active one first.
- **Every edit must be revertable**: before changing any script or asset, copy it to `ScriptBackups/` at the project root (outside Assets) with a timestamp, e.g. `PlayerController_20260924-211542.cs`. `ScriptBackups/` is git-ignored.
- Give **complete scripts**, not partial diffs or snippets.
- Keep answers concise and direct. Requests often come by voice, so they may be fragmented — interpret charitably, ask targeted questions if unclear.
- Prefer verified, tested logic over speed when debugging.
- **Check the actual scene hierarchy before writing hierarchy-traversal code** — wrong parent/child assumptions have caused repeated bugs.
- Unity auto-refresh is disabled; refresh/recompile explicitly after edits. APKs are deployed over wireless ADB.

## Gameplay rules & hard-won learnings
- **Checkpoints** are captured on **landing**, never on wall contact (otherwise mid-air positions get saved).
- Platform identity: `platform.name.TrimEnd().EndsWith("M")` — simpler and more reliable than hierarchy-based detection.
- Scaffold types use **Dragon Left/Right** naming, not generic wall tags.
- **Death**: position-based, not kill colliders — die when more than `fallDeathDistance` below the highest platform landed on, or beyond the current stack's platform X-extent by `sideDeathMargin`. Horizontal-stack air timer uses `airTime += Time.fixedDeltaTime`. Death plays a Mario-style animation (pop up, diagonal fall, no sprite flip) before the death UI.
- **Revive** = instant teleport (the rewind animation was tried and removed).
- Camera smoothing during animated sequences: `Mathf.SmoothDamp` with `Time.unscaledDeltaTime`, not per-frame snaps.
- Input: jump on touch and Space / Up / W.

## Level design rules
- **No dead ends**: from any platform Brutus can land on, there must always be a reachable platform above. Brutus auto-runs and reverses on wall contact, so every jump is diagonal and one diagonal run covers at most 4–5 platforms. Narrowing platforms or offsetting their X can break the path.
- Safe variety levers: platform count, stack-type sequencing (vertical / horizontal / scaffold), and which platforms get walls.
- **Eggs**: exactly one per 10-platform stack, on a mid platform, left- or right-justified (never centred, so eggs don't line up vertically).
- Endless mode (High Risers-style) lives in `ManualScenes_Endless`: chunks built from existing stacks, harder variants, backdrop stages, diagonal climbs.

## Git
- Branch: `master` → `origin` (github.com/sac1441/Brutus).
- Never commit: `user.keystore` / any keystore, `Screenshots/`, `ScriptBackups/`, `Recordings/`, `ProfilerCaptures/`, `Assets/_Recovery/`, `Library/`, `Temp/`, `Build/`, IDE folders (all in `.gitignore`).
