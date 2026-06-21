# Player Animation System — Design

- **Date:** 2026-06-21
- **Feature:** Directional visual + animation state machine for `DB_Player`
- **Status:** Approved (pending spec review)

## Goal

Add a `PlayerAnimation` script to the `DB_Player` prefab that manages the player's visual: which of 3 directional visuals (up / down / side) is shown, and which animation state (Idle / Run / Foraging) is playing — fully synced over the network so every client sees the correct facing and animation.

## Context

- Unity project, multiplayer via **Unity Netcode for GameObjects** + **VContainer** DI.
- `DB_Player` prefab (`Assets/_Game/Generated/Prefabs/Player/DB_Player.prefab`): root `NetworkObject` with `Rigidbody2D`, `InputReader`, `PlayerController`, `PlayerStats`, tools, and a single child `Visual` with one `SpriteRenderer`. **No Animator** exists on the player.
- Movement: `PlayerController` sets `Rigidbody2D.linearVelocity = _moveInput * speed` in `FixedUpdate`; input comes from `InputReader` (Unity Input System, WASD confirmed). Runs only for `IsOwner`.
- No facing/direction logic exists. No player animation system exists.
- Bunny sprites in `Assets/Sprite/Bunny/`: sheets **IDLE (20 frames)**, **RUN (32 frames)**, plus DEATH, HOE, SCYTHE, SWORD, WATERING CAN. Sheets are laid out **by direction in rows** (top→bottom): side-right, side-left, down, up. Direction is NOT in the frame names (indexed only).

## Requirements

- **3 visuals** up/down/side, switched like a state machine by movement direction (e.g. press W → up visual). Side handles left+right via `flipX`.
- Each visual supports the **same animation states** with different clips: **Idle, Run, Foraging** (this iteration; no Sword/Death yet).
- **Full network sync**: facing + state replicate owner→all so remote clients see correct visuals.
- Follow project conventions: `DungeonBuilder.Player` namespace, `sealed` `NetworkBehaviour`, `[SerializeField] private`, DI via VContainer, `DBLog` for logging.

## Approach

**Custom code-driven sprite swapping** (chosen over Unity Animator). One `SpriteRenderer` swaps sprites per frame; the "3 visuals × same states" model is a 2D lookup `Sprite[state][direction][]`. No `.controller`/`.anim` assets to author — fits a code-driven workflow and networks trivially.

## Design

### Component: `PlayerAnimation`

- **File:** `Assets/_Game/Scripts/Player/PlayerAnimation.cs`
- **Namespace:** `DungeonBuilder.Player`
- **Class:** `public sealed class PlayerAnimation : NetworkBehaviour`
- **Attach to:** root `DB_Player` (alongside `PlayerController`/`PlayerStats`).

### Data model

```csharp
public enum FacingDir { Up, Down, Left, Right }  // Left/Right both render as "side" + flipX
public enum AnimState { Idle, Run, Foraging }

[Serializable]
sealed class DirectionalSprites
{
    public Sprite[] up;
    public Sprite[] down;
    public Sprite[] side;   // side-right row; flipped horizontally when facing Left
}

[SerializeField] private DirectionalSprites _idle;
[SerializeField] private DirectionalSprites _run;
[SerializeField] private DirectionalSprites _foraging;
[SerializeField] private SpriteRenderer _renderer;            // child "Visual"
[SerializeField, Min(0.01f)] private float _frameRate = 10f;
[SerializeField, Min(0.001f)] private float _moveThreshold = 0.05f;
[SerializeField, Min(0.05f)]  private float _foragingDuration = 0.5f;
```

### Logic

**Owner (`Update`, guarded by `IsOwner`):**
1. `velocity = _rigidbody.linearVelocity`.
2. Facing: if `velocity.sqrMagnitude > _moveThreshold²`: dominant axis wins → `Up/Down` (sign of y) or `Left/Right` (sign of x); else keep last facing.
3. State: if a foraging timer is active → `Foraging`; else `velocity.sqrMagnitude > threshold²` → `Run`; else `Idle`.
4. On change, write `NetworkVariable<FacingDir>` and `NetworkVariable<AnimState>` (owner-write).

**All clients (`Update`, including owner):**
1. Resolve current array: `arr = GetArray(_netState.Value, _netFacing.Value)` (maps `Left/Right` → `side`).
2. Advance frame locally: `frameTimer += dt`; when `≥ 1/_frameRate`, `frameIndex = (frameIndex + 1) % arr.Length`.
3. `_renderer.sprite = arr[frameIndex]; _renderer.flipX = (_netFacing.Value == FacingDir.Left);`
4. When facing/state changes, reset `frameIndex = 0`.

Frame index is **not** networked — only the two enums are. Per-client frame advancement is smooth; sub-frame desync is invisible.

### Networking

- `NetworkVariable<FacingDir> _netFacing` — `NetworkVariableReadPermission.Everyone`, `NetworkVariableWritePermission.Owner`.
- `NetworkVariable<AnimState> _netState` — same permissions.
- Matches the existing owner-authoritative model (`ClientNetworkTransform.OnIsServerAuthoritative() => false`).

### Foraging integration

- `PlayerAnimation` subscribes to `InputReader.OnAttackPressed` for the **owner only** (same pattern as `ToolController`).
- On attack press → `PlayForaging()`: set a foraging-end timestamp `now + _foragingDuration`. Owner's Update keeps `State = Foraging` until the timer, then recomputes Idle/Run.
- Remote clients see it via the `State` NetworkVariable.
- **Why subscribe to input, not the tool:** the attack button already fires `ToolController.UseCurrentTool()` → `HarvestToolBase.UseAction()`. Subscribing to `OnAttackPressed` directly in `PlayerAnimation` avoids modifying any tool code.
- **Future:** per-tool animations (Hoe/Water/Sword) branch via a tool enum here.

### Sprite mapping (confirm vs `.meta` at implementation)

Sheets laid out by row (top→bottom): side-right, side-left, down, up.

| State | up frames | down frames | side (right) frames |
|-------|-----------|-------------|---------------------|
| IDLE (5/row) | 15-19 | 10-14 | 0-4 |
| RUN (8/row) | 24-31 | 16-23 | 0-7 |
| Foraging (SCYTHE, TBD) | up row | down row | side-right row |

The side-left row is unused (left handled by `flipX`).

### Prefab / DI wiring

- Add `PlayerAnimation` component to `DB_Player.prefab` root.
- `_renderer` → child `Visual`'s SpriteRenderer.
- `InputReader`: inject via VContainer — register `PlayerAnimation` in `PlayerLifetimeScope`; add `[Inject]` for `InputReader`.
- `Rigidbody2D`: `GetComponent<Rigidbody2D>()` on the root.
- Assign the 9 sprite arrays in the Inspector (IDLE/RUN/Foraging × up/down/side).

### Testing

- **Owner:** WASD switches facing up/down/side with correct flip; releasing keys → Idle (keeps last facing); holding → Run cycle; attack → Foraging ~0.5s then Idle/Run.
- **Remote (host + 1 client):** the other player's facing + animation render correctly.
- **Edge:** diagonal movement (dominant axis wins), dash (velocity-driven so correct), abrupt stop.
- Manual play-test checklist; no automated tests for visuals.

## Out of scope (this iteration)

- Sword (combat) and Death animation states.
- Per-tool distinct animations (single Foraging state).
- Automated/CI tests for animation.
- Animator/AnimatorController assets.

## Open items

- Confirm exact slice rects / direction row order from the `.meta` files before wiring sprites (image analysis reads side-right/side-left/down/up top→bottom; verify against the slice Y coordinates).
- Confirm which Foraging sheet to use (default: SCYTHE).
