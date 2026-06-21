# Tower Unlock Shop — Design

- **Date:** 2026-06-22
- **Feature:** Buy towers in the shop with tokens to unlock them; initially only Arrow Tower is unlocked. A SO configures default open/locked state per TowerType. Resets each match.
- **Status:** Approved (pending spec review)

## Goal

Players unlock towers by spending **Tokens** in the shop. At match start only **Arrow Tower** is unlocked; a `TowerUnlockConfigSO` configures which TowerTypes are unlocked by default. Buying a tower (one-time, Token cost) flips its unlock flag so it becomes buildable. State resets each match.

## Context (already in place)

- `CurrencyType.Token` → `ResourceType.Token` (`Shop/CurrencyType.cs`).
- `ResourceType` has per-tower unlock flags: `ArrowTowerUnlock=10 … LaserTowerUnlock=14`.
- `TowerDataSO` already has `int unlockTokenCost = 10;` and `ResourceType unlockResourceType = ArrowTowerUnlock;`.
- `TowerCatalogSO.Towers` lists all `TowerDataSO`.
- `Shop.ProcessBuyItem` already: finds item by ResourceType → spends `Price*qty` in the item's `CurrencyType` → `_sharedResources.TryAdd(resourceType, qty)`. So buying an item with `ResourceType=CannonTowerUnlock, CurrencyType=Token` unlocks it.
- `TowerSelectionPresenter.RefreshAffordability` already gates building by `GetAmount(data.unlockResourceType) > 0`, and refreshes on `ResourceChanged`.
- `ShopItem` is a `ScriptableObject`; `ShopModel.items : List<ShopItem>`. The shop is a trigger zone (`OnTriggerEnter2D` → open).
- Tower types: `Arrow, Cannon, Frost, SpikeTrap, Laser`.

## Requirements

1. At match start, towers listed in the config SO are unlocked (their unlock ResourceType set to 1); all others locked (0).
2. Every tower has a `ShopItemPanel` in the **Token tab** (Token currency, one-time). Default-unlocked towers display as unlocked (sold-out); locked towers are buyable.
3. Buying a tower item spends its `unlockTokenCost` in Tokens and sets its unlock flag → it becomes buildable.
4. Unlock state resets each match (no save system).
5. Reuse existing `Shop.BuyItem` + build gating — no new transaction/network code.

## Design

### 1. `TowerUnlockConfigSO` (new)
`Assets/_Game/Scripts/Data/TowerUnlockConfigSO.cs`:
```csharp
[CreateAssetMenu(fileName="TowerUnlockConfig", menuName="Dungeon Builder/Data/Tower Unlock Config")]
public sealed class TowerUnlockConfigSO : ScriptableObject
{
    [SerializeField] private TowerType[] _defaultUnlocked = { TowerType.Arrow };
    public IReadOnlyList<TowerType> DefaultUnlocked => _defaultUnlocked;
}
```

### 2. `Shop` (modified) — server-side init
Add serialized refs: `[SerializeField] private TowerCatalogSO _towerCatalog;` and `[SerializeField] private TowerUnlockConfigSO _unlockConfig;`.

In `OnNetworkSpawn` (server), **before** `InitializeNetworkData()`:
- `ApplyDefaultUnlocks()`: for each `TowerType` in `_unlockConfig.DefaultUnlocked`, find the matching `TowerDataSO` in `_towerCatalog.Towers`, then `_sharedResources.TrySet(data.unlockResourceType, 1)`.

Tower shop items are **NOT auto-generated**. The designer authors `ShopItem` assets (one per purchasable tower) and adds them to `ShopModel.items` (see #4). They appear under the Token tab via the existing `GetItemsByType(Token)`.

Then the existing `InitializeNetworkData()` syncs all items (resources + unlocks) into the `NetworkList`.

### 3. Purchase + build (unchanged systems)
- Purchase: `ShopPresenter.HandleBuyItem` → `Shop.BuyItem(unlockRT, qty=1)` → server spends Tokens, `TryAdd(unlockRT, 1)`, marks stock 0 (sold out). The quantity popup is clamped to stock (1).
- Build: `TowerSelectionPresenter` already treats `GetAmount(unlockResourceType) > 0` as unlocked; on `ResourceChanged` it refreshes, so the newly unlocked tower becomes placeable.

### 4. Assets / wiring
- Create `TowerUnlockConfig` asset (DefaultUnlocked = `{ Arrow }`).
- Tower `unlockResourceType` already set in `DB_*TowerData.asset` (Arrow=10, Cannon=11, Frost=12).
- **Author one `ShopItem` asset per purchasable tower** (e.g. `ItemShop_CannonTower`) and add it to `ShopModel.items`:
  - `ResourceType` = the tower's unlock flag (e.g. `CannonTowerUnlock`), `CurrencyType` = `Token`, `Price` = unlock cost, `RemainingQuantity` = 1 (one-time), `isUnlimited` = false, `isSellable` = false.
  - Default-unlocked towers (Arrow) need no shop item (already unlocked via the config); optionally add one with `RemainingQuantity` = 0 to show as sold-out.
- Wire `_towerCatalog` + `_unlockConfig` on the Shop (scene/prefab inspector).
- Unlock items render under the existing **Token tab**.

## Testing
- Test Runner (EditMode): existing pure-logic tests still pass.
- Play-test (host): walk into shop zone → Token tab shows the tower `ShopItem`s you added; Arrow is buildable immediately (default-unlocked). Buy Cannon (spends Tokens) → Cannon becomes buildable; its shop item is now sold out. Insufficient Tokens → buy fails (existing afford feedback).

## Out of scope
- Cross-session persistence (save/load) — resets each match by design.
- A dedicated "Towers" shop tab (unlock items use the Token tab).
- Server-side anti-cheat beyond the existing `CanAfford`/`TrySpend` checks.
