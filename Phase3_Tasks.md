# Paws & Care — Phase 3: Facility Building & Expansion

**Phase goal:** Turn the static, hand-authored facility into one the player **builds out and grows**. Buy stations/furniture/decor and place, rotate, and rearrange them on the grid within rooms; unlock pre-authored rooms to expand the footprint. Earn → invest → grow → earn more.

**Why this phase:** Phase 2 ends with a complete but *inert* level — it never changes, the grid is boot-time only, reputation gates nothing, and money has almost nothing to buy. This phase gives the player agency over their space and a reason to keep earning.

**Design decision (locked):** **no wall/door construction.** Rooms are hand-authored in the scene (preserving level-design quality); "expansion" = **unlocking a preset locked room**, which is a fraction of the cost of true construction and sidesteps exactly the complexity the TDD defers — *"wall rendering, and structural validation"* (§12.2). Within any open room, placement/decoration is free-form on the grid. This blends four concepts on purpose: **building** (rooms), **placement** (stations/furniture), **progression** (reputation-gated unlocks), **expansion** (growing footprint).

**First vertical slice:** **Buy & place a station** end-to-end — open the catalog (game pauses), pick an affordable blueprint, ghost-preview it on valid grid cells, confirm, pay, NavMesh rebakes, and the station immediately serves customers. Tasks 1→2→3→4.

**Maps to:** TDD §12 (Building System — Grid §12.1, Placement §12.3, NavMesh §12.4), extended past strict §12.2 MVP with the light room-unlock model above. GDD §8.1–8.2 (grid placement, rooms + Unlock Phases), §9.3 (reputation + money unlock layers; the XP skill tree stays post-MVP).

---

## Carried-over context from Phase 1–2 (verified against code)

Systems already in place that Phase 3 builds on:

- **`GridSystem`** (plain `MonoBehaviour`, **not a singleton** — components take a serialized reference, as `FacilityBuilder`/camera already do): `WorldToGrid` / `GridToWorld` / `GetCell` / `IsCellAvailable`, `CreateRoom(RoomType, cells)` / `GetRoomById` / `Rooms`, `Width` / `Height` / `CellSize`. Draws occupied/unwalkable gizmos (currently blank — occupancy is never populated).
- **`GridCell`**: `Position`, `IsOccupied`, `OccupiedBy` (GameObject), `RoomId`, `IsWalkable`, and setters `SetOccupied(GameObject)` / `SetRoomId(int)` / `SetWalkable(bool)`. **Occupancy setters are never called at runtime today** — Task 2 activates them.
- **`FacilityBuilder.Build()`** (boot): registers scene `RoomMarker`s via `GridSystem.CreateRoom`, then `navMeshSurface.BuildNavMesh()`. Called by `GameManager` for deterministic ordering.
- **`RoomMarker`**: `RoomType`, `GetCells()`.
- **`ServiceStation`**: self-registers with `StationManager` on spawn; its `SetOccupied(bool)` is the **service-busy reservation** (dispatcher-owned) — a *different concept* from grid-cell occupancy.
- **`EconomyManager.ApplyDelta`** chokepoint + `ExpenseIncurredEvent` / `ExpenseType` (append-only) + `BalanceChangedEvent`.
- **`ReputationManager`** (0–100) + `ReputationChangedEvent` — currently gates nothing.
- **`UIPanel` / `UIManager`** framework (panels pause the day via `pausesGame`); `HireScreen` is the reference panel. `StatusBarHud`, pooled `FloatingPopup`s, `MoneyFormatUtils`.
- **Interaction layer**: `IInteractable` / `InteractableType` / `InteractionMode` / `InteractionManager` / `AgentController` (raycast + selection).
- **`CustomerSpawner`** + seat-first `ServiceDispatcher` — new station capacity is consumed with zero extra wiring.

---

## Task 1 — Blueprint Data Foundations `[TODO]`

Mirrors the `ServiceData` / `PetDefinition` SO pattern: data in ScriptableObjects, logic in components.

### 1A — Blueprint ScriptableObject
- [x] 1A.1 `Blueprint : ScriptableObject` in `Scripts/Building/`: `displayName`, `description`, `cost`, `footprint` (Vector2Int, in cells), placed prefab reference, `uiIcon`
- [x] 1A.2 `category` (`BlueprintCategory` enum: `STATION`, `FURNITURE`, `DECORATION`) and optional `requiredRoomType` (`RoomType`; `NONE` = any room) — drives placement rules + which catalog tab it lives in
- [x] 1A.3 `requiredReputation` (0 = available from start) — the reputation gate for this blueprint
- [x] 1A.4 `[CreateAssetMenu]` (menu: `PawsAndCare/Building/Blueprint`)

### 1B — Enums & expense types (append-only per CLAUDE.md)
- [x] 1B.1 `BlueprintCategory` enum (own file, UPPER_SNAKE): `STATION`, `FURNITURE`, `DECORATION`
- [x] 1B.2 Append to `ExpenseType`: `FURNITURE` (buying/placing a blueprint) and `ROOM_UNLOCK` (Task 5) — **at end, never reorder**

### 1C — Blueprint assets
- [ ] 1C.1 `Blueprint_BathingStation` and `Blueprint_GroomingStation` from the existing station prefabs (available from start)
- [ ] 1C.2 One furniture + one decoration asset to prove the non-station categories place correctly
- [ ] 1C.3 One reputation-locked blueprint (e.g. `Blueprint_VetStation`, `requiredReputation` > 0) to prove gating end-to-end

---

## Task 2 — Floor-First Grid + Occupancy Activation `[DONE]` `(FOUNDATION)`

**Grew from "occupancy activation" into a foundation refactor** (see design decision below). Two problems: (1) the grid was *grid-first* — you hand-typed `width`/`height`/`cellSize` on `GridSystem` and grid-cell `origin`/`size` on each `RoomMarker`, coordinates offset from world space that required an edit→play→check loop to verify. (2) `GridCell` occupancy setters were never called at runtime, so `IsCellAvailable` treated authored-station cells as free. Both fixed by inverting the model: **you author in world space; the grid, rooms, and footprints all discretize from it. Nobody types grid coordinates or dimensions.** Occupancy becomes the spatial source of truth that placement (Task 3), rearrange/sell, layout **persistence** (Task 10 — the save data *is* the occupancy map), and **Chaos** spatial queries (Phase 4) all read.

**Design decisions (locked with user):** grid bounds derive **from a floor reference** (`GridSystem.lotFloor`, encapsulating child renderers); grid covers the **whole lot, static** (locked rooms' floors included — unlocking flips a room active over cells that already exist, no runtime regrow).

### 2A — GridSystem builds around the floor
- [x] 2A.1 Removed serialized `width`/`height`; keep only `cellSize` (1m, TDD §12.1). Derive `Origin`/`Width`/`Height` from `lotFloor`'s combined renderer bounds (`RecomputeMetrics`). Cached at runtime, recomputed live in-editor so gizmos/camera track the floor as it's resized
- [x] 2A.2 `WorldToGrid`/`GridToWorld` use the derived `Origin`; new public `Origin` property (camera pan-bounds switched from `transform.position` to it)
- [x] 2A.3 `GridSystem` gizmo recomputes from the floor → the grid overlay conforms live in the scene view (no play needed)

### 2B — RoomMarker derives cells from the floor
- [x] 2B.1 Removed `origin`/`size` fields — only `RoomType` is authored. `GetCells(grid)` derives cells from the floor's world bounds via `GridSystem.GetCellsInBounds` (min→max cell, edge-epsilon, clamped to grid)
- [x] 2B.2 Live per-cell gizmo draws the exact claimed cells — resize the floor, cells follow; misalignment is visible without entering play

### 2C — GridFootprint (occupancy)
- [x] 2C.1 `GridFootprint : MonoBehaviour` on placed objects: serializes a `Blueprint` (footprint source + cost for refunds/save). `Occupy(grid)` derives cells from world position (transform = footprint centre) and `SetOccupied(gameObject)`; `Free()` clears them; `OccupiedCells` exposed for rearrange/sell
- [x] 2C.2 Grid is **owner-injected** into `Occupy` — no per-prefab `GridSystem` ref, no runtime `Find`

### 2D — Boot registration + query helper
- [x] 2D.1 `FacilityBuilder` **auto-discovers** `RoomMarker`s and `GridFootprint`s (`FindObjectsByType`, one-time boot scan) — dropped the manual `roomMarkers` list, so a room/station can't be silently forgotten
- [x] 2D.2 `GridSystem.AreCellsAvailable(origin, footprint)` — multi-cell wrapper for Task 3 placement validity
- [ ] 2D.3 **Manual (editor):** assign `GridSystem.lotFloor`; add `GridFootprint` + `Blueprint` to authored station prefabs; grid-align stations; confirm the occupied-cell gizmo renders under them at play

**Correction applied:** the earlier plan said "stamp occupancy *before* the NavMesh bake" — dropped. `SetOccupied` sets `isOccupied`, which the bake never reads (NavMesh comes from geometry + `NavMeshModifier`), so ordering vs. bake is irrelevant. The room-lookup helper (old 2C.2) moved to Task 3, where "fits within one room" is actually used (YAGNI here).

---

## Task 3 — Build Mode & Placement `[TODO]` `(THE CRUX)`

Runtime placement on the now-truthful grid — TDD §12.3. Build mode is a distinct interaction mode entered from the catalog, with time paused.

### 3A — Build mode entry/exit
- [ ] 3A.1 Append `BUILD_MODE` to the existing `InteractionMode` enum (append-only); `InteractionManager`/`AgentController` route input to build mode while active
- [ ] 3A.2 `BuildModeController : MonoBehaviour` in `Scripts/Building/` (input-layer controller, like `AgentController`) — entered with a selected `Blueprint`, exited on place / cancel (right-click / Esc)
- [ ] 3A.3 Build mode pauses the day through `UIManager`/`pausesGame` (single pause owner — never call `DayManager.SetPaused` directly)

### 3B — Ghost preview & validation (§12.3)
- [ ] 3B.1 Mouse raycast → `WorldToGrid` → ghost prefab snapped to cell centers (`GridToWorld`), footprint-aware; re-evaluate on cell change, not every frame
- [ ] 3B.2 Validity rules: all footprint cells `AreCellsAvailable`; fits entirely within **one** room; respects `requiredRoomType`; doesn't block a room entrance (reachability — keep it simple: entrance cells stay walkable/reachable). Tint ghost valid/invalid (Sage Green / Coral, Art Bible)
- [ ] 3B.3 Rotate (90° steps, swaps footprint x/y) and cancel

### 3C — Placement commit
- [ ] 3C.1 On confirm: re-validate, then charge via `ExpenseIncurredEvent(cost, FURNITURE)` — **validate before charging** (same rule as `TryHire`)
- [ ] 3C.2 Instantiate the placed prefab at the footprint center; its `GridFootprint.Occupy()` stamps the cells; a placed `ServiceStation` self-registers with `StationManager` (existing behavior — Task 2B makes its occupancy real too)
- [ ] 3C.3 Publish `BlueprintPlacedEvent` (definition + grid origin) for milestones/UI/persistence

### 3D — Rearrange & sell
- [ ] 3D.1 Select an already-placed object in build mode → pick it up: `GridFootprint.Free()` its cells, re-enter the ghost flow to re-place it (no re-charge for a move)
- [ ] 3D.2 Sell/remove: `Free()` cells, destroy, refund a fraction via `ApplyDelta` (positive); publish `BlueprintRemovedEvent`

### 3E — NavMesh (§12.4)
- [ ] 3E.1 Async NavMesh rebake on **build-mode exit** (not per placement); agents mid-path must survive it (watch the `HasReachedDestination` arrival latch fixed in Phase 2)

---

## Task 4 — Build Catalog UI `[TODO]`

Second real panel on the `UIPanel` framework (`HireScreen` is the template).

- [ ] 4.1 `BuildMenuScreen : UIPanel` in `Scripts/UI/` — lists `Blueprint`s (icon, name, cost), grouped/filtered by `BlueprintCategory`; locked entries greyed with their reputation requirement
- [ ] 4.2 Entry click → close panel → enter build mode with that definition (hand pause from panel to build mode with no un-pause flicker)
- [ ] 4.3 Affordability greying via `BalanceChangedEvent` while open (same pattern as `HireScreen`); locked state via `ProgressionManager` (Task 5)
- [ ] 4.4 Status-bar **Build** button (beside Hire) opens the catalog

---

## Task 5 — Room Unlocking & Expansion `[TODO]`

The progression + expansion layer: pre-authored rooms that start locked and open up when purchased. No construction — just registering an existing room and extending the NavMesh.

### 5A — ProgressionManager
- [ ] 5A.1 `ProgressionManager : Singleton<ProgressionManager>` in `Scripts/Progression/` — single query point `IsUnlocked(Blueprint)` (reputation gate) and room-unlock state
- [ ] 5A.2 Subscribes to `ReputationChangedEvent`; crossing a threshold publishes `BlueprintUnlockedEvent`. Unlocks are **latching** (a later reputation drop never re-locks — punitive; GDD §9.2)

### 5B — Locked expansion rooms
- [ ] 5B.1 `ExpansionRoom : MonoBehaviour` in `Scripts/Building/` — holds its `RoomMarker`, unlock cost, optional `requiredReputation` (maps to the GDD §8.2 Unlock-Phase column), and a locked visual (fence/tarp)
- [ ] 5B.2 Locked rooms are click-interactable (`IInteractable`) showing "Unlock — $X" (or "Needs reputation Y" when gated)
- [ ] 5B.3 On purchase: charge `ExpenseIncurredEvent(cost, ROOM_UNLOCK)`; `GridSystem.CreateRoom(marker.RoomType, marker.GetCells())`; swap locked→open visual; async NavMesh update so the new floor is pathable; publish `RoomUnlockedEvent`
- [ ] 5B.4 At least one locked room in the scene proving: unlock room → place stations inside → customers use them (zero extra dispatch wiring)

---

## Task 6 — Decoration & Milestones (light) `[TODO]`

### 6A — Decoration
- [ ] 6A.1 `DECORATION` blueprints place exactly like furniture (Task 3 flow) — visual only for now; the **ambiance *score*** (GDD §8.3) is deferred (it's a system unto itself)

### 6B — Milestones
- [ ] 6B.1 `MilestoneTracker : MonoBehaviour` in `Scripts/Progression/` (non-singleton, event-driven), 2–3 launch milestones from GDD §9.1: **Employee of the Month** (first hire), **Expanding Horizons** (first room unlock), **Five Star Review** (N high-quality services)
- [ ] 6B.2 Reward = money bonus via `ApplyDelta` + popup; publishes `MilestoneReachedEvent`. State is runtime-only for now, flagged as save data for Task 10

---

## Task 7 — Building Validation `[TODO]`

- [ ] 7.1 Catalog opens (game pauses); station purchase charges the right amount; placement occupies the right cells (gizmo confirms)
- [ ] 7.2 Placed station serves customers with zero manual wiring (self-registration + dispatcher pick it up)
- [ ] 7.3 Invalid placements impossible: occupied cells, out of bounds, spanning two rooms, wrong/locked room, blocked entrance, insufficient funds
- [ ] 7.4 Rearrange (free move) and sell (refund) update occupancy correctly; no orphaned occupied cells
- [ ] 7.5 NavMesh updates on build-mode exit; pets/workers path around new objects; agents mid-path don't deadlock
- [ ] 7.6 Reputation threshold unlocks a locked blueprint at runtime (latching); locked room unlock → build inside → full loop
- [ ] 7.7 Full building playtest across a day; more stations = more throughput/revenue

---

## Later / Post-MVP (out of scope for Phase 3)

Per TDD §12.2 + GDD §8–9: **wall/door construction & free-form room drawing**, multi-floor + lot purchase / relocation, ambiance **scoring** + decoration themes, cleanliness/mess per-cell, the XP **skill tree** (third unlock layer), sell/move refinements beyond the basics, customer-tier scaling from reputation. Chaos-prevention upgrades (GDD §8.4) land in **Phase 4** on top of this system.

---

## Architectural Notes (Phase 3)

- **Grid is the single source of truth for space.** All placement/room validity flows through `GridSystem` + `GridCell` occupancy. No secondary occupancy bookkeeping. Occupancy is populated for *both* authored (boot) and runtime-placed objects so the two are indistinguishable to consumers.
- **`GridSystem` is not a singleton** — Phase 3 components take a serialized reference (the established pattern), never a global lookup.
- **Economy chokepoint untouched.** Buying furniture and unlocking rooms spend via `ExpenseIncurredEvent` → `ApplyDelta`, like salaries/hiring. New `ExpenseType` values are appended, never reordered.
- **Pause ownership stays with `UIManager`.** Build mode reuses the same pause path panels use; nothing else touches `DayManager.SetPaused` directly.
- **Unlock state lives in one place** (`ProgressionManager`), queried (never cached) by UI. Events (`BlueprintUnlockedEvent` / `RoomUnlockedEvent`) notify; the manager answers.
- **Pre-authored rooms over procedural construction.** Player agency is *what to place and when to expand*, not drawing walls — a fraction of the tooling cost, consistent with the scene-authored facility, and honoring the TDD's stated reason for deferring construction.
- **Persistence impact (Task 10, deferred):** placed blueprints (the occupancy map), unlocked rooms, unlock + milestone state all become save data — a core reason persistence runs after this phase.

---

## Phase 3 Definition of Done `[DRAFT]`

- [ ] Player can buy and place ≥2 station types + furniture/decor from a catalog; placed stations serve customers immediately
- [ ] Objects can be rearranged (free move) and sold (partial refund) with occupancy staying correct
- [ ] Grid occupancy is truthful for authored *and* placed objects (gizmo confirms); placement validity enforces all §12.3 rules
- [ ] At least one locked room unlocks (money + optional reputation gate) and is buildable inside
- [ ] At least one blueprint is reputation-gated and unlocks at runtime (latching)
- [ ] All spending flows through `ApplyDelta` with appended `ExpenseType`s; NavMesh stays correct after every change
- [ ] 2–3 milestones fire with rewards
- [ ] All code follows CLAUDE.md conventions
