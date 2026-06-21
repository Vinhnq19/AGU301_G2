# Foraging Redesign — Design

- **Date:** 2026-06-21
- **Feature:** Rework the harvest/foraging mechanic — aim-facing, damage-at-anim-end, movement lock, shorter range.
- **Status:** Approved (pending spec review)
- **Builds on:** `2026-06-21-player-animation-design.md` (the PlayerAnimation system)

## Goal

Make foraging feel like a committed swing: when the owner clicks a resource node, the player **locks movement**, **turns to face the node**, **plays the foraging animation**, and **deals damage only when the swing ends** (not instantly). The player must also stand **closer** (1.2 units) to interact.

## Context (current state)

- `PlayerAnimation` (already built): owner computes `FacingDir` + `AnimState` from `Rigidbody2D.velocity`, syncs via owner-authoritative `NetworkVariable`s; foraging is currently triggered by `InputReader.OnAttackPressed` with a fixed `_foragingDuration` timer and faces the **movement** direction.
- `HarvestToolBase.UseAction(targetPosition)` (`Assets/_Game/Scripts/Player/Tools/HarvestToolBase.cs`): owner-only; immediately `FindTarget` → `InteractWithNodeServerRpc` → server validates range + applies `harvestable.TakeDamageFrom(this)`. **Damage is instant.**
- `ToolController.UseCurrentTool()` → `GetTargetWorldPosition()` (mouse) → active tool's `UseAction(target)`.
- `PlayerController.FixedUpdate` (owner): `_rigidbody.linearVelocity = _moveInput * Speed;`.
- Range fields on harvest tools (Axe/Pickaxe): `_targetRadius=0.35`, `_fallbackSearchRadius=2`, `_serverInteractionRange=2`.

## Requirements

1. **Aim-facing foraging** — during foraging the player faces the **resource node** (not movement direction).
2. **Damage at end of swing** — the harvest RPC runs when the foraging animation/windup completes, not on click.
3. **Movement lock** — the player cannot move or dash while foraging; the swing commits once started.
4. **Shorter range** — `_fallbackSearchRadius` and `_serverInteractionRange` reduced from **2.0 → 1.2** on harvest tools.
5. Swing only begins when a valid target node exists at click time (no swinging at empty ground).

## Approach (A — approved)

The **tool owns the swing windup + damage**; `PlayerAnimation` is pure visual (begin/end driven); `PlayerController` locks movement by reading `PlayerAnimation.IsForaging`. No new components; coupling is via `GetComponent` on the shared root.

## Design

### Flow (owner)

1. Click → `ToolController.UseCurrentTool` → `HarvestToolBase.UseAction(mouseWorldPos)`.
2. `UseAction`: owner-only; `FindTarget(mouseWorldPos)`; **if no target → return (no swing)**; else begin swing: set `_isSwinging`, `_swingEnd = now + _swingDuration`, capture `_pendingTargetId`; call `_animation.BeginForaging(node.transform.position)`.
3. `HarvestToolBase.Update` (owner): when `now >= _swingEnd` → `_isSwinging=false`; `_animation.EndForaging()`; `InteractWithNodeServerRpc(_pendingTargetId)` (server re-validates range + applies damage).
4. Remote clients see facing + foraging via the existing `NetworkVariable`s; the locked position syncs via `ClientNetworkTransform`.

### Component changes

**`HarvestToolBase`** (`Assets/_Game/Scripts/Player/Tools/HarvestToolBase.cs`):
- Add `[SerializeField, Min(0.05f)] private float _swingDuration = 0.5f;`.
- Add fields `bool _isSwinging; float _swingEnd; ulong _pendingTargetId;` and `PlayerAnimation _animation;`.
- `OnNetworkSpawn`: `_animation = GetComponent<PlayerAnimation>();`.
- `UseAction(targetPosition)`: owner-only; if `_isSwinging` return; `NetworkObject target = FindTarget(targetPosition);` if null → log + return; else begin swing (set fields above) + `_animation?.BeginForaging(target.transform.position)`. **Remove the immediate `InteractWithNodeServerRpc` from here.**
- New `Update()`: `if (!IsOwner || !_isSwinging) return;` if `Time.time >= _swingEnd` → `_isSwinging=false; _animation?.EndForaging(); InteractWithNodeServerRpc(_pendingTargetId);`.
- `[Rpc(SendTo.Server, ...)] InteractWithNodeServerRpc(ulong, RpcParams)` stays unchanged (server validates range with the new 1.2 and applies damage).

**`PlayerAnimation`** (`Assets/_Game/Scripts/Player/PlayerAnimation.cs`):
- Remove the `InputReader.OnAttackPressed` subscription (in `OnNetworkSpawn`/`OnNetworkDespawn`), `HandleAttackPressed`, `_foragingUntil`, and the `_foragingDuration` field (the tool owns timing now).
- Add `public bool IsForaging { get; private set; }`.
- Add `public void BeginForaging(Vector3 worldTarget)`: `IsForaging = true;` set `_netState.Value = AnimState.Foraging;` set `_netFacing.Value = PlayerAnimLogic.ComputeFacing((Vector2)(worldTarget - transform.position), 0f, _netFacing.Value);` (threshold 0 ⇒ always picks dominant axis toward target).
- Add `public void EndForaging()`: `IsForaging = false;` (State reverts to Idle/Run on the next `SampleOwnerIntent`).
- `SampleOwnerIntent`: at the top, `if (IsForaging) { if (_netState.Value != AnimState.Foraging) _netState.Value = AnimState.Foraging; return; }` (keep the node-facing + Foraging state while swinging). The existing velocity-based facing/state logic runs only when not foraging.

**`PlayerController`** (`Assets/_Game/Scripts/Player/PlayerController.cs`):
- Add `private PlayerAnimation _animation;`; in `Awake`: `_animation = GetComponent<PlayerAnimation>();`.
- `FixedUpdate`: after the owner/null guard, `if (_animation != null && _animation.IsForaging) { _rigidbody.linearVelocity = Vector2.zero; return; }`.
- `HandleDashPressed`: at the top, `if (_animation != null && _animation.IsForaging) return;`.

### Range change (prefab, scalar — safe to hand-edit)

On the `AxeTool` and `PickaxeTool` MonoBehaviours in `DB_Player.prefab`:
- `_fallbackSearchRadius`: 2 → **1.2**
- `_serverInteractionRange`: 2 → **1.2**
- (`_targetRadius` stays 0.35.)

These are plain floats — Unity does not scramble scalar fields on import (unlike nested sprite sub-asset references), so editing the prefab YAML directly is safe.

## Testing

- **Test Runner (EditMode):** existing `PlayerAnimLogic` tests still pass; `ComputeFacing` is reused for aim-facing (threshold 0), so the diagonal/direction cases already cover it.
- **Play-test (owner):** click a resource node within 1.2 units → movement locks, player faces the node, foraging anim plays ~0.5s, **damage applies at the end** (node HP drops / depletes after the swing, not on click). Standing >1.2 units → no swing (no target found). Pressing a move key during the swing → no movement.
- **Play-test (2 clients):** remote client sees the owner face the node + foraging anim + the node take damage at swing end.

## Out of scope

- Combat (`WeaponTool`) and build (`BuilderTool`) animations — foraging anim applies to harvest tools only.
- Per-tool distinct foraging clips (single Watering Can set reused for up/down/side).
- Cancelable swings (the user chose commit/lock — `CancelAction` stays a no-op).
- Tuning `_swingDuration` beyond the 0.5s default (exposed as a serialized field for easy adjustment).
