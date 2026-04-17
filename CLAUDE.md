# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A VRChat haunted house stealth game built in Unity with UdonSharp. Players collect keys, avoid a ghost AI, and reach the exit. All gameplay scripts are in `Assets/_3DStealthGame/Scripts/`.

There are no CLI build, lint, or test commands — compilation happens inside the Unity Editor automatically when scripts are saved. UdonSharp scripts are compiled by UdonSharp on domain reload.

## VRChat / UdonSharp Constraints

These are hard limitations imposed by the Udon VM — violating them produces compiler errors or silent failures at runtime:

- **No user-defined static classes or static methods.** Use instance methods instead. (A past comment in `LightFlickerU.cs` explicitly notes: "Removed static here since VR Chat does not support it.")
- **No `System.Linq`, `System.Threading.Tasks`, or other unsupported .NET APIs** inside UdonSharp behaviour files. These are fine in editor-only (`#if UNITY_EDITOR`) code.
- **No generic collections** (`List<T>`, `Dictionary<K,V>`). Use fixed-size arrays.
- **Deferred calls** use `SendCustomEventDelayedFrames(nameof(MyMethod), n)` — not coroutines.
- **Network authority**: AI logic that must be consistent across clients runs only on the owner: `if (!Networking.IsOwner(gameObject)) return;`
- **Player-attached scripts** (like `PlayerMovementU`) are retrieved via `player.GetPlayerObjects()[0].GetComponent<T>()`, not `FindObjectOfType`.
- **NavMeshAgent** is supported from VRChat Worlds SDK ≥ 3.7.4 (this project uses 3.10.1).

## Architecture

### Inheritance / Reset System

`Resettable` (→ `UdonSharpBehaviour`) is the base class for any object that needs to be reset between attempts. On `Start()` it self-registers with the `ResetManager` singleton. `ResetManager.ResetAll()` iterates registered behaviours and calls `ResetState()` on each via `SendCustomEvent("ResetState")`.

Concrete resettables: `PlayerMovementU`, `DoorU`, `KeyU`.

### Ghost AI (`GhostAISearching.cs`)

The main AI behaviour. Uses a `NavMeshAgent` for pathfinding. Three modes stored in `AIBehavior` enum:
- `RandomWalk` — patrol to random NavMesh positions
- `FollowPlayer` — always chase the nearest valid player
- `WalkNChase` — patrol and interrupt when a player is detected

Detection uses a vision cone (angle + raycast occlusion check) and a hearing radius. On arrival at a waypoint the ghost performs an idle look-around sweep before selecting a new destination.

Runs exclusively on the owner client (`Networking.IsOwner` guard in `Update`).

### Key / Door System

`KeyType` enum (in `Assets._3DStealthGame.Scripts` namespace) drives a typed inventory. `KeyU` adds a key to `PlayerMovementU.keyCounts[]` on trigger enter. `DoorU` consumes a matching key via `PlayerMovementU.UseKey(keyType)` on trigger enter, then animates open.

### Player

`PlayerMovementU` (extends `Resettable`) lives on the player avatar's first player object. It holds the key inventory and exposes `AddKey`, `UseKey`, `GetKeyCount`, and `GetKeyCountsString`. `PlayerUI` reads `GetKeyCountsString()` each `LateUpdate` and displays it via TextMeshPro; it polls via `SendCustomEventDelayedFrames` until it successfully locates `PlayerMovementU` on the local player object.

### Namespaces

- Most game scripts: global namespace.
- `LightFlickerU` + `FlickerMode` enum: `StealthGame` namespace (also contains editor code under `#if UNITY_EDITOR`).
- `KeyType`, `KeyTypeHelper`, `AIBehavior`: `Assets._3DStealthGame.Scripts` namespace.
- `AwarenessIndicator`, `IdleLookAroundState`: `Assets._3DStealthGame.Scripts.Enums` namespace.

### VPM Dependencies

- `com.vrchat.worlds` 3.10.1 (VRChat SDK)
- `com.mmmaellon.smartobjectsync` 3.10.16
- `bobystarvrc.opennid` 1.0.0
