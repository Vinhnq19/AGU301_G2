# Foraging Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rework harvest/foraging so the player locks movement, faces the resource node, plays the foraging animation, and deals damage only at the end of the swing — within a shorter 1.2-unit range.

**Architecture:** The harvest tool (`HarvestToolBase`) owns a swing windup timer and fires the damage RPC when it ends; `PlayerAnimation` becomes visual-only (`BeginForaging`/`EndForaging` driven, faces the node); `PlayerController` locks velocity + dash by reading `PlayerAnimation.IsForaging`. Coupling is via `GetComponent` on the shared root — no new components.

**Tech Stack:** Unity (C#), Unity Netcode for GameObjects, VContainer DI.

## Global Constraints

- Namespaces: `DungeonBuilder.Player` (PlayerAnimation, PlayerController), `DungeonBuilder.Player.Tools` (HarvestToolBase). `PlayerAnimation` is visible in `DungeonBuilder.Player.Tools` via parent-namespace lookup — no `using` needed.
- `sealed ... : NetworkBehaviour`, `[SerializeField] private`, `DBLog` for logging.
- Swing is owner-only and commits once started (not cancelable); `CancelAction` stays a no-op.
- Range change is plain-float edits in the prefab YAML — Unity does **not** scramble scalar fields (only nested sprite sub-asset refs), so direct edits are safe.
- No new unit tests: the aim-facing reuses `PlayerAnimLogic.ComputeFacing` (already covered by existing EditMode tests). Verification is Unity compile + play-test.

---

## File Structure

- **Modify** `Assets/_Game/Scripts/Player/PlayerAnimation.cs` — remove input-driven foraging; add `IsForaging`/`BeginForaging`/`EndForaging`.
- **Modify** `Assets/_Game/Scripts/Player/Tools/HarvestToolBase.cs` — swing windup; damage at end.
- **Modify** `Assets/_Game/Scripts/Player/PlayerController.cs` — lock movement + dash during foraging.
- **Modify** `Assets/_Game/Generated/Prefabs/Player/DB_Player.prefab` — `_fallbackSearchRadius` + `_serverInteractionRange`: 2 → 1.2 on AxeTool & PickaxeTool.
- **Revert** `Assets/_Game/Editor/PlayerAnimationSpriteSetup.cs` to its committed (menu-only) version; user runs it once to populate sprites.

---

## Task 1: Refactor PlayerAnimation for tool-driven foraging

**Files:**
- Modify: `Assets/_Game/Scripts/Player/PlayerAnimation.cs`

**Interfaces:**
- Produces: `public bool IsForaging { get; private set; }`, `public void BeginForaging(Vector3 worldTarget)`, `public void EndForaging()`.

- [ ] **Step 1: Rewrite PlayerAnimation.cs**

Replace the file with (note: removed `using VContainer`, `[Inject] Construct`, `_inputReader`, `OnNetworkDespawn`, `HandleAttackPressed`, `_foragingUntil`, `_foragingDuration`; `DriveVisual`/`SelectArray` unchanged):

```csharp
using DungeonBuilder.Core.Debugging;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Player
{
    /// <summary>
    /// Drives the player's visual: 3 directional sprite sets (up/down/side) and
    /// 3 animation states (Idle/Run/Foraging). The owner computes facing + state
    /// from velocity and syncs them via NetworkVariables; every client advances
    /// frames locally on the child "Visual" SpriteRenderer. Foraging is begun/ended
    /// by the harvest tool (BeginForaging turns the player to face the node).
    /// </summary>
    public sealed class PlayerAnimation : NetworkBehaviour
    {
        [System.Serializable]
        private sealed class DirectionalSprites
        {
            public Sprite[] up;
            public Sprite[] down;
            public Sprite[] side; // side-right; flipped horizontally when facing Left
        }

        [SerializeField] private DirectionalSprites _idle;
        [SerializeField] private DirectionalSprites _run;
        [SerializeField] private DirectionalSprites _foraging;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField, Min(0.01f)] private float _frameRate = 10f;
        [SerializeField, Min(0.001f)] private float _moveThreshold = 0.05f;

        private Rigidbody2D _rigidbody;

        private readonly NetworkVariable<FacingDir> _netFacing =
            new(FacingDir.Down, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<AnimState> _netState =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private float _animElapsed;
        private FacingDir _lastDrivenFacing = FacingDir.Down;
        private AnimState _lastDrivenState = AnimState.Idle;

        /// <summary>True while a foraging swing is in progress. Read by PlayerController to lock movement.</summary>
        public bool IsForaging { get; private set; }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        public override void OnNetworkSpawn()
        {
            DriveVisual(immediate: true);
            DBLog.Info($"anim.spawn.{NetworkObjectId}", $"PlayerAnimation spawned. isOwner={IsOwner}.", 0f, this);
        }

        /// <summary>Called by the harvest tool when a swing starts: face the target node and enter Foraging.</summary>
        public void BeginForaging(Vector3 worldTarget)
        {
            IsForaging = true;
            _netState.Value = AnimState.Foraging;
            _netFacing.Value = PlayerAnimLogic.ComputeFacing((Vector2)(worldTarget - transform.position), 0f, _netFacing.Value);
        }

        /// <summary>Called by the harvest tool when the swing ends; State reverts to Idle/Run on the next update.</summary>
        public void EndForaging()
        {
            IsForaging = false;
        }

        private void Update()
        {
            if (IsOwner)
            {
                SampleOwnerIntent();
            }

            DriveVisual(immediate: false);
        }

        private void SampleOwnerIntent()
        {
            if (IsForaging)
            {
                if (_netState.Value != AnimState.Foraging)
                {
                    _netState.Value = AnimState.Foraging;
                }
                return; // keep node-facing + Foraging state for the whole swing
            }

            Vector2 velocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero;
            float thresholdSq = _moveThreshold * _moveThreshold;

            FacingDir facing = PlayerAnimLogic.ComputeFacing(velocity, thresholdSq, _netFacing.Value);
            AnimState state = PlayerAnimLogic.ComputeState(velocity, thresholdSq, foraging: false);

            if (facing != _netFacing.Value)
            {
                _netFacing.Value = facing;
            }

            if (state != _netState.Value)
            {
                _netState.Value = state;
            }
        }

        private void DriveVisual(bool immediate)
        {
            FacingDir facing = _netFacing.Value;
            AnimState state = _netState.Value;

            if (facing != _lastDrivenFacing || state != _lastDrivenState)
            {
                _lastDrivenFacing = facing;
                _lastDrivenState = state;
                _animElapsed = 0f;
            }
            else if (!immediate)
            {
                _animElapsed += Time.deltaTime;
            }

            if (_renderer == null)
            {
                return;
            }

            Sprite[] arr = SelectArray(state, facing);
            if (arr == null || arr.Length == 0)
            {
                return;
            }

            int frame = PlayerAnimLogic.FrameAtTime(_animElapsed, 1f / _frameRate, arr.Length);
            _renderer.sprite = arr[frame];
            _renderer.flipX = facing == FacingDir.Left;
        }

        private Sprite[] SelectArray(AnimState state, FacingDir facing)
        {
            DirectionalSprites set = state switch
            {
                AnimState.Run => _run,
                AnimState.Foraging => _foraging,
                _ => _idle,
            };
            if (set == null)
            {
                return null;
            }

            return facing switch
            {
                FacingDir.Up => set.up,
                FacingDir.Down => set.down,
                _ => set.side,
            };
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/_Game/Scripts/Player/PlayerAnimation.cs
git commit -m "refactor(player): tool-driven foraging in PlayerAnimation"
```

---

## Task 2: HarvestToolBase swing windup + damage at end

**Files:**
- Modify: `Assets/_Game/Scripts/Player/Tools/HarvestToolBase.cs`

**Interfaces:**
- Consumes: `PlayerAnimation.BeginForaging(Vector3)`, `PlayerAnimation.EndForaging()` (Task 1).
- Produces: `UseAction` now starts a windup; damage (`InteractWithNodeServerRpc`) fires when the windup elapses.

- [ ] **Step 1: Add swing fields + cache PlayerAnimation**

In `HarvestToolBase`, add fields near the existing `[SerializeField]` fields and a private cache:

```csharp
[SerializeField, Min(0.05f)] private float _swingDuration = 0.5f;

private bool _isSwinging;
private float _swingEnd;
private ulong _pendingTargetId;
private PlayerAnimation _animation;
```

Add an `Awake` to cache the animation (same root):

```csharp
private void Awake()
{
    _animation = GetComponent<PlayerAnimation>();
}
```

- [ ] **Step 2: Rewrite UseAction to begin a swing (no immediate damage)**

Replace the body of `public void UseAction(Vector3 targetPosition)` with:

```csharp
public void UseAction(Vector3 targetPosition)
{
    if (!IsOwner || _isSwinging)
    {
        return;
    }

    NetworkObject target = FindTarget(targetPosition);
    if (target == null)
    {
        DBLog.Warning($"{ToolType}.send.no-target.{NetworkObjectId}", $"{ToolType} found no harvest target. click={targetPosition}, player={transform.position}.", 0.5f, this);
        return;
    }

    _isSwinging = true;
    _swingEnd = Time.time + _swingDuration;
    _pendingTargetId = target.NetworkObjectId;
    if (_animation != null)
    {
        _animation.BeginForaging(target.transform.position);
    }
}
```

- [ ] **Step 3: Add Update to fire damage at swing end**

Add this method (owner-only; `_isSwinging` is only ever set on the owner):

```csharp
private void Update()
{
    if (!IsOwner || !_isSwinging)
    {
        return;
    }

    if (Time.time < _swingEnd)
    {
        return;
    }

    _isSwinging = false;
    if (_animation != null)
    {
        _animation.EndForaging();
    }
    InteractWithNodeServerRpc(_pendingTargetId);
}
```

`[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)] private void InteractWithNodeServerRpc(ulong targetNetworkObjectId, RpcParams rpcParams = default)` stays **unchanged** — the server still re-validates existence / `IHarvestable` / range (`_serverInteractionRange`) before `harvestable.TakeDamageFrom(this)`.

- [ ] **Step 4: Commit**

```bash
git add Assets/_Game/Scripts/Player/Tools/HarvestToolBase.cs
git commit -m "feat(harvest): swing windup, damage at end, aim-facing"
```

---

## Task 3: PlayerController movement lock during foraging

**Files:**
- Modify: `Assets/_Game/Scripts/Player/PlayerController.cs`

**Interfaces:**
- Consumes: `PlayerAnimation.IsForaging` (Task 1).

- [ ] **Step 1: Cache PlayerAnimation + lock velocity + block dash**

In `PlayerController`, add the cache field and resolve it in `Awake`:

```csharp
private PlayerAnimation _animation;

private void Awake()
{
    _rigidbody = GetComponent<Rigidbody2D>();
    _animation = GetComponent<PlayerAnimation>();
}
```

In `FixedUpdate`, add the lock right after the owner/null guard:

```csharp
private void FixedUpdate()
{
    if (!IsOwner || _rigidbody == null)
    {
        return;
    }

    if (_animation != null && _animation.IsForaging)
    {
        _rigidbody.linearVelocity = Vector2.zero;
        return;
    }

    _rigidbody.linearVelocity = _moveInput * Speed;
}
```

In `HandleDashPressed`, block dashing while foraging (first line of the method body):

```csharp
private void HandleDashPressed()
{
    if (_animation != null && _animation.IsForaging)
    {
        return;
    }

    if (_rigidbody == null || Time.time - _lastDashTime < DashCooldown)
    {
        return;
    }
    // ... rest unchanged ...
```

- [ ] **Step 2: Commit**

```bash
git add Assets/_Game/Scripts/Player/PlayerController.cs
git commit -m "feat(player): lock movement + dash during foraging"
```

---

## Task 4: Range 2→1.2, sprite setup handoff, verify

**Files:**
- Modify: `Assets/_Game/Generated/Prefabs/Player/DB_Player.prefab` (AxeTool + PickaxeTool scalars)
- Revert: `Assets/_Game/Editor/PlayerAnimationSpriteSetup.cs` → committed (menu-only) version

- [ ] **Step 1: Reduce harvest range in the prefab**

First confirm the exact formatting on disk (Unity may write `2` or `2.0`):

```bash
grep -n "_fallbackSearchRadius\|_serverInteractionRange" Assets/_Game/Generated/Prefabs/Player/DB_Player.prefab
```

Then replace **all** occurrences (only AxeTool & PickaxeTool have these fields) of `_fallbackSearchRadius: 2` → `_fallbackSearchRadius: 1.2` and `_serverInteractionRange: 2` → `_serverInteractionRange: 1.2` (adjust to `2.0` if that's what's on disk). Use `replace_all`.

- [ ] **Step 2: Revert the editor script to its committed menu-only version**

(The auto-run variant was not approved; restore the menu-only version that's in git.)

```bash
git checkout HEAD -- Assets/_Game/Editor/PlayerAnimationSpriteSetup.cs
```

- [ ] **Step 3: Commit the range + revert**

```bash
git add Assets/_Game/Generated/Prefabs/Player/DB_Player.prefab Assets/_Game/Editor/PlayerAnimationSpriteSetup.cs
git commit -m "feat(harvest): interaction range 2.0 -> 1.2"
```

- [ ] **Step 4: Unity — populate sprites (one-time)**

In Unity, run **Tools ▸ Player Animation ▸ Setup Sprites** so the 9 sprite arrays populate (they were emptied/scrambled on import). Confirm the console logs `Done.` with no `Missing sprite` warnings.

- [ ] **Step 5: Play-test (owner)**

- Click a resource node within 1.2 units → player **locks** (can't move/dash), **faces the node**, foraging anim plays ~0.5s, **damage applies at the end** (node HP/depletes after the swing, not on click).
- Stand >1.2 units + click → **no swing** (no target found).
- Hold a move key during a swing → no movement.

- [ ] **Step 6: Play-test (2 clients)**

Remote client sees the owner face the node + foraging anim + the node take damage at swing end.

---

## Self-Review (completed during planning)

- **Spec coverage:** aim-facing → Task 1 `BeginForaging` (ComputeFacing toward node) + Task 2 passes `target.transform.position`. Damage at end → Task 2 `Update` fires RPC at `_swingEnd`. Movement lock → Task 3. Range 1.2 → Task 4. Swing only on valid target → Task 2 `FindTarget` null-check. All spec requirements covered.
- **Placeholder scan:** no TBD/TODO; every code step shows full code; range step includes the verify-grep.
- **Type consistency:** `BeginForaging(Vector3)` / `EndForaging()` / `IsForaging` match across Tasks 1→2→3. `InteractWithNodeServerRpc(ulong, RpcParams)` call matches its unchanged signature.
