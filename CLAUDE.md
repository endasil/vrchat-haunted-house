# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A VRChat haunted house stealth game built in Unity with UdonSharp. Players collect colored pills (the keys), avoid ghost AIs, and reach the exit. All gameplay scripts are in `Assets/_3DStealthGame/Scripts/`.

There are no CLI build, lint, or test commands — compilation happens inside the Unity Editor automatically when scripts are saved. UdonSharp scripts are compiled by UdonSharp on domain reload.

## VRChat / UdonSharp Constraints

These are hard limitations imposed by the Udon VM — violating them produces compiler errors or silent failures at runtime:

- **No static fields (including `static readonly`).** A field is storage that has to exist while the world runs, and Udon has no place to put it. Put the value on an instance instead, or make it a `const`.
- **Static methods in a plain (non-behaviour) helper class do work.** They are only code, so UdonSharp compiles them into the caller and nothing shared has to exist at runtime. See `TimeFormatter.cs`. Such a file is only a `.cs` plus a `.meta`; it needs no UdonSharpProgramAsset.
- **A `static class` holding only `const` values is fine**, and is how a string or number shared by two behaviours should be written. A `const` is resolved at compile time: the compiler replaces `PlayerDataKeys.BestEscapeTime` with the literal `"haunted.bestEscapeTime"` everywhere it is written, so no reference to the class survives into the code UdonSharp compiles. See `Constants/AwarenessIndicator.cs` and `Constants/PlayerDataKeys.cs`.
- **No `System.Linq`, `System.Threading.Tasks`, or other unsupported .NET APIs** inside UdonSharp behaviour files. These are fine in editor-only (`#if UNITY_EDITOR`) code.
- **No generic collections** (`List<T>`, `Dictionary<K,V>`). Use fixed-size arrays.
- **Deferred calls** use `SendCustomEventDelayedFrames(nameof(MyMethod), n)` — not coroutines.
- **Network authority**: AI logic that must be consistent across clients runs only on the owner: `if (!Networking.IsOwner(gameObject)) return;`
- **Player-attached scripts** (like `PlayerInventory`) are retrieved via `player.GetPlayerObjects()[0].GetComponent<T>()`, not `FindObjectOfType`.
- **NavMeshAgent** is supported from VRChat Worlds SDK ≥ 3.7.4 (this project uses 3.10.1).

## Architecture

### Inheritance / Reset System

`Resettable` (→ `UdonSharpBehaviour`) is the base class for any object that needs to be reset between attempts. On `Start()` it self-registers with the `ResetManager` singleton; subclasses override `public virtual void ResetState()`, and `ResetManager.ResetAll()` calls it on each registered behaviour. `GameEndingU` calls `ResetAll()` when the caught-screen fade finishes (per-client — everything resettable is sync mode None).

Concrete resettables: `PlayerInventory`, `PlayerUI`, `DoorU`, `Pill`, `GargoyleU`, `ExitPillSockets`.

### Ghost AIs

`GhostAgentBase` holds what the ghosts share: owner-only `NavMeshAgent` handling (with an `OnBecameAgentOwner` hook for ownership handoffs), player scanning (`FindClosestVisiblePlayer` = distance + vision cone + line-of-sight raycast; `FindClosestPlayerWithin` = plain radius for hearing), `SetDestinationOnNavMesh` (snaps points onto the mesh first), turn helpers, and the indicator text. It is never attached directly, so it has no UdonSharpProgramAsset (same as `Resettable`).

- **`SeekerGhost`** — the seeker. Patrols to random NavMesh positions, chases when a player is seen or heard, investigates the last known position after losing them, then resumes patrol with an idle look-around sweep at each stop. Receives `[NetworkCallable] OnSnowballHit(Vector3)` from `Snowball` (sent to the ghost's owner) and investigates the thrower's position. A snowball that hits anything else calls `[NetworkCallable] OnSnowballImpact(Vector3)` on every seeker within `Snowball.seekerAlertRadius` (found with `Physics.OverlapSphere`); the seeker walks to the impact point if the point is within `hearingRange` or inside its vision cone with line of sight, unless it is chasing a player it detects.
- **`ButlerGhost`** — the butler. Walks a fixed `WaypointNetwork` loop and never scans for players. A snowball hit sends `[NetworkCallable] OnSnowballHit(int throwerPlayerId)` to its owner: if the thrower is more than `TurnInPlaceStartAngle` (90°) off its forward, it stops and turns in place until within `TurnInPlaceEndAngle` (10°), the same turn as the seeker; it then chases that player for `snowballChaseTime` seconds, then returns to its route. It carries the id rather than a position because the butler follows a moving player. The prefab root needs its solid `CapsuleCollider` for snowballs to hit it at all. The patrol index is `[UdonSynced]`.
- **`GargoyleU`** — the gargoyle statue (`ObserverU` and the editor map generator also find gargoyles by it). `Snowball.OnCollisionEnter` calls `FadeOut()` on the thrower's client only: it switches off the `ObserverU` child and the solid collider, swaps `Gargoyle.mat` for `Gargoyle Fade.mat`, fades the alpha over `fadeDuration`, then hides the renderers. `ResetState` restores everything.

AI logic runs exclusively on the owner client. Catches are local: each ghost carries an `ObserverU` trigger that raycasts at the local player (a hit with `collider == null` is the player — VRChat hides player colliders) and calls `GameEndingU.CaughtPlayerBy(ghostType)`, which shows the caught overlay, writes the death message, teleports the player to spawn and triggers the reset.

### Pill / Door System

`PillColor` enum (in `Assets._3DStealthGame.Scripts` namespace, with a `LastEnum` count sentinel) drives a typed inventory stored as an enum-indexed array. `Pill` adds itself via `PlayerInventory.AddPill(pillColor)` on trigger enter and deactivates. `DoorU` checks `PlayerInventory.HasPill(pillColor)` on trigger enter and animates open (pills are not consumed).

The exit is two gold `Door_Post Exit` doors behind the `ExitTrigger` (`ExitTriggerU` ends the run only when `HasAllPills()` is true). A dark bar across them (`ExitPillSockets`) holds one colored disc per `PillColor`. `ExitPillSockets.OnPlayerRestored` passes itself to the local player's inventory with `SetExitPillSockets`. `PlayerInventory.AddPill` then calls `ExitPillSockets.ShowPill`, which shows a pill model on the disc of that color. `ExitPillSockets.ResetState` hides the pill models.

### Player

`PlayerInventory` (extends `Resettable`) lives on the player avatar's first player object and exposes `AddPill`, `HasPill`, `HasAllPills`.

`PlayerUI` (also `Resettable`) is on the head-locked HUD canvas GameObject and owns everything drawn on it: the pill row (`pillIcons`, one `Image` per pill color, indexed by `(int)PillColor`), the escape timer text, and the full-screen caught/end panels (`blackDeathBackground`, `endScreen`, faded by `GameEndingU`).

The two hook up in `PlayerUI.OnPlayerRestored`: it grabs `PlayerInventory` off the local player's first player object, calls `playerInventory.SetPlayerUI(this)` so `AddPill` can push straight to `ShowPillIcon`, then `RefreshPillIcons()` once to catch up on pills already held.

`LateUpdate` re-places the canvas in front of the head every frame and pins the pill row and timer to fixed spots in view. The caught/end panels are only re-fitted when field of view or aspect changes. VRChat can't draw screen-space canvases in VR, so the HUD is a world-space canvas and all placement is worked out from the field of view: on desktop from `VRCCameraSettings.ScreenCamera`, in VR from the `vrFieldOfView` / `vrHudFieldOfView` inspector fields.
